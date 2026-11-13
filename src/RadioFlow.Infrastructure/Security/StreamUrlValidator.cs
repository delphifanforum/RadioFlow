using System.Net;
using System.Net.Sockets;
using RadioFlow.Core.Constants;
using RadioFlow.Core.Interfaces;

namespace RadioFlow.Infrastructure.Security;

/// <summary>
/// Radyo kataloğundan veya kullanıcıdan gelen URL'leri güvenilmeyen veri olarak denetler.
/// Yalnızca mutlak http/https adreslerine izin verilir; file:, javascript:, data:, smb:, ftp: vb. reddedilir.
/// Katalog verisinde loopback / özel ağ adresleri de reddedilir (yerel ağa yönelik istekleri önlemek için).
/// </summary>
public sealed class StreamUrlValidator : IStreamUrlValidator
{
    public bool TryValidate(string? url, out Uri? uri, out string? error, bool allowPrivateHosts = false)
    {
        uri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            error = "Yayın adresi boş olamaz.";
            return false;
        }

        var trimmed = url.Trim();
        if (trimmed.Length > AppConstants.MaxUrlLength)
        {
            error = "Yayın adresi çok uzun.";
            return false;
        }

        if (trimmed.Any(char.IsControl))
        {
            error = "Yayın adresi geçersiz karakterler içeriyor.";
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed))
        {
            error = "Yayın adresi geçerli bir URL değil.";
            return false;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            error = "Yalnızca http:// ve https:// ile başlayan yayın adresleri desteklenir.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(parsed.Host) || parsed.IsUnc || parsed.IsFile)
        {
            error = "Yayın adresinde sunucu adı bulunamadı.";
            return false;
        }

        if (!allowPrivateHosts && IsPrivateOrLocalHost(parsed))
        {
            error = "Yerel ağ veya bu bilgisayara ait adresler katalog istasyonlarında kabul edilmez.";
            return false;
        }

        uri = parsed;
        return true;
    }

    internal static bool IsPrivateOrLocalHost(Uri uri)
    {
        var host = uri.IdnHost.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (uri.HostNameType is not (UriHostNameType.IPv4 or UriHostNameType.IPv6))
        {
            return false;
        }

        if (!IPAddress.TryParse(host.Trim('[', ']'), out var address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                || b[0] == 0;
        }

        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal;
    }
}
