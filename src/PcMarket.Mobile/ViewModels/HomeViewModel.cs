using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMarket.ApiClient;
using PcMarket.Contracts.Catalog;

namespace PcMarket.Mobile.ViewModels;

/// <summary>Landing screen: the top-level categories plus the newest products.</summary>
public partial class HomeViewModel(CatalogApiClient catalog) : BaseViewModel
{
    public ObservableCollection<CategoryNodeDto> Categories { get; } = [];

    public ObservableCollection<ProductListItemDto> NewArrivals { get; } = [];

    [ObservableProperty]
    public partial string? SearchText { get; set; }

    [RelayCommand]
    private Task AppearingAsync() => IsStale ? LoadAsync() : Task.CompletedTask;

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async ct =>
    {
        var categories = await catalog.GetCategoriesAsync(ct);
        var newest = await catalog.GetProductsAsync(sort: ProductSort.Newest, page: 1, pageSize: 10, cancellationToken: ct);

        Categories.Clear();
        foreach (var category in categories)
        {
            Categories.Add(category);
        }

        NewArrivals.Clear();
        foreach (var product in newest.Items)
        {
            NewArrivals.Add(product);
        }

        MarkLoaded();
    });

    [RelayCommand]
    private static Task OpenProductAsync(ProductListItemDto? product) =>
        product is null ? Task.CompletedTask : Shell.Current.GoToAsync($"product?slug={Uri.EscapeDataString(product.Slug)}");

    [RelayCommand]
    private static Task OpenCategoryAsync(CategoryNodeDto? category) =>
        category is null ? Task.CompletedTask : Shell.Current.GoToAsync($"//catalog?category={Uri.EscapeDataString(category.Slug)}");

    [RelayCommand]
    private Task SearchAsync() =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Task.CompletedTask
            : Shell.Current.GoToAsync($"//catalog?q={Uri.EscapeDataString(SearchText.Trim())}");
}
