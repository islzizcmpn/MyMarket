using PcMarket.Mobile.ViewModels;

namespace PcMarket.Mobile.Views;

public partial class OrderDetailPage : ContentPage
{
    private readonly OrderDetailViewModel _viewModel;

    public OrderDetailPage(OrderDetailViewModel viewModel)
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
