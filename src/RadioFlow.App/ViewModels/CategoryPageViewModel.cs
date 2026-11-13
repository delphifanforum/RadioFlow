using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;
using RadioFlow.Core.Text;

namespace RadioFlow.App.ViewModels;

/// <summary>Ülkeler veya türler (etiketler) listesi.</summary>
public sealed partial class CategoryPageViewModel : ObservableObject
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<CatalogCategory>>> _loader;
    private readonly Action<CatalogCategory> _onSelected;
    private IReadOnlyList<CatalogCategory> _all = [];
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _message;

    [ObservableProperty]
    private string _filter = string.Empty;

    public CategoryPageViewModel(
        string key,
        string title,
        string subtitle,
        string glyph,
        Func<CancellationToken, Task<IReadOnlyList<CatalogCategory>>> loader,
        Action<CatalogCategory> onSelected)
    {
        Key = key;
        Title = title;
        Subtitle = subtitle;
        Glyph = glyph;
        _loader = loader;
        _onSelected = onSelected;
    }

    public string Key { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string Glyph { get; }

    public ObservableCollection<CatalogCategory> Items { get; } = [];

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public async Task LoadAsync()
    {
        if (_all.Count > 0)
        {
            ApplyFilter();
            return;
        }

        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        IsLoading = true;
        Message = null;
        try
        {
            _all = await _loader(cts.Token);
            ApplyFilter();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (CatalogUnavailableException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Message = "Liste yüklenemedi. Lütfen tekrar deneyin.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void Select(CatalogCategory? category)
    {
        if (category is not null)
        {
            _onSelected(category);
        }
    }

    [RelayCommand]
    private Task RetryAsync()
    {
        _all = [];
        return LoadAsync();
    }

    private void ApplyFilter()
    {
        var folded = TextFolding.Fold(Filter.Trim());
        Items.Clear();
        foreach (var item in _all.Where(c => folded.Length == 0 || TextFolding.Fold(c.DisplayName).Contains(folded, StringComparison.Ordinal)))
        {
            Items.Add(item);
        }

        if (_all.Count > 0)
        {
            Message = Items.Count == 0 ? "Eşleşen sonuç yok." : null;
        }
    }
}
