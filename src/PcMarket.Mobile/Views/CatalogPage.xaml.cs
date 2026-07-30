using PcMarket.Mobile.ViewModels;

namespace PcMarket.Mobile.Views;

public partial class CatalogPage : ContentPage
{
    private readonly CatalogViewModel _viewModel;

    public CatalogPage(CatalogViewModel viewModel)
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
