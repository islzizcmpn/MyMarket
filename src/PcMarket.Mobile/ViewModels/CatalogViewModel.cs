using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMarket.ApiClient;
using PcMarket.Contracts.Catalog;

namespace PcMarket.Mobile.ViewModels;

/// <summary>Product browsing: category/brand/price filters, sorting, incremental paging, and free-text
/// search. Search and filtering are mutually exclusive — a query runs the FTS endpoint, everything else
/// runs the filtered list — which matches how the API splits the two.</summary>
public partial class CatalogViewModel(CatalogApiClient catalog) : BaseViewModel, IQueryAttributable
{
    private int _page = 1;
    private int _totalPages;
    private bool _loaded;

    public ObservableCollection<ProductListItemDto> Products { get; } = [];

    public ObservableCollection<CategoryNodeDto> Categories { get; } = [];

    public ObservableCollection<BrandDto> Brands { get; } = [];

    public IReadOnlyList<ProductSort> SortOptions { get; } = Enum.GetValues<ProductSort>();

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [ObservableProperty]
    public partial CategoryNodeDto? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial BrandDto? SelectedBrand { get; set; }

    [ObservableProperty]
    public partial decimal? MinPrice { get; set; }

    [ObservableProperty]
    public partial decimal? MaxPrice { get; set; }

    /// <summary>Defaults to <see cref="ProductSort.Newest"/> — it is the zero value, and partial properties
    /// cannot carry an initializer.</summary>
    [ObservableProperty]
    public partial ProductSort Sort { get; set; }

    [ObservableProperty]
    public partial bool FiltersVisible { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    public bool HasMore => _page < _totalPages;

    public bool IsEmpty => Products.Count == 0 && !IsBusy;

    /// <summary>Entry from the home screen: either a category tap or a search. Applied before the page
    /// appears, so the first load already reflects it.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("q", out var q))
        {
            SearchText = Uri.UnescapeDataString(q?.ToString() ?? string.Empty);
            _pendingCategorySlug = null;
        }

        if (query.TryGetValue("category", out var category))
        {
            _pendingCategorySlug = Uri.UnescapeDataString(category?.ToString() ?? string.Empty);
            SearchText = null;
        }

        if (query.Count > 0)
        {
            _loaded = false;
        }
    }

    private string? _pendingCategorySlug;

    [RelayCommand]
    private Task AppearingAsync() => _loaded ? Task.CompletedTask : LoadAsync();

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async ct =>
    {
        if (Categories.Count == 0)
        {
            foreach (var category in Flatten(await catalog.GetCategoriesAsync(ct)))
            {
                Categories.Add(category);
            }

            foreach (var brand in await catalog.GetBrandsAsync(ct))
            {
                Brands.Add(brand);
            }
        }

        if (_pendingCategorySlug is not null)
        {
            SelectedCategory = Categories.FirstOrDefault(c => c.Slug == _pendingCategorySlug);
            _pendingCategorySlug = null;
        }

        _page = 1;
        var result = await FetchAsync(_page, ct);

        Products.Clear();
        foreach (var product in result.Items)
        {
            Products.Add(product);
        }

        _totalPages = result.TotalPages;
        _loaded = true;
        NotifyListState();
    });

    /// <summary>Appends the next page when the list is scrolled to its end. Guarded so the several
    /// threshold events a fast scroll produces do not each fire a request.</summary>
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || IsLoadingMore || !HasMore)
        {
            return;
        }

        IsLoadingMore = true;
        try
        {
            var result = await FetchAsync(_page + 1, CancellationToken.None);
            _page++;
            _totalPages = result.TotalPages;

            foreach (var product in result.Items)
            {
                Products.Add(product);
            }

            NotifyListState();
        }
        catch (ApiException ex)
        {
            Error = ex.Message;
        }
        catch (HttpRequestException)
        {
            // A failed "load more" leaves what is already on screen usable; stay quiet.
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    [RelayCommand]
    private Task ApplyFiltersAsync()
    {
        FiltersVisible = false;
        return LoadAsync();
    }

    [RelayCommand]
    private Task ClearFiltersAsync()
    {
        SelectedCategory = null;
        SelectedBrand = null;
        MinPrice = null;
        MaxPrice = null;
        Sort = ProductSort.Newest;
        SearchText = null;
        return LoadAsync();
    }

    [RelayCommand]
    private void ToggleFilters() => FiltersVisible = !FiltersVisible;

    [RelayCommand]
    private static Task OpenProductAsync(ProductListItemDto? product) =>
        product is null ? Task.CompletedTask : Shell.Current.GoToAsync($"product?slug={Uri.EscapeDataString(product.Slug)}");

    private Task<Contracts.Common.PagedResult<ProductListItemDto>> FetchAsync(int page, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(SearchText)
            ? catalog.GetProductsAsync(
                SelectedCategory?.Slug, SelectedBrand?.Slug, MinPrice, MaxPrice, Sort, page, PageSize, ct)
            : catalog.SearchAsync(SearchText.Trim(), page, PageSize, ct);

    private const int PageSize = 20;

    protected override void OnBusyChanged() => NotifyListState();

    private void NotifyListState()
    {
        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>The picker shows one flat list; the tree's shape is not useful for a filter dropdown.</summary>
    private static IEnumerable<CategoryNodeDto> Flatten(IEnumerable<CategoryNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}
