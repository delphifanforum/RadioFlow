using System.Globalization;
using System.Text;

namespace RadioFlow.Core.Text;

/// <summary>
/// Türkçe duyarlı, büyük/küçük harf ve aksan bağımsız arama anahtarı üretir.
/// "İSTANBUL", "Istanbul", "istanbul" → "istanbul"; "Şarkı" → "sarki".
/// </summary>
public static class TextFolding
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Fold(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var lowered = value.ToLower(Turkish);
        var builder = new StringBuilder(lowered.Length);
        foreach (var ch in lowered.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(ch switch
            {
                'ı' => 'i',
                'ş' => 's',
                'ç' => 'c',
                'ğ' => 'g',
                'ö' => 'o',
                'ü' => 'u',
                _ => ch,
            });
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
