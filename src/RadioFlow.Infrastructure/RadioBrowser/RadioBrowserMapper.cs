using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.Infrastructure.RadioBrowser;

/// <summary>Güvenilmeyen API verisini doğrulayarak alan modeline dönüştürür.</summary>
public static class RadioBrowserMapper
{
    private const int MaxNameLength = 200;
    private const int MaxFieldLength = 500;

    /// <summary>Geçerli bir yayın adresi yoksa null döner (istasyon listelenmez).</summary>
    public static RadioStation? ToStation(RadioBrowserStationDto dto, IStreamUrlValidator validator)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(validator);

        var name = Clean(dto.Name, MaxNameLength);
        if (name is null || string.IsNullOrWhiteSpace(dto.StationUuid))
        {
            return null;
        }

        Uri? stream = null;
        if (!validator.TryValidate(dto.UrlResolved, out stream, out _)
            && !validator.TryValidate(dto.Url, out stream, out _))
        {
            return null;
        }

        string? logo = null;
        if (validator.TryValidate(dto.Favicon, out var logoUri, out _))
        {
            logo = logoUri!.AbsoluteUri;
        }

        string? homepage = null;
        if (validator.TryValidate(dto.Homepage, out var homeUri, out _))
        {
            homepage = homeUri!.AbsoluteUri;
        }

        return new RadioStation
        {
            ExternalId = dto.StationUuid.Trim(),
            Name = name,
            StreamUrl = stream!.AbsoluteUri,
            Country = Clean(dto.Country, 100),
            CountryCode = Clean(dto.CountryCode, 2)?.ToUpperInvariant(),
            Language = Clean(dto.Language, 100),
            Tags = Clean(dto.Tags, MaxFieldLength),
            LogoUrl = logo,
            Homepage = homepage,
            Codec = Clean(dto.Codec, 20),
            Bitrate = Math.Max(0, dto.Bitrate),
            IsCustom = false,
        };
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = new string(value.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            return null;
        }

        return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
    }
}
