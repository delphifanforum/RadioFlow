using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadioFlow.Core.Models;

namespace RadioFlow.App.ViewModels;

/// <summary>İstasyon kartı eylemleri (MainViewModel tarafından uygulanır).</summary>
public interface IStationActions
{
    Task PlayAsync(StationItemViewModel item);

    Task ToggleFavoriteAsync(StationItemViewModel item);

    Task MoveAsync(StationItemViewModel item, int offset);

    Task DeleteAsync(StationItemViewModel item);
}

public sealed partial class StationItemViewModel : ObservableObject
{
    private readonly IStationActions _actions;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteGlyph))]
    [NotifyPropertyChangedFor(nameof(FavoriteToolTip))]
    private bool _isFavorite;

    [ObservableProperty]
    private string? _logoPath;

    [ObservableProperty]
    private bool _isCurrent;

    public StationItemViewModel(RadioStation station, bool isFavorite, bool canReorder, IStationActions actions)
    {
        Station = station;
        _isFavorite = isFavorite;
        CanReorder = canReorder;
        _actions = actions;
    }

    public RadioStation Station { get; private set; }

    public string Name => Station.Name;

    public string Initial => string.IsNullOrWhiteSpace(Station.Name) ? "?" : Station.Name.Trim()[..1].ToUpperInvariant();

    public string Country => Station.Country ?? (Station.IsCustom ? "Özel istasyon" : "—");

    public string Genre => Station.PrimaryGenre ?? "Genel";

    public string TechInfo
    {
        get
        {
            var codec = string.IsNullOrWhiteSpace(Station.Codec) ? null : Station.Codec;
            var bitrate = Station.Bitrate > 0 ? $"{Station.Bitrate} kbps" : null;
            return (codec, bitrate) switch
            {
                (null, null) => string.Empty,
                (not null, null) => codec,
                (null, not null) => bitrate,
                _ => $"{codec} · {bitrate}",
            };
        }
    }

    public bool CanReorder { get; }

    public bool CanDelete => Station.IsCustom;

    public string FavoriteGlyph => IsFavorite ? "" : "";

    public string FavoriteToolTip => IsFavorite ? "Favorilerden çıkar" : "Favorilere ekle";

    /// <summary>Kaydedildikten sonra kalıcı kimlik içeren istasyonla günceller.</summary>
    public void UpdateStation(RadioStation station) => Station = station;

    [RelayCommand]
    private Task PlayAsync() => _actions.PlayAsync(this);

    [RelayCommand]
    private Task ToggleFavoriteAsync() => _actions.ToggleFavoriteAsync(this);

    [RelayCommand]
    private Task MoveUpAsync() => _actions.MoveAsync(this, -1);

    [RelayCommand]
    private Task MoveDownAsync() => _actions.MoveAsync(this, +1);

    [RelayCommand]
    private Task DeleteAsync() => _actions.DeleteAsync(this);
}
