using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RadioFlow.App.Converters;

/// <summary>bool → Visibility. ConverterParameter="Invert" ile tersine çevrilir.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>null / boş metin → Collapsed. ConverterParameter="Invert" ile tersine çevrilir.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is string s ? !string.IsNullOrWhiteSpace(s) : value is not null;
        if (parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            hasValue = !hasValue;
        }

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Logo dosya yolunu küçük boyutta çözülmüş, dondurulmuş bir BitmapImage'a dönüştürür.</summary>
/// <remarks>Yalnızca LogoCache'in yazdığı yerel dosyalar yüklenir; hatalı görüntüde null döner (yer tutucu görünür).</remarks>
public sealed class LogoPathToImageConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, WeakReference<ImageSource>> Cache = new(StringComparer.OrdinalIgnoreCase);

    public int DecodeSize { get; set; } = 128;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var key = path + "|" + DecodeSize.ToString(CultureInfo.InvariantCulture);
        if (Cache.TryGetValue(key, out var weak) && weak.TryGetTarget(out var cached))
        {
            return cached;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad; // dosya kilidi tutulmaz
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.DecodePixelWidth = DecodeSize;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            Cache[key] = new WeakReference<ImageSource>(image);
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or InvalidOperationException or ArgumentException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Değer, parametreye eşitse true (enum seçimleri için).</summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>0–1 opaklığı yüzde metnine çevirir.</summary>
public sealed class PercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? $"%{Math.Round(d * 100).ToString(CultureInfo.InvariantCulture)}" : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
