using PcMarket.Mobile.ViewModels;

namespace PcMarket.Mobile.Views;

public partial class OrdersPage : ContentPage
{
    private readonly OrdersViewModel _viewModel;

    public OrdersPage(OrdersViewModel viewModel)
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
