using System.Windows;
using System.Windows.Controls;

namespace RadioFlow.App.Controls;

/// <summary>İstasyon logosu; önbellekteki yerel dosyayı küçük boyutta çözer, yoksa baş harfi gösterir.</summary>
public partial class StationLogo : UserControl
{
    public static readonly DependencyProperty LogoPathProperty =
        DependencyProperty.Register(nameof(LogoPath), typeof(string), typeof(StationLogo), new PropertyMetadata(null));

    public static readonly DependencyProperty InitialProperty =
        DependencyProperty.Register(nameof(Initial), typeof(string), typeof(StationLogo), new PropertyMetadata("R"));

    public static readonly DependencyProperty InitialFontSizeProperty =
        DependencyProperty.Register(nameof(InitialFontSize), typeof(double), typeof(StationLogo), new PropertyMetadata(20d));

    public StationLogo()
    {
        InitializeComponent();
    }

    public string? LogoPath
    {
        get => (string?)GetValue(LogoPathProperty);
        set => SetValue(LogoPathProperty, value);
    }

    public string? Initial
    {
        get => (string?)GetValue(InitialProperty);
        set => SetValue(InitialProperty, value);
    }

    public double InitialFontSize
    {
        get => (double)GetValue(InitialFontSizeProperty);
        set => SetValue(InitialFontSizeProperty, value);
    }
}
