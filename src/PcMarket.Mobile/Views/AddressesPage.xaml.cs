using PcMarket.Mobile.ViewModels;

namespace PcMarket.Mobile.Views;

public partial class AddressesPage : ContentPage
{
    private readonly AddressesViewModel _viewModel;

    public AddressesPage(AddressesViewModel viewModel)
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
