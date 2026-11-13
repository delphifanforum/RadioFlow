using RadioFlow.Core.Models;

namespace RadioFlow.Core.Interfaces;

/// <summary>
/// Düşük seviyeli ses motoru soyutlaması (LibVLC). Durum makinesi, yeniden bağlanma ve iptal
/// mantığı <see cref="IRadioPlayerService"/> içindedir; böylece testler gerçek yayın açmadan çalışır.
/// </summary>
public interface IAudioEngine : IDisposable
{
    /// <summary>Motor olayları; motorun kendi iş parçacığında tetiklenebilir. İşleyiciler motoru geri çağırmamalıdır.</summary>
    event EventHandler<AudioEngineEvent>? EngineEvent;

    /// <summary>Yeni yayını başlatır. Bu çağrıyla ilişkili tüm olaylar <paramref name="generation"/> ile etiketlenir.</summary>
    void Play(Uri streamUri, long generation);

    void Stop();

    /// <summary>Duraklatmayı dener; yayın duraklatmayı desteklemiyorsa false döner.</summary>
    bool TryPause();

    void Resume();

    int Volume { get; set; }

    bool IsMuted { get; set; }
}
