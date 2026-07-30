using PcMarket.Mobile.ViewModels;

namespace PcMarket.Mobile.Views;

public partial class CheckoutPage : ContentPage
{
    private readonly CheckoutViewModel _viewModel;

    public CheckoutPage(CheckoutViewModel viewModel)
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
