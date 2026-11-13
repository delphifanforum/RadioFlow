using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using RadioFlow.Core.Models;

namespace RadioFlow.App.ViewModels;

/// <summary>Sonuç kaynağı (katalog veya önbellek) ve kullanıcıya gösterilecek bilgi mesajı.</summary>
public sealed record StationLoadResult(IReadOnlyList<RadioStation> Stations, string? Notice = null);

/// <summary>İstasyon kartlarının listelendiği sayfa (Ana Sayfa, Tüm Radyolar, Favoriler, Geçmiş, ülke/tür sonuçları).</summary>
public sealed partial class StationListPageViewModel : ObservableObject
{
    private readonly Func<CancellationToken, Task<StationLoadResult>> _loader;
    private readonly Func<RadioStation, bool, StationItemViewModel> _itemFactory;
    private readonly Func<Task<HashSet<string>>> _favoriteKeys;
    private CancellationTokenSource? _loadCts;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _message;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    private string? _notice;

    public StationListPageViewModel(
        string key,
        string title,
        string subtitle,
        Func<CancellationToken, Task<StationLoadResult>> loader,
        Func<RadioStation, bool, StationItemViewModel> itemFactory,
        Func<Task<HashSet<string>>> favoriteKeys,
        string emptyMessage)
    {
        Key = key;
        Title = title;
        Subtitle = subtitle;
        _loader = loader;
        _itemFactory = itemFactory;
        _favoriteKeys = favoriteKeys;
        EmptyMessage = emptyMessage;
    }

    public string Key { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string EmptyMessage { get; }

    public ObservableCollection<StationItemViewModel> Items { get; } = [];

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);

    /// <summary>Yerel veritabanı kimliği veya katalog kimliğinden kararlı anahtar.</summary>
    public static string KeyOf(RadioStation station) =>
        !string.IsNullOrEmpty(station.ExternalId) ? "ext:" + station.ExternalId : "id:" + station.Id;

    public async Task LoadAsync()
    {
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        IsLoading = true;
        Message = null;
        Notice = null;
        try
        {
            var result = await _loader(cts.Token);
            var favorites = await _favoriteKeys();
            if (cts.IsCancellationRequested)
            {
                return;
            }

            Items.Clear();
            foreach (var station in result.Stations)
            {
                Items.Add(_itemFactory(station, favorites.Contains(KeyOf(station)) || (station.Id != 0 && favorites.Contains("id:" + station.Id))));
            }

            Notice = result.Notice;
            Message = Items.Count == 0 ? EmptyMessage : null;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Daha yeni bir yükleme başladı.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Items.Clear();
            Message = "İstasyonlar yüklenemedi. Lütfen tekrar deneyin.";
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                IsLoading = false;
            }
        }
    }

    public async Task RefreshFavoritesAsync()
    {
        var favorites = await _favoriteKeys();
        foreach (var item in Items)
        {
            item.IsFavorite = favorites.Contains(KeyOf(item.Station)) || (item.Station.Id != 0 && favorites.Contains("id:" + item.Station.Id));
        }
    }

    public void MarkCurrent(RadioStation? current)
    {
        foreach (var item in Items)
        {
            item.IsCurrent = item.Station.IsSameStationAs(current);
        }
    }

    public void Cancel() => _loadCts?.Cancel();

    public IEnumerable<StationItemViewModel> Snapshot() => Items.ToList();
}
