using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioFlow.App.Services;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.App.ViewModels;

/// <summary>Büyük radyo paneli: sol menü, arama, sayfalar ve alt "Now Playing" çubuğu.</summary>
public sealed partial class MainViewModel : ObservableObject, IStationActions
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IRadioCatalogService _catalog;
    private readonly ILibraryService _library;
    private readonly ILogoCache _logoCache;
    private readonly ISettingsService _settings;
    private readonly IShellService _shell;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<MainViewModel> _logger;
    private readonly Dictionary<string, CategoryPageViewModel> _categoryPages = [];
    private object? _previousPage;
    private bool _suppressNavigation;

    [ObservableProperty]
    private NavItem? _selectedNav;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    private object? _currentPage;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isAddStationOpen;

    [ObservableProperty]
    private string _newStationName = string.Empty;

    [ObservableProperty]
    private string _newStationUrl = string.Empty;

    [ObservableProperty]
    private string _newStationLogo = string.Empty;

    [ObservableProperty]
    private string _newStationTags = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAddStationError))]
    private string? _addStationError;

    public MainViewModel(
        PlayerViewModel player,
        IRadioCatalogService catalog,
        ILibraryService library,
        ILogoCache logoCache,
        ISettingsService settings,
        IShellService shell,
        IUiDispatcher dispatcher,
        ILogger<MainViewModel> logger)
    {
        Player = player;
        _catalog = catalog;
        _library = library;
        _logoCache = logoCache;
        _settings = settings;
        _shell = shell;
        _dispatcher = dispatcher;
        _logger = logger;

        NavItems =
        [
            new(NavKeys.Home, "Ana Sayfa", ""),
            new(NavKeys.All, "Tüm Radyolar", ""),
            new(NavKeys.Favorites, "Favoriler", ""),
            new(NavKeys.Recent, "Son Dinlenenler", ""),
            new(NavKeys.Countries, "Ülkelere Göre", ""),
            new(NavKeys.Genres, "Türlere Göre", ""),
        ];

        _library.FavoritesChanged += (_, _) => _dispatcher.Post(() => _ = OnFavoritesChangedAsync());
        Player.PropertyChanged += OnPlayerPropertyChanged;
    }

    public PlayerViewModel Player { get; }

    public ObservableCollection<NavItem> NavItems { get; }

    public bool CanGoBack => _previousPage is not null && CurrentPage is StationListPageViewModel { Key: var key } && key.StartsWith("cat:", StringComparison.Ordinal);

    public bool HasAddStationError => !string.IsNullOrWhiteSpace(AddStationError);

    /// <summary>Pencere ilk kez açıldığında çağrılır.</summary>
    public void EnsureInitialized()
    {
        if (CurrentPage is null)
        {
            SelectedNav = NavItems[0];
        }
    }

    public async Task PlayAsync(StationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        await Player.PlayStationAsync(item.Station);
    }

    public async Task ToggleFavoriteAsync(StationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        try
        {
            var persisted = await _library.EnsurePersistedAsync(item.Station);
            item.UpdateStation(persisted);
            item.IsFavorite = await _library.ToggleFavoriteAsync(persisted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Favori güncellenemedi.");
            _shell.ShowError("Favori güncellenemedi. Lütfen tekrar deneyin.");
        }
    }

    public async Task MoveAsync(StationItemViewModel item, int offset)
    {
        ArgumentNullException.ThrowIfNull(item);
        try
        {
            await _library.MoveFavoriteAsync(item.Station, offset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Favori sırası değiştirilemedi.");
        }
    }

    public async Task DeleteAsync(StationItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!item.CanDelete || !_shell.Confirm($"\"{item.Name}\" istasyonu silinsin mi?", "İstasyonu sil"))
        {
            return;
        }

        try
        {
            await _library.DeleteCustomStationAsync(item.Station);
            if (CurrentPage is StationListPageViewModel page)
            {
                page.Items.Remove(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Özel istasyon silinemedi.");
            _shell.ShowError("İstasyon silinemedi.");
        }
    }

    partial void OnSelectedNavChanged(NavItem? oldValue, NavItem? newValue)
    {
        if (_suppressNavigation || newValue is null)
        {
            return;
        }

        Navigate(newValue.Key);
    }

    partial void OnCurrentPageChanged(object? oldValue, object? newValue)
    {
        if (oldValue is StationListPageViewModel oldPage)
        {
            oldPage.Cancel();
        }
    }

    [RelayCommand]
    private void Search()
    {
        var text = SearchText.Trim();
        if (text.Length == 0)
        {
            Navigate(NavKeys.All);
            return;
        }

        _suppressNavigation = true;
        SelectedNav = NavItems.First(n => n.Key == NavKeys.All);
        _suppressNavigation = false;
        ShowStationPage(CreateCatalogPage(
            "search",
            $"“{text}” için sonuçlar",
            "Radio Browser kataloğunda arama",
            new StationQuery { Name = text, Limit = 100 }));
    }

    [RelayCommand]
    private void GoBack()
    {
        if (_previousPage is not null)
        {
            CurrentPage = _previousPage;
            _previousPage = null;
            OnPropertyChanged(nameof(CanGoBack));
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => CurrentPage switch
    {
        StationListPageViewModel page => page.LoadAsync(),
        CategoryPageViewModel cat => cat.RetryCommand.ExecuteAsync(null),
        _ => Task.CompletedTask,
    };

    [RelayCommand]
    private void OpenSettings() => _shell.ShowSettings();

    [RelayCommand]
    private void OpenAddStation()
    {
        NewStationName = string.Empty;
        NewStationUrl = string.Empty;
        NewStationLogo = string.Empty;
        NewStationTags = string.Empty;
        AddStationError = null;
        IsAddStationOpen = true;
    }

    [RelayCommand]
    private void CancelAddStation() => IsAddStationOpen = false;

    [RelayCommand]
    private async Task SaveCustomStationAsync()
    {
        AddStationError = null;
        if (string.IsNullOrWhiteSpace(NewStationName))
        {
            AddStationError = "İstasyon adı gerekli.";
            return;
        }

        try
        {
            var station = await _library.AddCustomStationAsync(
                NewStationName,
                NewStationUrl,
                string.IsNullOrWhiteSpace(NewStationLogo) ? null : NewStationLogo,
                string.IsNullOrWhiteSpace(NewStationTags) ? null : NewStationTags);
            IsAddStationOpen = false;
            SelectedNav = NavItems.First(n => n.Key == NavKeys.Favorites);
            await Player.PlayStationAsync(station);
        }
        catch (ArgumentException ex)
        {
            AddStationError = ex.Message.Split(" (Parameter", StringSplitOptions.None)[0];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Özel istasyon eklenemedi.");
            AddStationError = "İstasyon kaydedilemedi.";
        }
    }

    private void Navigate(string key)
    {
        _previousPage = null;
        switch (key)
        {
            case NavKeys.Home:
                var cc = _settings.Current.DefaultCountryCode;
                ShowStationPage(CreateCatalogPage(
                    NavKeys.Home,
                    "Ana Sayfa",
                    $"{CountryDisplayName(cc)} — en çok dinlenen istasyonlar",
                    new StationQuery { CountryCode = cc, Limit = 60 }));
                break;

            case NavKeys.All:
                ShowStationPage(CreateCatalogPage(
                    NavKeys.All,
                    "Tüm Radyolar",
                    "Dünya genelinde popüler istasyonlar · aramak için üstteki alanı kullanın",
                    new StationQuery { Limit = 100 }));
                break;

            case NavKeys.Favorites:
                ShowStationPage(new StationListPageViewModel(
                    NavKeys.Favorites,
                    "Favoriler",
                    "Sıralamayı oklarla değiştirin · widget'taki önceki/sonraki bu sırayı kullanır",
                    async _ => new StationLoadResult(await _library.GetFavoritesAsync()),
                    (s, fav) => CreateItem(s, fav, canReorder: true),
                    GetFavoriteKeysAsync,
                    "Henüz favori istasyonunuz yok. Bir istasyon kartındaki ☆ simgesine tıklayın veya \"İstasyon Ekle\" ile kendi yayınınızı ekleyin."));
                break;

            case NavKeys.Recent:
                ShowStationPage(new StationListPageViewModel(
                    NavKeys.Recent,
                    "Son Dinlenenler",
                    "En son dinlediğiniz istasyonlar",
                    async _ => new StationLoadResult(await _library.GetRecentAsync(50)),
                    (s, fav) => CreateItem(s, fav, canReorder: false),
                    GetFavoriteKeysAsync,
                    "Henüz dinleme geçmişi yok."));
                break;

            case NavKeys.Countries:
                ShowCategoryPage(NavKeys.Countries, () => new CategoryPageViewModel(
                    NavKeys.Countries,
                    "Ülkelere Göre",
                    "Bir ülke seçin",
                    "",
                    ct => _catalog.GetCountriesAsync(ct),
                    c => ShowSubPage(CreateCatalogPage(
                        "cat:country:" + c.Key,
                        c.DisplayName,
                        $"{c.StationCount.ToString("N0", Turkish)} istasyon",
                        new StationQuery { CountryCode = c.Key, Limit = 150 }))));
                break;

            case NavKeys.Genres:
                ShowCategoryPage(NavKeys.Genres, () => new CategoryPageViewModel(
                    NavKeys.Genres,
                    "Türlere Göre",
                    "Bir tür seçin",
                    "",
                    ct => _catalog.GetTagsAsync(250, ct),
                    c => ShowSubPage(CreateCatalogPage(
                        "cat:tag:" + c.Key,
                        Capitalize(c.DisplayName),
                        $"{c.StationCount.ToString("N0", Turkish)} istasyon",
                        new StationQuery { Tag = c.Key, Limit = 150 }))));
                break;

            case NavKeys.Settings:
                _shell.ShowSettings();
                break;
        }
    }

    private void ShowStationPage(StationListPageViewModel page)
    {
        CurrentPage = page;
        _ = LoadPageAsync(page);
    }

    private void ShowSubPage(StationListPageViewModel page)
    {
        var previous = CurrentPage;
        CurrentPage = page;
        _previousPage = previous;
        OnPropertyChanged(nameof(CanGoBack));
        _ = LoadPageAsync(page);
    }

    private void ShowCategoryPage(string key, Func<CategoryPageViewModel> factory)
    {
        if (!_categoryPages.TryGetValue(key, out var page))
        {
            page = factory();
            _categoryPages[key] = page;
        }

        CurrentPage = page;
        _ = page.LoadAsync();
    }

    private async Task LoadPageAsync(StationListPageViewModel page)
    {
        await page.LoadAsync();
        page.MarkCurrent(Player.CurrentStation);
        foreach (var item in page.Snapshot())
        {
            _ = LoadLogoAsync(item);
        }
    }

    private async Task LoadLogoAsync(StationItemViewModel item)
    {
        try
        {
            item.LogoPath = await _logoCache.GetLogoPathAsync(item.Station.LogoUrl);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Logo yüklenemedi.");
        }
    }

    private StationListPageViewModel CreateCatalogPage(string key, string title, string subtitle, StationQuery query) =>
        new(
            key,
            title,
            subtitle,
            ct => LoadFromCatalogAsync(query, ct),
            (s, fav) => CreateItem(s, fav, canReorder: false),
            GetFavoriteKeysAsync,
            "Sonuç bulunamadı.");

    private async Task<StationLoadResult> LoadFromCatalogAsync(StationQuery query, CancellationToken ct)
    {
        try
        {
            var stations = await _catalog.SearchAsync(query, ct);
            _ = CacheAsync(stations);
            return new StationLoadResult(stations);
        }
        catch (CatalogUnavailableException ex)
        {
            _logger.LogWarning(ex, "Katalog erişilemedi; önbellek kullanılıyor.");
            var cached = await _library.SearchCachedAsync(query, ct);
            return new StationLoadResult(
                cached,
                cached.Count > 0 ? "Radyo kataloğuna ulaşılamadı. Yerel önbellekteki istasyonlar gösteriliyor." : ex.Message);
        }
    }

    private async Task CacheAsync(IReadOnlyList<RadioStation> stations)
    {
        try
        {
            await _library.CacheStationsAsync(stations);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "İstasyonlar önbelleğe yazılamadı.");
        }
    }

    private StationItemViewModel CreateItem(RadioStation station, bool isFavorite, bool canReorder) =>
        new(station, isFavorite, canReorder, this);

    private async Task<HashSet<string>> GetFavoriteKeysAsync()
    {
        var favorites = await _library.GetFavoritesAsync();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var station in favorites)
        {
            keys.Add(StationListPageViewModel.KeyOf(station));
            keys.Add("id:" + station.Id);
        }

        return keys;
    }

    private async Task OnFavoritesChangedAsync()
    {
        try
        {
            if (CurrentPage is StationListPageViewModel { Key: NavKeys.Favorites } favoritesPage)
            {
                await LoadPageAsync(favoritesPage);
            }
            else if (CurrentPage is StationListPageViewModel page)
            {
                await page.RefreshFavoritesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Favori durumu yenilenemedi.");
        }
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.CurrentStation) && CurrentPage is StationListPageViewModel page)
        {
            page.MarkCurrent(Player.CurrentStation);
        }
    }

    private static string CountryDisplayName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "Dünya";
        }

        if (code.Equals("TR", StringComparison.OrdinalIgnoreCase))
        {
            return "Türkiye";
        }

        try
        {
            return new RegionInfo(code).DisplayName;
        }
        catch (ArgumentException)
        {
            return code.ToUpperInvariant();
        }
    }

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpper(value[0], Turkish) + value[1..];
}
