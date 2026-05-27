using System.Windows;
using System.Windows.Input;
using MusicPixelPet.Wpf.Models;
using MusicPixelPet.Wpf.ViewModels;

namespace MusicPetDesktop;

public partial class SettingsWindow : Window
{
    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        var viewModel = new SettingsViewModel(settings);
        viewModel.SaveRequested += (_, nextSettings) =>
        {
            Saved?.Invoke(this, nextSettings);
            Close();
        };
        viewModel.CloseRequested += (_, _) => Close();
        DataContext = viewModel;
    }

    public event EventHandler<AppSettings>? Saved;

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
