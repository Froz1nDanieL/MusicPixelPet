using MusicPixelPet.Wpf.Models;
using NAudio.Wave;

namespace MusicPixelPet.Wpf.Services;

public sealed class AudioAnalyzerService : IDisposable
{
    private readonly object _syncRoot = new();
    private WasapiLoopbackCapture? _capture;
    private float _rollingLevel;
    private DateTimeOffset _lastBeatAt = DateTimeOffset.MinValue;
    private bool _isRunning;

    public event EventHandler<float>? LevelChanged;
    public event EventHandler<BeatEventArgs>? BeatDetected;
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

            _isRunning = false;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        try
        {
            var level = CalculateRms(args.Buffer, args.BytesRecorded, _capture?.WaveFormat);
            _rollingLevel = _rollingLevel <= 0 ? level : (_rollingLevel * 0.92f) + (level * 0.08f);

            LevelChanged?.Invoke(this, level);

            var now = DateTimeOffset.Now;
            var isBeat = level > 0.08f
                && level > _rollingLevel * 1.75f
                && now - _lastBeatAt > TimeSpan.FromMilliseconds(220);

            if (!isBeat)
            {
                return;
            }

            _lastBeatAt = now;
            BeatDetected?.Invoke(this, new BeatEventArgs(level, now));
        }
        catch (Exception exception)
        {
            ErrorOccurred?.Invoke(this, exception.Message);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs args)
    {
        if (args.Exception is not null)
        {
            ErrorOccurred?.Invoke(this, args.Exception.Message);
        }

        Dispose();
    }

    private static float CalculateRms(byte[] buffer, int bytesRecorded, WaveFormat? format)
    {
        if (format is null || bytesRecorded <= 0)
        {
            return 0;
        }

        return format.Encoding == WaveFormatEncoding.IeeeFloat
            ? CalculateFloatRms(buffer, bytesRecorded)
            : CalculatePcmRms(buffer, bytesRecorded, format.BitsPerSample);
    }

    private static float CalculateFloatRms(byte[] buffer, int bytesRecorded)
    {
        var sampleCount = bytesRecorded / sizeof(float);
        if (sampleCount == 0)
        {
            return 0;
        }

        double sum = 0;
        for (var index = 0; index < bytesRecorded; index += sizeof(float))
        {
            var sample = BitConverter.ToSingle(buffer, index);
            sum += sample * sample;
        }

        return (float)Math.Sqrt(sum / sampleCount);
    }

    private static float CalculatePcmRms(byte[] buffer, int bytesRecorded, int bitsPerSample)
    {
        if (bitsPerSample != 16)
        {
            return 0;
        }

        var sampleCount = bytesRecorded / sizeof(short);
        if (sampleCount == 0)
        {
            return 0;
        }

        double sum = 0;
        for (var index = 0; index < bytesRecorded; index += sizeof(short))
        {
            var sample = BitConverter.ToInt16(buffer, index) / 32768f;
            sum += sample * sample;
        }

        return (float)Math.Sqrt(sum / sampleCount);
    }
}
