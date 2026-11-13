namespace RadioFlow.Core.Interfaces;

public interface IStreamUrlValidator
{
    /// <summary>
    /// URL'nin güvenli bir http/https adresi olup olmadığını denetler.
    /// </summary>
    /// <param name="allowPrivateHosts">Yerel ağ / loopback adreslerine izin ver (yalnızca kullanıcının elle eklediği istasyonlar için).</param>
    bool TryValidate(string? url, out Uri? uri, out string? error, bool allowPrivateHosts = false);
}
