using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PolyCode.UI.ViewModels;

namespace PolyCode.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnCodeChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.SendCodeCommand.ExecuteAsync(null);
        }
    }

    private async void OnCodeKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (e.Key == Key.Enter || e.Key == Key.Up || e.Key == Key.Down ||
                e.Key == Key.Left || e.Key == Key.Right)
            {
                await vm.UpdateCursorCommand.ExecuteAsync(null);
            }
        }
    }
}
