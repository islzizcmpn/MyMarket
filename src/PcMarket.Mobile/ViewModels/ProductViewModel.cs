using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMarket.ApiClient;
using PcMarket.Contracts.Catalog;
using PcMarket.Mobile.Services;

namespace PcMarket.Mobile.ViewModels;

/// <summary>One row of the specifications table. A named type rather than a KeyValuePair so the XAML can
/// declare a compiled binding type for it.</summary>
public sealed record SpecRow(string Key, string Value);

/// <summary>Product detail with variant selection and add-to-cart.</summary>
public partial class ProductViewModel(CatalogApiClient catalog, StoreCart cart) : BaseViewModel, IQueryAttributable
{
    private string? _slug;

    [ObservableProperty]
    public partial ProductDetailDto? Product { get; set; }

    [ObservableProperty]
    public partial ProductVariantDto? SelectedVariant { get; set; }

    [ObservableProperty]
    public partial string? AddedMessage { get; set; }

    public bool HasProduct => Product is not null;

    public IReadOnlyList<SpecRow> Specs =>
        Product?.Specs.Select(spec => new SpecRow(spec.Key, spec.Value)).ToList() ?? [];

    public string? PrimaryImageUrl =>
        Product?.Images.FirstOrDefault(i => i.IsPrimary)?.Url ?? Product?.Images.FirstOrDefault()?.Url;

    public bool CanAddToCart => SelectedVariant is { StockQty: > 0 } && !IsBusy;

    public string StockLabel => SelectedVariant is null
        ? string.Empty
        : SelectedVariant.StockQty > 0 ? $"{SelectedVariant.StockQty} in stock" : "Out of stock";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("slug", out var slug))
        {
            _slug = Uri.UnescapeDataString(slug?.ToString() ?? string.Empty);
            Product = null;
        }
    }

    [RelayCommand]
    private Task AppearingAsync() => Product is null ? LoadAsync() : Task.CompletedTask;

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async ct =>
    {
        if (string.IsNullOrEmpty(_slug))
        {
            return;
        }

        Product = await catalog.GetProductAsync(_slug, ct);
        if (Product is null)
        {
            Error = "This product is no longer available.";
            return;
        }

        // Default to the first variant a customer can actually buy.
        SelectedVariant = Product.Variants.FirstOrDefault(v => v.StockQty > 0) ?? Product.Variants.FirstOrDefault();
    });

    [RelayCommand]
    private Task AddToCartAsync() => RunAsync(async ct =>
    {
        if (SelectedVariant is null)
        {
            return;
        }

        await cart.AddAsync(SelectedVariant.Id, 1, ct);
        AddedMessage = "Added to cart.";
    });

    [RelayCommand]
    private static Task GoToCartAsync() => Shell.Current.GoToAsync("//cart");

    protected override void OnBusyChanged() => OnPropertyChanged(nameof(CanAddToCart));

    partial void OnProductChanged(ProductDetailDto? value)
    {
        OnPropertyChanged(nameof(HasProduct));
        OnPropertyChanged(nameof(Specs));
        OnPropertyChanged(nameof(PrimaryImageUrl));
    }

    partial void OnSelectedVariantChanged(ProductVariantDto? value)
    {
        AddedMessage = null;
        OnPropertyChanged(nameof(CanAddToCart));
        OnPropertyChanged(nameof(StockLabel));
    }
}
