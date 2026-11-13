using System.Windows;
using System.Windows.Controls;
using RadioFlow.Core.Enums;

namespace RadioFlow.App.Controls;

/// <summary>
/// Oynatıcı durumunu küçük bir animasyonlu göstergeye çevirir (bkz. LiveIndicator.xaml).
/// Durum değiştiğinde yalnızca ilgili VisualState'e geçilir; gereksiz Storyboard çalışmaz.
/// </summary>
public partial class LiveIndicator : UserControl
{
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(nameof(State), typeof(PlayerState), typeof(LiveIndicator),
            new PropertyMetadata(PlayerState.Stopped, OnStateChanged));

    public LiveIndicator()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyState(State, useTransitions: false);
    }

    public PlayerState State
    {
        get => (PlayerState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LiveIndicator indicator && indicator.IsLoaded)
        {
            indicator.ApplyState((PlayerState)e.NewValue, useTransitions: true);
        }
    }

    private void ApplyState(PlayerState state, bool useTransitions)
    {
        var stateName = state switch
        {
            PlayerState.Playing => "Playing",
            PlayerState.Connecting or PlayerState.Buffering or PlayerState.Reconnecting => "Connecting",
            PlayerState.Paused => "Paused",
            PlayerState.Error => "Error",
            _ => "Stopped",
        };

        // UserControl'ün kendisi değil, VisualStateGroups'u taşıyan içerik kökü (Root Grid) hedeflenir.
        VisualStateManager.GoToElementState(Root, stateName, useTransitions);
    }
}
