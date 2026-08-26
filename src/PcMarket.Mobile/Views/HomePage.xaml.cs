using PcMarket.Mobile.Services;
using PcMarket.Mobile.ViewModels;

namespace PcMarket.Mobile.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // The hero's path is fixed, so it is assigned rather than bound. Verified on device: bound
        // through an x:Static string source it was never even requested, and the hero silently kept
        // the gradient panel that is meant to be its failure state.
        HeroImage.Source = Artwork.Source(Artwork.Banner);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.AppearingCommand.Execute(null);
    }
}
