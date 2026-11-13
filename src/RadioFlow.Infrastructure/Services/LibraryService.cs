using Microsoft.Extensions.Logging;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.Infrastructure.Services;

public sealed class LibraryService(
    IStationRepository stations,
    IFavoritesRepository favorites,
    IHistoryRepository history,
    IStreamUrlValidator validator,
    ILogger<LibraryService> logger) : ILibraryService
{
    public event EventHandler? FavoritesChanged;

    public async Task<RadioStation> EnsurePersistedAsync(RadioStation station, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);
        if (station.Id != 0)
        {
            return station;
        }

        if (!string.IsNullOrEmpty(station.ExternalId))
        {
            var existing = await stations.GetByExternalIdAsync(station.ExternalId, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                // Katalogdan gelen güncel bilgileri (logo, URL) koru.
                return await stations.UpsertAsync(station with { Id = existing.Id, CreatedAt = existing.CreatedAt }, cancellationToken).ConfigureAwait(false);
            }
        }

        return await stations.UpsertAsync(station, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<RadioStation>> GetFavoritesAsync(CancellationToken cancellationToken = default) =>
        favorites.GetAllAsync(cancellationToken);

    public async Task<bool> IsFavoriteAsync(RadioStation station, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);
        var id = station.Id;
        if (id == 0 && !string.IsNullOrEmpty(station.ExternalId))
        {
            id = (await stations.GetByExternalIdAsync(station.ExternalId, cancellationToken).ConfigureAwait(false))?.Id ?? 0;
        }

        return id != 0 && await favorites.IsFavoriteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ToggleFavoriteAsync(RadioStation station, CancellationToken cancellationToken = default)
    {
        var persisted = await EnsurePersistedAsync(station, cancellationToken).ConfigureAwait(false);
        bool isFavorite;
        if (await favorites.IsFavoriteAsync(persisted.Id, cancellationToken).ConfigureAwait(false))
        {
            await favorites.RemoveAsync(persisted.Id, cancellationToken).ConfigureAwait(false);
            isFavorite = false;
        }
        else
        {
            await favorites.AddAsync(persisted.Id, cancellationToken).ConfigureAwait(false);
            isFavorite = true;
        }

        logger.LogInformation("Favori {Action}: {Station}", isFavorite ? "eklendi" : "kaldırıldı", persisted.Name);
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
        return isFavorite;
    }

    public async Task MoveFavoriteAsync(RadioStation station, int offset, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);
        var list = (await favorites.GetAllAsync(cancellationToken).ConfigureAwait(false)).Select(s => s.Id).ToList();
        var index = list.IndexOf(station.Id);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= list.Count || offset == 0)
        {
            return;
        }

        list.RemoveAt(index);
        list.Insert(target, station.Id);
        await favorites.ReorderAsync(list, cancellationToken).ConfigureAwait(false);
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<RadioStation?> GetAdjacentFavoriteAsync(RadioStation? current, int direction, CancellationToken cancellationToken = default)
    {
        var list = await favorites.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (list.Count == 0)
        {
            return null;
        }

        var index = -1;
        if (current is not null)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].IsSameStationAs(current))
                {
                    index = i;
                    break;
                }
            }
        }

        if (index < 0)
        {
            return direction >= 0 ? list[0] : list[^1];
        }

        var step = direction >= 0 ? 1 : -1;
        return list[((index + step) % list.Count + list.Count) % list.Count];
    }

    public Task<IReadOnlyList<RadioStation>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) =>
        history.GetRecentStationsAsync(limit, cancellationToken);

    public async Task<RadioStation> AddCustomStationAsync(
        string name,
        string streamUrl,
        string? logoUrl,
        string? tags,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("İstasyon adı boş olamaz.", nameof(name));
        }

        if (!validator.TryValidate(streamUrl, out var uri, out var error, allowPrivateHosts: true))
        {
            throw new ArgumentException(error ?? "Geçersiz yayın adresi.", nameof(streamUrl));
        }

        string? logo = null;
        if (!string.IsNullOrWhiteSpace(logoUrl))
        {
            if (!validator.TryValidate(logoUrl, out var logoUri, out var logoError, allowPrivateHosts: true))
            {
                throw new ArgumentException(logoError ?? "Geçersiz logo adresi.", nameof(logoUrl));
            }

            logo = logoUri!.AbsoluteUri;
        }

        var station = await stations.UpsertAsync(
            new RadioStation
            {
                Name = name.Trim(),
                StreamUrl = uri!.AbsoluteUri,
                LogoUrl = logo,
                Tags = string.IsNullOrWhiteSpace(tags) ? null : tags.Trim(),
                IsCustom = true,
            },
            cancellationToken).ConfigureAwait(false);

        // Elle eklenen istasyon, widget'tan hızlıca ulaşılabilmesi için favorilere de eklenir.
        await favorites.AddAsync(station.Id, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Özel istasyon eklendi: {Station}", station.Name);
        FavoritesChanged?.Invoke(this, EventArgs.Empty);
        return station;
    }

    public async Task<bool> DeleteCustomStationAsync(RadioStation station, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(station);
        if (!station.IsCustom || station.Id == 0)
        {
            return false;
        }

        var deleted = await stations.DeleteCustomAsync(station.Id, cancellationToken).ConfigureAwait(false);
        if (deleted)
        {
            FavoritesChanged?.Invoke(this, EventArgs.Empty);
        }

        return deleted;
    }

    public Task<RadioStation?> GetStationAsync(long id, CancellationToken cancellationToken = default) =>
        stations.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<RadioStation>> SearchCachedAsync(StationQuery query, CancellationToken cancellationToken = default) =>
        stations.SearchCachedAsync(query, cancellationToken);

    public Task CacheStationsAsync(IEnumerable<RadioStation> stationsToCache, CancellationToken cancellationToken = default) =>
        stations.UpsertManyAsync(stationsToCache, cancellationToken);
}
