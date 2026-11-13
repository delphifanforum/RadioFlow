using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using CommunityToolkit.Mvvm.Input;
using RadioFlow.App.Services;
using RadioFlow.App.ViewModels;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.App.Views;

public partial class MainWindow : Window, IForceClosable
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settings;
    private readonly IShellService _shell;
    private bool _forceClose;
    private bool _placementRestored;

    public MainWindow(MainViewModel viewModel, ISettingsService settings, IShellService shell)
    {
        _viewModel = viewModel;
        _settings = settings;
        _shell = shell;
        FocusSearchCommand = new RelayCommand(() =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        });

        DataContext = viewModel;
        InitializeComponent();
        InputBindings.Add(new KeyBinding(FocusSearchCommand, Key.F, ModifierKeys.Control));

        WindowBackdrop.Attach(this, acrylic: false, captionHeight: 48, resizable: true);
        // Mica üzerinde hafif ton; efekt yoksa opak yüzey.
        TintLayer.Opacity = WindowBackdrop.IsBackdropSupported ? 0.55 : 1.0;

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentPage))
            {
                PlayPageEnter();
            }
        };

        settings.Changed += OnSettingsChanged;
        StateChanged += (_, _) => UpdateMaximizedState();
        Loaded += (_, _) => _viewModel.EnsureInitialized();
    }

    public ICommand FocusSearchCommand { get; }

    public void SavePlacement()
    {
        if (!IsLoaded || WindowState != WindowState.Normal)
        {
            return;
        }

        var left = Left;
        var top = Top;
        var width = Width;
        var height = Height;
        _ = _settings.UpdateAsync(s =>
        {
            s.MainLeft = left;
            s.MainTop = top;
            s.MainWidth = width;
            s.MainHeight = height;
        });
    }

    public void ForceClose()
    {
        _forceClose = true;
        _settings.Changed -= OnSettingsChanged;
        Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        RestorePlacement();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_forceClose)
        {
            return;
        }

        // Büyük pencere kapatıldığında yalnızca gizlenir; yayın ve mini widget devam eder.
        e.Cancel = true;
        SavePlacement();
        Hide();
    }

    private void RestorePlacement()
    {
        if (_placementRestored)
        {
            return;
        }

        _placementRestored = true;
        var s = _settings.Current;
        Width = s.MainWidth;
        Height = s.MainHeight;
        if (s.MainLeft is { } left && s.MainTop is { } top)
        {
            var screen = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
            var bounds = new Rect(left, top, Width, Height);
            if (screen.IntersectsWith(bounds) && Rect.Intersect(screen, bounds).Width > 200)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = left;
                Top = Math.Max(screen.Top, top);
            }
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings s) => Dispatcher.InvokeAsync(() =>
    {
        if (WindowState == WindowState.Normal && (Math.Abs(Width - s.MainWidth) > 1 || Math.Abs(Height - s.MainHeight) > 1))
        {
            Width = s.MainWidth;
            Height = s.MainHeight;
        }
    });

    private void PlayPageEnter()
    {
        if (TryFindResource("PageEnter") is Storyboard storyboard)
        {
            storyboard.Begin(PageHost);
        }
    }

    private void UpdateMaximizedState()
    {
        // WindowChrome ile ekranı kaplayan pencere ekran kenarlarından taşar; kenar boşluğuyla düzelt.
        Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "" : "";
    }

    private void OnSwitchToWidgetClick(object sender, RoutedEventArgs e)
    {
        _shell.ShowWidget();
        SavePlacement();
        Hide();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
