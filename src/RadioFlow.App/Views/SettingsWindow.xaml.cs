using System;
using System.Windows;
using System.Windows.Input;
using RadioFlow.App.Services;
using RadioFlow.App.ViewModels;

namespace RadioFlow.App.Views;

public partial class SettingsWindow : Window, IForceClosable
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        WindowBackdrop.Attach(this, acrylic: false, captionHeight: 44, resizable: true);
        TintLayer.Opacity = WindowBackdrop.IsBackdropSupported ? 0.6 : 1.0;

        viewModel.CloseRequested += OnCloseRequested;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                _viewModel.CancelCommand.Execute(null);
            }
        };
    }

    public void ForceClose() => Close();

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        base.OnClosed(e);
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();
}
