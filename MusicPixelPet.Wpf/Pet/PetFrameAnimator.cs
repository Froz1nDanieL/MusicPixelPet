using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using MusicPixelPet.Wpf.Models;

namespace MusicPixelPet.Wpf.Pet;

public sealed class PetFrameAnimator : IDisposable
{
    private const int SourceFrameSize = 32;
    private const int OutputFrameSize = 128;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<PetAnimationId, IReadOnlyList<ImageSource>> _cache = [];
    private PetAnimationId _animationId = PetAnimationId.Idle;
    private IReadOnlyList<ImageSource> _frames = [];
    private int _frameIndex;

    public PetFrameAnimator()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = GetFrameInterval(PetAnimationId.Idle)
        };
        _timer.Tick += (_, _) => AdvanceFrame();
    }

    public event EventHandler<ImageSource>? FrameChanged;

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

        _animationId = animationId;
        _timer.Interval = GetFrameInterval(animationId);
        _frames = LoadFrames(animationId);
        _frameIndex = 0;

        if (_frames.Count > 0)
        {
            FrameChanged?.Invoke(this, _frames[0]);
        }
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
        FrameChanged?.Invoke(this, _frames[_frameIndex]);
    }

    private IReadOnlyList<ImageSource> LoadFrames(PetAnimationId animationId)
    {
        if (_cache.TryGetValue(animationId, out var cachedFrames))
        {
            return cachedFrames;
        }

        var frames = TryLoadPngFrames(animationId);
        if (frames.Count == 0)
        {
            frames = LoadSvgSpriteFrames(animationId);
        }

        _cache[animationId] = frames;
        return frames;
    }

    private static IReadOnlyList<ImageSource> TryLoadPngFrames(PetAnimationId animationId)
    {
        var baseDirectory = AppContext.BaseDirectory;
        var animationName = ToAssetName(animationId);
        var frames = new List<ImageSource>();

        for (var index = 0; index < 4; index += 1)
        {
            var path = Path.Combine(baseDirectory, "Assets", "Pet", "default", $"{animationName}-{index}.png");
            if (!File.Exists(path))
            {
                frames.Clear();
                break;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            frames.Add(bitmap);
        }

        return frames;
    }

    private static IReadOnlyList<ImageSource> LoadSvgSpriteFrames(PetAnimationId animationId)
    {
        var animationName = ToAssetName(animationId);
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Pet", "default", $"{animationName}.svg");

        if (!File.Exists(path))
        {
            return CreateFallbackFrames(animationId);
        }

        var document = XDocument.Load(path);
        var root = document.Root;
        if (root is null)
        {
            return CreateFallbackFrames(animationId);
        }

        var groups = root.Elements().Where(element => element.Name.LocalName == "g").Take(4);
        var frames = new List<ImageSource>();

        foreach (var group in groups)
        {
            frames.Add(RenderFrame(group));
        }

        return frames.Count > 0 ? frames : CreateFallbackFrames(animationId);
    }

    private static ImageSource RenderFrame(XElement group)
    {
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            foreach (var rect in group.Elements().Where(element => element.Name.LocalName == "rect"))
            {
                var fill = rect.Attribute("fill")?.Value;
                if (string.IsNullOrWhiteSpace(fill) || fill.Equals("transparent", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fill));
                brush.Freeze();
                context.DrawRectangle(
                    brush,
                    null,
                    new System.Windows.Rect(
                        ReadDouble(rect, "x") / SourceFrameSize * OutputFrameSize,
                        ReadDouble(rect, "y") / SourceFrameSize * OutputFrameSize,
                        ReadDouble(rect, "width") / SourceFrameSize * OutputFrameSize,
                        ReadDouble(rect, "height") / SourceFrameSize * OutputFrameSize));
            }
        }

        var bitmap = new RenderTargetBitmap(OutputFrameSize, OutputFrameSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static IReadOnlyList<ImageSource> CreateFallbackFrames(PetAnimationId animationId)
    {
        var frames = new List<ImageSource>();
        for (var index = 0; index < 4; index += 1)
        {
            frames.Add(CreateFallbackFrame(animationId, index));
        }

        return frames;
    }

    private static ImageSource CreateFallbackFrame(PetAnimationId animationId, int index)
    {
        var mouth = animationId is PetAnimationId.Playing or PetAnimationId.Celebrating ? "#4cd5b5" : "#e08e45";
        var yOffset = animationId == PetAnimationId.Celebrating ? index % 2 * -8 : index % 2 * 4;
        var visual = new DrawingVisual();

        using (var context = visual.RenderOpen())
        {
            var body = CreateBrush("#f4f1dc");
            var eye = CreateBrush("#11261f");
            var accent = CreateBrush(mouth);

            context.DrawRectangle(body, null, new System.Windows.Rect(32, 36 + yOffset, 64, 48));
            context.DrawRectangle(body, null, new System.Windows.Rect(40, 20 + yOffset, 8, 20));
            context.DrawRectangle(body, null, new System.Windows.Rect(80, 20 + yOffset, 8, 20));
            context.DrawRectangle(eye, null, new System.Windows.Rect(42, 48 + yOffset, 12, 12));
            context.DrawRectangle(eye, null, new System.Windows.Rect(74, 48 + yOffset, 12, 12));
            context.DrawRectangle(accent, null, new System.Windows.Rect(56, 68 + yOffset, 16, 8));
            context.DrawRectangle(body, null, new System.Windows.Rect(40, 84 + yOffset, 12, 24));
            context.DrawRectangle(body, null, new System.Windows.Rect(76, 84 + yOffset, 12, 24));
        }

        var bitmap = new RenderTargetBitmap(OutputFrameSize, OutputFrameSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static double ReadDouble(XElement element, string attributeName)
    {
        return double.Parse(element.Attribute(attributeName)?.Value ?? "0", CultureInfo.InvariantCulture);
    }

    private static TimeSpan GetFrameInterval(PetAnimationId animationId)
    {
        return animationId switch
        {
            PetAnimationId.Celebrating => TimeSpan.FromMilliseconds(150),
            PetAnimationId.Playing => TimeSpan.FromMilliseconds(225),
            PetAnimationId.Paused => TimeSpan.FromMilliseconds(350),
            PetAnimationId.Sleeping => TimeSpan.FromMilliseconds(400),
            _ => TimeSpan.FromMilliseconds(300)
        };
    }

    private static string ToAssetName(PetAnimationId animationId)
    {
        return animationId switch
        {
            PetAnimationId.Playing => "playing",
            PetAnimationId.Paused => "paused",
            PetAnimationId.Sleeping => "sleeping",
            PetAnimationId.Celebrating => "celebrating",
            _ => "idle"
        };
    }
}
