using MusicPixelPet.Wpf.Models;
using NAudio.Dsp;
using NAudio.Wave;

namespace MusicPixelPet.Wpf.Services;

public sealed class AudioAnalyzerService : IDisposable
{
    public const int FftSize = 2048;
    private const int FftExponent = 11;
    private const float SilenceFloor = 0.000001f;
    private const float BassNoiseFloor = 0.0025f;
    private const float BassBeatMultiplier = 1.5f;
    private const float SpectrumSmoothingNewWeight = 0.3f;
    private const float SpectrumSmoothingPreviousWeight = 0.7f;
    private static readonly TimeSpan BeatMinimumGap = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan BeatHistoryWindow = TimeSpan.FromSeconds(5);

    private readonly object _syncRoot = new();
    private readonly object _beatSyncRoot = new();
    private readonly float[] _sampleBuffer = new float[FftSize];
    private readonly Complex[] _fftBuffer = new Complex[FftSize];
    private readonly float[] _window = new float[FftSize];
    private readonly Queue<DateTimeOffset> _recentBeats = new();
    private readonly double[] _beatIntervals = new double[16];
    private WasapiLoopbackCapture? _capture;
    private WaveFormat? _waveFormat;
    private int _bufferIndex;
    private int _sampleRate = 44100;
    private float _rollingBass;
    private SpectrumData _smoothedSpectrum;
    private DateTimeOffset _lastBeatAt = DateTimeOffset.MinValue;
    private bool _isRunning;
    private bool _hasSmoothedSpectrum;

    public AudioAnalyzerService()
    {
        FillHammingWindow(_window);
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
        var rawSpectrum = AnalyzeSamples(_sampleBuffer, _sampleRate, _fftBuffer, _window);
        DetectBeat(rawSpectrum);

        var smoothedSpectrum = SmoothSpectrum(rawSpectrum);
        SpectrumAnalyzed?.Invoke(this, smoothedSpectrum);
        LevelChanged?.Invoke(this, smoothedSpectrum.Rms);
    }

    public static SpectrumData AnalyzeSamples(ReadOnlySpan<float> samples, int sampleRate)
    {
        var fftBuffer = new Complex[FftSize];
        var window = new float[FftSize];
        FillHammingWindow(window);
        return AnalyzeSamples(samples, sampleRate, fftBuffer, window);
    }

    private static SpectrumData AnalyzeSamples(
        ReadOnlySpan<float> samples,
        int sampleRate,
        Complex[] fftBuffer,
        float[] window)
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

        return new SpectrumData(
            Rms: (float)Math.Sqrt(rmsSum / FftSize),
            Bass: CalculateBandMagnitude(fftBuffer, sampleRate, 20, 250),
            Mid: CalculateBandMagnitude(fftBuffer, sampleRate, 250, 4000),
            High: CalculateBandMagnitude(fftBuffer, sampleRate, 4000, 20000));
    }

    private static float CalculateBandMagnitude(Complex[] fftBuffer, int sampleRate, float minFrequency, float maxFrequency)
    {
        var nyquist = sampleRate / 2f;
        var binFrequency = sampleRate / (float)FftSize;
        var maxUsableFrequency = Math.Min(maxFrequency, nyquist);
        var startBin = Math.Max(1, (int)MathF.Ceiling(minFrequency / binFrequency));
        var endBin = Math.Min(FftSize / 2 - 1, (int)MathF.Floor(maxUsableFrequency / binFrequency));

        if (endBin < startBin)
        {
            return 0;
        }

        double magnitudeSum = 0;
        var binCount = endBin - startBin + 1;
        for (var bin = startBin; bin <= endBin; bin += 1)
        {
            var real = fftBuffer[bin].X;
            var imaginary = fftBuffer[bin].Y;
            magnitudeSum += Math.Sqrt(real * real + imaginary * imaginary);
        }

        return (float)(magnitudeSum / binCount);
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
        BeatDetected?.Invoke(this, new BeatEventArgs(bass, now, bpm));
    }

    private SpectrumData SmoothSpectrum(SpectrumData next)
    {
        if (!_hasSmoothedSpectrum)
        {
            _smoothedSpectrum = next;
            _hasSmoothedSpectrum = true;
            return _smoothedSpectrum;
        }

        _smoothedSpectrum = new SpectrumData(
            Rms: LowPass(next.Rms, _smoothedSpectrum.Rms),
            Bass: LowPass(next.Bass, _smoothedSpectrum.Bass),
            Mid: LowPass(next.Mid, _smoothedSpectrum.Mid),
            High: LowPass(next.High, _smoothedSpectrum.High));

        return _smoothedSpectrum;
    }

    private static float LowPass(float next, float previous)
    {
        return next * SpectrumSmoothingNewWeight + previous * SpectrumSmoothingPreviousWeight;
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
}
