using PcMarket.Mobile.ViewModels;

namespace PcMarket.Mobile.Views;

public partial class AccountPage : ContentPage
{
    private readonly AccountViewModel _viewModel;

    public AccountPage(AccountViewModel viewModel)
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
