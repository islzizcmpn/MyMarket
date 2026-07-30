using PcMarket.Mobile.ViewModels;

namespace PcMarket.Mobile.Views;

public partial class CartPage : ContentPage
{
    private readonly CartViewModel _viewModel;

    public CartPage(CartViewModel viewModel)
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
