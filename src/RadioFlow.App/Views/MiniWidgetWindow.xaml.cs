using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using RadioFlow.App.Services;
using RadioFlow.App.ViewModels;
using RadioFlow.Core.Interfaces;

namespace RadioFlow.App.Views;

public partial class MiniWidgetWindow : Window, IForceClosable
{
    private const double EdgeMargin = 24;
    private readonly WidgetViewModel _viewModel;
    private readonly ISettingsService _settings;
    private bool _forceClose;

    public MiniWidgetWindow(WidgetViewModel viewModel, ISettingsService settings)
    {
        _viewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;
        InitializeComponent();

        WindowBackdrop.Attach(this, acrylic: true, captionHeight: 0, resizable: false);
        RestorePlacement();

        Loaded += (_, _) => EnsureOnScreen();
        SizeChanged += (_, _) => EnsureOnScreen();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void SavePlacement()
    {
        if (IsLoaded && WindowState == WindowState.Normal && !double.IsNaN(Left) && !double.IsNaN(Top))
        {
            _ = _viewModel.SavePositionAsync(Left, Top);
        }
    }

    public void ForceClose()
    {
        _forceClose = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        Close();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.Handled || _viewModel.IsLocked || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // Fare bırakılmışsa DragMove istisna verebilir.
            return;
        }

        EnsureOnScreen();
        SavePlacement();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_forceClose)
        {
            return;
        }

        // Kapatma (Alt+F4 dahil) ayara göre tepsiye küçültür; ses kesilmez.
        e.Cancel = true;
        SavePlacement();
        _viewModel.CloseCommand.Execute(null);
    }

    private void RestorePlacement()
    {
        var s = _settings.Current;
        if (s.WidgetLeft is { } left && s.WidgetTop is { } top)
        {
            Left = left;
            Top = top;
        }
        else
        {
            MoveToDefaultPosition();
        }
    }

    private void MoveToDefaultPosition()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - _viewModel.WidgetWidth - EdgeMargin;
        Top = area.Bottom - _viewModel.WidgetHeight - EdgeMargin;
    }

    /// <summary>Pencere ekran dışına taşmışsa (ör. monitör çıkarıldı) görünür alana geri alır.</summary>
    private void EnsureOnScreen()
    {
        var width = ActualWidth > 0 ? ActualWidth : _viewModel.WidgetWidth;
        var height = ActualHeight > 0 ? ActualHeight : _viewModel.WidgetHeight;
        var screen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        var window = new Rect(Left, Top, width, height);
        var visible = Rect.Intersect(screen, window);

        if (visible.IsEmpty || visible.Width < width * 0.5 || visible.Height < height * 0.5)
        {
            MoveToDefaultPosition();
            return;
        }

        Left = Math.Clamp(Left, screen.Left, Math.Max(screen.Left, screen.Right - width));
        Top = Math.Clamp(Top, screen.Top, Math.Max(screen.Top, screen.Bottom - height));
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.InvokeAsync(EnsureOnScreen);

    /// <summary>"Kontrolleri otomatik gizle" açıkken ikincil düğmeleri belirir; kapalıyken hep görünürler (bkz. ShowSecondaryControls).</summary>
    private void OnRootMouseEnter(object sender, MouseEventArgs e) => _viewModel.OnPointerEnter();

    private void OnRootMouseLeave(object sender, MouseEventArgs e) => _viewModel.OnPointerLeave();
}
