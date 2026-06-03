using MusicPixelPet.Wpf.Models;
using NAudio.Dsp;
using NAudio.Wave;
using NWaves.FeatureExtractors;
using NWaves.FeatureExtractors.Options;
using NWaves.Windows;

namespace MusicPixelPet.Wpf.Services;

public sealed class AudioAnalyzerService : IDisposable
{
    public const int FftSize = 2048;
    private const int FftExponent = 11;
    private const int MagnitudeSpectrumLength = FftSize / 2 + 1;
    private const int MfccFeatureCount = SpectrumData.MfccLength;
    private const int MfccFilterBankSize = 26;
    private const float SilenceFloor = 0.000001f;
    private const float BassNoiseFloor = 0.0025f;
    private const float BassBeatMultiplier = 1.5f;
    private const float SpectrumSmoothingNewWeight = 0.3f;
    private const float SpectrumSmoothingPreviousWeight = 0.7f;
    private const float MfccSmoothingNewWeight = 0.2f;
    private const float MfccSmoothingPreviousWeight = 0.8f;
    private const float SpectralSmoothingNewWeight = 0.2f;
    private const float SpectralSmoothingPreviousWeight = 0.8f;
    private const float RolloffThreshold = 0.85f;
    private static readonly TimeSpan BeatMinimumGap = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan BeatHistoryWindow = TimeSpan.FromSeconds(5);

    private readonly object _syncRoot = new();
    private readonly object _beatSyncRoot = new();
    private readonly float[] _sampleBuffer = new float[FftSize];
    private readonly Complex[] _fftBuffer = new Complex[FftSize];
    private readonly float[] _window = new float[FftSize];
    private readonly float[] _magnitudeSpectrum = new float[MagnitudeSpectrumLength];
    private readonly float[] _previousMagnitudeSpectrum = new float[MagnitudeSpectrumLength];
    private readonly float[] _rawMfcc = new float[MfccFeatureCount];
    private readonly float[] _smoothedMfcc = new float[MfccFeatureCount];
    private readonly Queue<DateTimeOffset> _recentBeats = new(18);
    private readonly double[] _beatIntervals = new double[16];
    private readonly BeatEventArgs _beatEventArgs = new(0, DateTimeOffset.MinValue, 0);
    private readonly SpectralCentroidExtractor _centroidExtractor = new();
    private readonly SpectralFluxExtractor _fluxExtractor = new();
    private readonly SpectralRolloffExtractor _rolloffExtractor = new(RolloffThreshold);
    private WasapiLoopbackCapture? _capture;
    private WaveFormat? _waveFormat;
    private MfccExtractor? _mfccExtractor;
    private int _bufferIndex;
    private int _sampleRate = 44100;
    private float _rollingBass;
    private SpectrumData _smoothedSpectrum;
    private DateTimeOffset _lastBeatAt = DateTimeOffset.MinValue;
    private bool _isRunning;
    private bool _hasSmoothedSpectrum;
    private bool _hasPreviousMagnitudeSpectrum;

    public AudioAnalyzerService()
    {
        FillHammingWindow(_window);
        _smoothedSpectrum = new SpectrumData(0, 0, 0, 0, _smoothedMfcc, 0, 0, 0);
    }

    public event EventHandler<float>? LevelChanged;
    public event EventHandler<BeatEventArgs>? BeatDetected;
    public event EventHandler<SpectrumData>? SpectrumAnalyzed;
    public event EventHandler<string>? ErrorOccurred;

    public Task StartAsync()
    {
        return Task.Run(() =>
        {
            lock (_syncRoot)
            {
                if (_isRunning)
                {
                    return;
                }

                _capture = new WasapiLoopbackCapture();
                _waveFormat = _capture.WaveFormat;
                _sampleRate = _waveFormat.SampleRate;
                _bufferIndex = 0;
                _hasSmoothedSpectrum = false;
                _hasPreviousMagnitudeSpectrum = false;
                Array.Clear(_magnitudeSpectrum);
                Array.Clear(_previousMagnitudeSpectrum);
                Array.Clear(_rawMfcc);
                Array.Clear(_smoothedMfcc);
                _smoothedSpectrum = new SpectrumData(0, 0, 0, 0, _smoothedMfcc, 0, 0, 0);
                _mfccExtractor = CreateMfccExtractor(_sampleRate);
                ResetBeatState();
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;
                _capture.StartRecording();
                _isRunning = true;
            }
        });
    }

    public Task StopAsync()
    {
        return Task.Run(() =>
        {
            lock (_syncRoot)
            {
                if (!_isRunning || _capture is null)
                {
                    return;
                }

                _capture.StopRecording();
            }
        });
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_capture is not null)
            {
                _capture.DataAvailable -= OnDataAvailable;
                _capture.RecordingStopped -= OnRecordingStopped;
                _capture.Dispose();
                _capture = null;
            }

            _waveFormat = null;
            _mfccExtractor = null;
            _isRunning = false;
            _hasSmoothedSpectrum = false;
            ResetBeatState();
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        try
        {
            var format = _waveFormat;
            if (format is null || args.BytesRecorded <= 0)
            {
                return;
            }

            ProcessSamples(args.Buffer, args.BytesRecorded, format);
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(this, exception.Message);
        }
    }

    private void ProcessSamples(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        var channels = Math.Max(1, format.Channels);
        var bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
        var bytesPerFrame = bytesPerSample * channels;
        var completeFrames = bytesRecorded / bytesPerFrame;

        for (var frame = 0; frame < completeFrames; frame += 1)
        {
            var frameOffset = frame * bytesPerFrame;
            var monoSample = 0f;

            for (var channel = 0; channel < channels; channel += 1)
            {
                monoSample += ReadSample(buffer, frameOffset + channel * bytesPerSample, format);
            }

            AddSample(monoSample / channels);
        }
    }

    private void AddSample(float sample)
    {
        _sampleBuffer[_bufferIndex] = sample;
        _bufferIndex += 1;

        if (_bufferIndex < FftSize)
        {
            return;
        }

        _bufferIndex = 0;
        AnalyzeCurrentBuffer();
    }

    private void AnalyzeCurrentBuffer()
    {
        var rawSpectrum = AnalyzeSamples(
            _sampleBuffer,
            _sampleRate,
            _fftBuffer,
            _window,
            _magnitudeSpectrum,
            _previousMagnitudeSpectrum,
            ref _hasPreviousMagnitudeSpectrum,
            _mfccExtractor,
            _centroidExtractor,
            _fluxExtractor,
            _rolloffExtractor,
            _rawMfcc);
        DetectBeat(rawSpectrum);

        var smoothedSpectrum = SmoothSpectrum(rawSpectrum);
        SpectrumAnalyzed?.Invoke(this, smoothedSpectrum);
        LevelChanged?.Invoke(this, smoothedSpectrum.Rms);
    }

    public static SpectrumData AnalyzeSamples(float[] samples, int sampleRate)
    {
        var fftBuffer = new Complex[FftSize];
        var window = new float[FftSize];
        var magnitudeSpectrum = new float[MagnitudeSpectrumLength];
        var previousMagnitudeSpectrum = new float[MagnitudeSpectrumLength];
        var mfcc = new float[MfccFeatureCount];
        var hasPreviousMagnitudeSpectrum = false;
        var centroidExtractor = new SpectralCentroidExtractor();
        var fluxExtractor = new SpectralFluxExtractor();
        var rolloffExtractor = new SpectralRolloffExtractor(RolloffThreshold);
        FillHammingWindow(window);
        return AnalyzeSamples(
            samples,
            sampleRate,
            fftBuffer,
            window,
            magnitudeSpectrum,
            previousMagnitudeSpectrum,
            ref hasPreviousMagnitudeSpectrum,
            null,
            centroidExtractor,
            fluxExtractor,
            rolloffExtractor,
            mfcc);
    }

    private static SpectrumData AnalyzeSamples(
        float[] samples,
        int sampleRate,
        Complex[] fftBuffer,
        float[] window,
        float[] magnitudeSpectrum,
        float[] previousMagnitudeSpectrum,
        ref bool hasPreviousMagnitudeSpectrum,
        MfccExtractor? mfccExtractor,
        SpectralCentroidExtractor centroidExtractor,
        SpectralFluxExtractor fluxExtractor,
        SpectralRolloffExtractor rolloffExtractor,
        float[] mfcc)
    {
        if (samples.Length < FftSize)
        {
            throw new ArgumentException($"At least {FftSize} samples are required.", nameof(samples));
        }

        double rmsSum = 0;
        for (var index = 0; index < FftSize; index += 1)
        {
            var sample = samples[index];
            rmsSum += sample * sample;
            fftBuffer[index].X = sample * window[index];
            fftBuffer[index].Y = 0;
        }

        FastFourierTransform.FFT(forward: true, FftExponent, fftBuffer);
        FillMagnitudeSpectrum(fftBuffer, magnitudeSpectrum);
        mfccExtractor?.ProcessFrame(samples, mfcc);

        var result = default(SpectrumData);
        result.Rms = (float)Math.Sqrt(rmsSum / FftSize);
        result.Bass = CalculateBandMagnitude(magnitudeSpectrum, sampleRate, 20, 250);
        result.Mid = CalculateBandMagnitude(magnitudeSpectrum, sampleRate, 250, 4000);
        result.High = CalculateBandMagnitude(magnitudeSpectrum, sampleRate, 4000, 20000);
        result.Mfcc = mfcc;
        result.Centroid = centroidExtractor.ProcessFrame(magnitudeSpectrum, sampleRate);
        result.Flux = fluxExtractor.ProcessFrame(
            magnitudeSpectrum,
            previousMagnitudeSpectrum,
            hasPreviousMagnitudeSpectrum);
        result.Rolloff = rolloffExtractor.ProcessFrame(magnitudeSpectrum, sampleRate);

        CopyMagnitudeSpectrum(magnitudeSpectrum, previousMagnitudeSpectrum);
        hasPreviousMagnitudeSpectrum = true;

        return result;
    }

    private static void FillMagnitudeSpectrum(Complex[] fftBuffer, float[] magnitudeSpectrum)
    {
        for (var bin = 0; bin < MagnitudeSpectrumLength; bin += 1)
        {
            var real = fftBuffer[bin].X;
            var imaginary = fftBuffer[bin].Y;
            magnitudeSpectrum[bin] = (float)Math.Sqrt(real * real + imaginary * imaginary);
        }
    }

    private static float CalculateBandMagnitude(float[] magnitudeSpectrum, int sampleRate, float minFrequency, float maxFrequency)
    {
        var nyquist = sampleRate / 2f;
        var binFrequency = sampleRate / (float)FftSize;
        var maxUsableFrequency = Math.Min(maxFrequency, nyquist);
        var startBin = Math.Max(1, (int)MathF.Ceiling(minFrequency / binFrequency));
        var endBin = Math.Min(MagnitudeSpectrumLength - 1, (int)MathF.Floor(maxUsableFrequency / binFrequency));

        if (endBin < startBin)
        {
            return 0;
        }

        double magnitudeSum = 0;
        var binCount = endBin - startBin + 1;
        for (var bin = startBin; bin <= endBin; bin += 1)
        {
            magnitudeSum += magnitudeSpectrum[bin];
        }

        return (float)(magnitudeSum / binCount);
    }

    private static void CopyMagnitudeSpectrum(float[] source, float[] target)
    {
        for (var index = 0; index < MagnitudeSpectrumLength; index += 1)
        {
            target[index] = source[index];
        }
    }

    private static void FillHammingWindow(float[] window)
    {
        for (var index = 0; index < window.Length; index += 1)
        {
            window[index] = (float)FastFourierTransform.HammingWindow(index, window.Length);
        }
    }

    private void DetectBeat(SpectrumData spectrum)
    {
        var bass = spectrum.Bass;
        var baseline = Math.Max(_rollingBass, BassNoiseFloor);
        var now = DateTimeOffset.Now;
        var isBeat = bass > BassNoiseFloor
            && bass > baseline * BassBeatMultiplier
            && now - _lastBeatAt >= BeatMinimumGap;

        _rollingBass = _rollingBass <= SilenceFloor
            ? bass
            : (_rollingBass * 0.92f) + (bass * 0.08f);

        if (!isBeat)
        {
            return;
        }

        _lastBeatAt = now;
        var bpm = UpdateBpm(now);
        _beatEventArgs.Update(bass, now, bpm);
        BeatDetected?.Invoke(this, _beatEventArgs);
    }

    private SpectrumData SmoothSpectrum(SpectrumData next)
    {
        if (!_hasSmoothedSpectrum)
        {
            _smoothedSpectrum.Rms = next.Rms;
            _smoothedSpectrum.Bass = next.Bass;
            _smoothedSpectrum.Mid = next.Mid;
            _smoothedSpectrum.High = next.High;
            _smoothedSpectrum.Centroid = next.Centroid;
            _smoothedSpectrum.Flux = next.Flux;
            _smoothedSpectrum.Rolloff = next.Rolloff;
            CopyMfcc(next.Mfcc, _smoothedMfcc);
            _hasSmoothedSpectrum = true;
            return _smoothedSpectrum;
        }

        _smoothedSpectrum.Rms = LowPass(next.Rms, _smoothedSpectrum.Rms);
        _smoothedSpectrum.Bass = LowPass(next.Bass, _smoothedSpectrum.Bass);
        _smoothedSpectrum.Mid = LowPass(next.Mid, _smoothedSpectrum.Mid);
        _smoothedSpectrum.High = LowPass(next.High, _smoothedSpectrum.High);
        _smoothedSpectrum.Centroid = LowPassSpectral(next.Centroid, _smoothedSpectrum.Centroid);
        _smoothedSpectrum.Flux = LowPassSpectral(next.Flux, _smoothedSpectrum.Flux);
        _smoothedSpectrum.Rolloff = LowPassSpectral(next.Rolloff, _smoothedSpectrum.Rolloff);
        SmoothMfcc(next.Mfcc, _smoothedMfcc);

        return _smoothedSpectrum;
    }

    private static float LowPass(float next, float previous)
    {
        return next * SpectrumSmoothingNewWeight + previous * SpectrumSmoothingPreviousWeight;
    }

    private static float LowPassSpectral(float next, float previous)
    {
        return next * SpectralSmoothingNewWeight + previous * SpectralSmoothingPreviousWeight;
    }

    private static MfccExtractor CreateMfccExtractor(int sampleRate)
    {
        return new MfccExtractor(new MfccOptions
        {
            SamplingRate = sampleRate,
            FeatureCount = MfccFeatureCount,
            FrameSize = FftSize,
            HopSize = FftSize,
            FftSize = FftSize,
            FilterBankSize = MfccFilterBankSize,
            Window = WindowType.Hamming,
            PreEmphasis = 0.97
        });
    }

    private static void CopyMfcc(float[] source, float[] target)
    {
        for (var index = 0; index < MfccFeatureCount; index += 1)
        {
            target[index] = Sanitize(source[index]);
        }
    }

    private static void SmoothMfcc(float[] source, float[] target)
    {
        for (var index = 0; index < MfccFeatureCount; index += 1)
        {
            var next = Sanitize(source[index]);
            target[index] = next * MfccSmoothingNewWeight + target[index] * MfccSmoothingPreviousWeight;
        }
    }

    private static float Sanitize(float value)
    {
        return float.IsFinite(value) ? value : 0;
    }

    private float UpdateBpm(DateTimeOffset detectedAt)
    {
        lock (_beatSyncRoot)
        {
            _recentBeats.Enqueue(detectedAt);

            while (_recentBeats.Count > 0 && detectedAt - _recentBeats.Peek() > BeatHistoryWindow)
            {
                _recentBeats.Dequeue();
            }

            while (_recentBeats.Count > _beatIntervals.Length + 1)
            {
                _recentBeats.Dequeue();
            }

            if (_recentBeats.Count < 3)
            {
                return 0;
            }

            var intervalCount = 0;
            DateTimeOffset? previousBeat = null;
            foreach (var beat in _recentBeats)
            {
                if (previousBeat is not null)
                {
                    _beatIntervals[intervalCount] = (beat - previousBeat.Value).TotalMilliseconds;
                    intervalCount += 1;
                }

                previousBeat = beat;
            }

            Array.Sort(_beatIntervals, 0, intervalCount);
            var medianInterval = intervalCount % 2 == 0
                ? (_beatIntervals[intervalCount / 2 - 1] + _beatIntervals[intervalCount / 2]) / 2
                : _beatIntervals[intervalCount / 2];

            if (medianInterval <= 0)
            {
                return 0;
            }

            var bpm = (float)(60000.0 / medianInterval);
            while (bpm < 60)
            {
                bpm *= 2;
            }

            while (bpm > 200)
            {
                bpm /= 2;
            }

            return bpm is >= 60 and <= 200 ? bpm : 0;
        }
    }

    private void ResetBeatState()
    {
        lock (_beatSyncRoot)
        {
            _rollingBass = 0;
            _lastBeatAt = DateTimeOffset.MinValue;
            _recentBeats.Clear();
        }
    }

    private static float ReadSample(byte[] buffer, int offset, WaveFormat format)
    {
        if (format.Encoding is WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            return BitConverter.ToSingle(buffer, offset);
        }

        return format.BitsPerSample switch
        {
            16 => BitConverter.ToInt16(buffer, offset) / 32768f,
            24 => ReadPcm24(buffer, offset) / 8388608f,
            32 => BitConverter.ToInt32(buffer, offset) / 2147483648f,
            _ => 0
        };
    }

    private static int ReadPcm24(byte[] buffer, int offset)
    {
        var sample = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
        return (sample & 0x800000) == 0 ? sample : sample | unchecked((int)0xFF000000);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs args)
    {
        if (args.Exception is not null)
        {
            ErrorOccurred?.Invoke(this, args.Exception.Message);
        }

        Dispose();
    }

    private sealed class SpectralCentroidExtractor
    {
        public float ProcessFrame(float[] magnitudeSpectrum, int sampleRate)
        {
            var binFrequency = sampleRate / (float)FftSize;
            double weightedFrequencySum = 0;
            double magnitudeSum = 0;

            for (var bin = 1; bin < MagnitudeSpectrumLength; bin += 1)
            {
                var magnitude = magnitudeSpectrum[bin];
                weightedFrequencySum += magnitude * bin * binFrequency;
                magnitudeSum += magnitude;
            }

            return magnitudeSum <= SilenceFloor ? 0 : (float)(weightedFrequencySum / magnitudeSum);
        }
    }

    private sealed class SpectralFluxExtractor
    {
        public float ProcessFrame(
            float[] magnitudeSpectrum,
            float[] previousMagnitudeSpectrum,
            bool hasPreviousMagnitudeSpectrum)
        {
            if (!hasPreviousMagnitudeSpectrum)
            {
                return 0;
            }

            double differenceSum = 0;
            double energySum = 0;
            for (var bin = 1; bin < MagnitudeSpectrumLength; bin += 1)
            {
                var current = magnitudeSpectrum[bin];
                var difference = Math.Max(0, current - previousMagnitudeSpectrum[bin]);
                differenceSum += difference * difference;
                energySum += current * current;
            }

            if (energySum <= SilenceFloor)
            {
                return 0;
            }

            return Math.Clamp((float)Math.Sqrt(differenceSum / energySum), 0, 1);
        }
    }

    private sealed class SpectralRolloffExtractor
    {
        private readonly float _threshold;

        public SpectralRolloffExtractor(float threshold)
        {
            _threshold = threshold;
        }

        public float ProcessFrame(float[] magnitudeSpectrum, int sampleRate)
        {
            double totalEnergy = 0;
            for (var bin = 1; bin < MagnitudeSpectrumLength; bin += 1)
            {
                totalEnergy += magnitudeSpectrum[bin];
            }

            if (totalEnergy <= SilenceFloor)
            {
                return 0;
            }

            var threshold = totalEnergy * _threshold;
            double cumulativeEnergy = 0;
            for (var bin = 1; bin < MagnitudeSpectrumLength; bin += 1)
            {
                cumulativeEnergy += magnitudeSpectrum[bin];
                if (cumulativeEnergy >= threshold)
                {
                    return bin * sampleRate / (float)FftSize;
                }
            }

            return sampleRate / 2f;
        }
    }
}
