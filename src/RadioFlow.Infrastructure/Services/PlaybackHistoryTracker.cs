using Microsoft.Extensions.Logging;
using RadioFlow.Core.Enums;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.Infrastructure.Services;

/// <summary>
/// Oynatıcı durum olaylarını dinler (polling yok): çalmaya başlayan istasyon için geçmiş kaydı açar,
/// durdurma/değişimde süreyi yazar ve son istasyonu ayarlara kaydeder.
/// </summary>
public sealed class PlaybackHistoryTracker(
    IRadioPlayerService player,
    ILibraryService library,
    IHistoryRepository history,
    ISettingsService settings,
    ILogger<PlaybackHistoryTracker> logger,
    TimeProvider? timeProvider = null) : IPlaybackHistoryTracker
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long? _openHistoryId;
    private long _openStationId;
    private DateTimeOffset _openedAt;
    private bool _started;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        player.StateChanged += OnStateChanged;
    }

    public async Task FlushAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await CloseOpenEntryAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_started)
        {
            player.StateChanged -= OnStateChanged;
        }
    }

    private async void OnStateChanged(object? sender, PlayerStateChangedEventArgs e)
    {
        // async void yalnızca olay işleyicisi olarak kullanılır; tüm hatalar burada yakalanır.
        try
        {
            await HandleAsync(e).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dinleme geçmişi güncellenemedi.");
        }
    }

    private async Task HandleAsync(PlayerStateChangedEventArgs e)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            switch (e.NewState)
            {
                case PlayerState.Playing when e.Station is not null:
                    var station = await library.EnsurePersistedAsync(e.Station).ConfigureAwait(false);
                    if (_openHistoryId is not null && _openStationId == station.Id)
                    {
                        return; // Aynı oturum (ör. yeniden bağlanma sonrası) devam ediyor.
                    }

                    await CloseOpenEntryAsync().ConfigureAwait(false);
                    _openedAt = _time.GetUtcNow();
                    _openHistoryId = await history.StartAsync(station.Id, _openedAt).ConfigureAwait(false);
                    _openStationId = station.Id;
                    if (settings.Current.LastStationId != station.Id)
                    {
                        await settings.UpdateAsync(s => s.LastStationId = station.Id).ConfigureAwait(false);
                    }

                    break;

                case PlayerState.Stopped:
                case PlayerState.Paused:
                case PlayerState.Error:
                    await CloseOpenEntryAsync().ConfigureAwait(false);
                    break;

                case PlayerState.Connecting when e.Station is not null && _openHistoryId is not null:
                    // Farklı istasyona geçiliyor.
                    await CloseOpenEntryAsync().ConfigureAwait(false);
                    break;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task CloseOpenEntryAsync()
    {
        if (_openHistoryId is not { } id)
        {
            return;
        }

        _openHistoryId = null;
        var seconds = (int)Math.Clamp((_time.GetUtcNow() - _openedAt).TotalSeconds, 0, int.MaxValue);
        await history.EndAsync(id, seconds).ConfigureAwait(false);
    }
}
