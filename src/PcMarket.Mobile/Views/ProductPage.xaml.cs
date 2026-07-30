using PcMarket.Mobile.ViewModels;

namespace PcMarket.Mobile.Views;

public partial class ProductPage : ContentPage
{
    private readonly ProductViewModel _viewModel;

    public ProductPage(ProductViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.AppearingCommand.Execute(null);
    }
}
