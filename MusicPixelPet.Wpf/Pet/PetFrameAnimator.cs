using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MusicPixelPet.Wpf.Models;

namespace MusicPixelPet.Wpf.Pet;

public sealed class PetFrameAnimator : IDisposable
{
    private const int CellSize = 32;
    private const string SpriteSheetFileName = "Cat Sprite Sheet.png";

    private static readonly IReadOnlyDictionary<PetAnimationId, SpriteClip> Clips =
        new Dictionary<PetAnimationId, SpriteClip>
        {
            [PetAnimationId.Idle] = new(Row: 0, Column: 0, FrameCount: 4, FrameInterval: TimeSpan.FromMilliseconds(300)),
            [PetAnimationId.Paused] = new(Row: 1, Column: 0, FrameCount: 4, FrameInterval: TimeSpan.FromMilliseconds(360)),
            [PetAnimationId.Sleeping] = new(Row: 2, Column: 0, FrameCount: 4, FrameInterval: TimeSpan.FromMilliseconds(420)),
            [PetAnimationId.Playing] = new(Row: 4, Column: 0, FrameCount: 8, FrameInterval: TimeSpan.FromMilliseconds(180)),
            [PetAnimationId.Celebrating] = new(Row: 8, Column: 0, FrameCount: 8, FrameInterval: TimeSpan.FromMilliseconds(120))
        };

    private readonly DispatcherTimer _timer;
    private readonly Dictionary<PetAnimationId, IReadOnlyList<ImageSource>> _cache = [];
    private readonly BitmapSource _spriteSheet;
    private PetAnimationId _animationId = PetAnimationId.Idle;
    private IReadOnlyList<ImageSource> _frames = [];
    private int _frameIndex;

    public PetFrameAnimator()
    {
        _spriteSheet = LoadSpriteSheet();
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = Clips[PetAnimationId.Idle].FrameInterval
        };
        _timer.Tick += (_, _) => AdvanceFrame();
    }

    public event EventHandler<ImageSource>? FrameChanged;

    public ImageSource? CurrentFrame { get; private set; }

    public void Start()
    {
        SetAnimation(PetAnimationId.Idle);
        _timer.Start();
    }

    public void SetAnimation(PetAnimationId animationId)
    {
        if (_animationId == animationId && _frames.Count > 0)
        {
            return;
        }

        var clip = Clips.GetValueOrDefault(animationId, Clips[PetAnimationId.Idle]);
        _animationId = animationId;
        _timer.Interval = clip.FrameInterval;
        _frames = LoadFrames(animationId, clip);
        _frameIndex = 0;
        PublishFrame();
    }

    public void Dispose()
    {
        _timer.Stop();
    }

    private void AdvanceFrame()
    {
        if (_frames.Count == 0)
        {
            return;
        }

        _frameIndex = (_frameIndex + 1) % _frames.Count;
        PublishFrame();
    }

    private void PublishFrame()
    {
        if (_frames.Count == 0)
        {
            return;
        }

        CurrentFrame = _frames[_frameIndex];
        FrameChanged?.Invoke(this, CurrentFrame);
    }

    private IReadOnlyList<ImageSource> LoadFrames(PetAnimationId animationId, SpriteClip clip)
    {
        if (_cache.TryGetValue(animationId, out var cachedFrames))
        {
            return cachedFrames;
        }

        var frames = CropFrames(_spriteSheet, clip);
        _cache[animationId] = frames;
        return frames;
    }

    private static BitmapSource LoadSpriteSheet()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Pet", SpriteSheetFileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Pet sprite sheet was not copied to the application output.", path);
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static IReadOnlyList<ImageSource> CropFrames(BitmapSource spriteSheet, SpriteClip clip)
    {
        var frames = new List<ImageSource>(clip.FrameCount);

        for (var index = 0; index < clip.FrameCount; index += 1)
        {
            var x = (clip.Column + index) * CellSize;
            var y = clip.Row * CellSize;
            if (x + CellSize > spriteSheet.PixelWidth || y + CellSize > spriteSheet.PixelHeight)
            {
                break;
            }

            var frame = new CroppedBitmap(spriteSheet, new Int32Rect(x, y, CellSize, CellSize));
            frame.Freeze();
            frames.Add(frame);
        }

        if (frames.Count == 0)
        {
            throw new InvalidOperationException("Pet sprite clip is outside the sprite sheet bounds.");
        }

        return frames;
    }

    private sealed record SpriteClip(int Row, int Column, int FrameCount, TimeSpan FrameInterval);
}
