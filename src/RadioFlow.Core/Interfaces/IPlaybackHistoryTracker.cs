namespace RadioFlow.Core.Interfaces;

/// <summary>Oynatıcı olaylarını dinleyerek dinleme geçmişini kaydeder.</summary>
public interface IPlaybackHistoryTracker : IDisposable
{
    void Start();

    /// <summary>Açık oturumu kapatır (uygulama çıkışında).</summary>
    Task FlushAsync();
}
