using PcMarket.Mobile.ViewModels;

namespace PcMarket.Mobile.Views;

public partial class OtpPage : ContentPage
{
    public OtpPage(OtpViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
