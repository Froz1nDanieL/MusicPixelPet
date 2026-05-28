using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Hardcodet.Wpf.TaskbarNotification;
using MusicPixelPet.Wpf.Pet;
using MusicPixelPet.Wpf.Services;
using MusicPixelPet.Wpf.ViewModels;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicPetDesktop;

public partial class MainWindow : Window
{
    private readonly MediaService _mediaService = new();
    private readonly AudioAnalyzerService _audioAnalyzerService = new();
    private readonly SettingsService _settingsService = new();
    private readonly PetFrameAnimator _petFrameAnimator = new();
    private readonly DispatcherTimer _hoverLeaveTimer;
    private readonly MainViewModel _viewModel;
    private readonly TaskbarIcon _trayIcon = new();
    private SettingsWindow? _settingsWindow;
    private bool _isQuitting;

    public MainWindow()
    {
        _viewModel = new MainViewModel(_mediaService, _audioAnalyzerService, _settingsService);
        DataContext = _viewModel;
        InitializeComponent();

        _hoverLeaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _hoverLeaveTimer.Tick += (_, _) =>
        {
            _hoverLeaveTimer.Stop();
            _viewModel.IsHovered = ShellRoot.IsMouseOver || ControlBarCard.IsMouseOver;
        };

        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.CurrentAnimation))
            {
                _petFrameAnimator.SetAnimation(_viewModel.CurrentAnimation);
            }
        };

        _viewModel.OpenSettingsRequested += (_, _) => OpenSettingsWindow();
        _petFrameAnimator.FrameChanged += (_, frame) => _viewModel.PetFrame = frame;
        _petFrameAnimator.Start();
        InitializeTray();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isQuitting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _viewModel.SaveWindowBounds(Left, Top, Width, Height);
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _trayIcon.Dispose();
        _petFrameAnimator.Dispose();
        _audioAnalyzerService.Dispose();
        _mediaService.Dispose();
        base.OnClosed(e);
    }

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
    {
        Left = _viewModel.Settings.WindowBounds.X;
        Top = _viewModel.Settings.WindowBounds.Y;
        Width = _viewModel.Settings.WindowBounds.Width;
        Height = _viewModel.Settings.WindowBounds.Height;

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch
        {
            _viewModel.IsReady = true;
        }
    }

    private void InitializeTray()
    {
        _trayIcon.ToolTipText = "Music Pixel Pet";
        _trayIcon.IconSource = CreateTrayIcon();
        _trayIcon.TrayLeftMouseUp += (_, _) => ToggleWindowVisibility();
        _trayIcon.ContextMenu = CreateTrayMenu();
    }

    private ContextMenu CreateTrayMenu()
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) => RefreshTrayMenu(menu);
        RefreshTrayMenu(menu);
        return menu;
    }

    private void RefreshTrayMenu(ContextMenu menu)
    {
        menu.Items.Clear();

        menu.Items.Add(new MenuItem
        {
            Header = IsVisible ? "隐藏桌宠" : "显示桌宠",
            Command = new CommunityToolkit.Mvvm.Input.RelayCommand(ToggleWindowVisibility)
        });

        menu.Items.Add(new MenuItem
        {
            Header = _viewModel.Settings.AlwaysOnTop ? "取消置顶" : "始终置顶",
            Command = new CommunityToolkit.Mvvm.Input.RelayCommand(ToggleAlwaysOnTop)
        });

        menu.Items.Add(new MenuItem
        {
            Header = "打开设置",
            Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() =>
            {
                EnsureWindowVisible();
                OpenSettingsWindow();
            })
        });

        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem
        {
            Header = "退出",
            Command = new CommunityToolkit.Mvvm.Input.RelayCommand(Quit)
        });
    }

    private void ToggleWindowVisibility()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        EnsureWindowVisible();
    }

    private void EnsureWindowVisible()
    {
        Show();
        Activate();
    }

    private async void ToggleAlwaysOnTop()
    {
        var settings = _viewModel.Settings.Clone();
        settings.AlwaysOnTop = !settings.AlwaysOnTop;
        await _viewModel.SaveSettingsAsync(settings);
    }

    private void OpenSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            PositionSettingsWindow(_settingsWindow);
            _settingsWindow.Show();
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_viewModel.Settings)
        {
            Owner = this,
            Topmost = _viewModel.Settings.AlwaysOnTop
        };
        PositionSettingsWindow(_settingsWindow);

        _settingsWindow.Saved += async (_, settings) => await _viewModel.SaveSettingsAsync(settings);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void PositionSettingsWindow(Window settingsWindow)
    {
        const double gap = 10;
        const double settingsWidth = 340;
        const double settingsHeight = 520;

        var workArea = SystemParameters.WorkArea;
        var preferredX = Left + Width + gap;
        var fallbackX = Left - settingsWidth - gap;
        var canFitRight = preferredX + settingsWidth <= workArea.Right;
        settingsWindow.Left = Math.Clamp(canFitRight ? preferredX : fallbackX, workArea.Left, workArea.Right - settingsWidth);
        settingsWindow.Top = Math.Clamp(Top + (Height - settingsHeight) / 2, workArea.Top, workArea.Bottom - settingsHeight);
    }

    private void Quit()
    {
        _isQuitting = true;
        _settingsWindow?.Close();
        Application.Current.Shutdown();
    }

    private static ImageSource CreateTrayIcon()
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            var background = CreateBrush("#11261f");
            var body = CreateBrush("#f4f1dc");
            var mouth = CreateBrush("#e08e45");
            var accent = CreateBrush("#4cd5b5");

            context.DrawRoundedRectangle(background, null, new Rect(0, 0, 32, 32), 6, 6);
            context.DrawRectangle(body, null, new Rect(7, 8, 6, 6));
            context.DrawRectangle(body, null, new Rect(19, 8, 6, 6));
            context.DrawRectangle(accent, null, new Rect(13, 14, 6, 3));
            context.DrawRectangle(mouth, null, new Rect(10, 19, 12, 4));
        }

        group.Freeze();
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    private static SolidColorBrush CreateBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private void HoverSurface_OnMouseEnter(object sender, MouseEventArgs e)
    {
        _hoverLeaveTimer.Stop();
        _viewModel.IsHovered = true;
    }

    private void HoverSurface_OnMouseLeave(object sender, MouseEventArgs e)
    {
        _hoverLeaveTimer.Stop();
        _hoverLeaveTimer.Start();
    }

    private void Window_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && HasButtonAncestor(source))
        {
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed && e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private async void PetSurface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            await _viewModel.PlayPauseCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }

    private async void NowPlayingCard_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var delta = e.Delta > 0 ? 1 : -1;
        await _viewModel.AdjustVolumeCommand.ExecuteAsync(delta);
        e.Handled = true;
    }

    private static bool HasButtonAncestor(DependencyObject source)
    {
        while (source is not null)
        {
            if (source is ButtonBase)
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
