using System.Windows.Input;
using PcMarket.Contracts.Catalog;

namespace PcMarket.Mobile.Views;

/// <summary>
/// The shared product card. Bind <see cref="Product"/> and the card renders itself; the host owns
/// navigation, so it also owns which gesture opens the product.
/// </summary>
/// <remarks>
/// A <c>DataTemplate</c> cannot be shared across differently typed collections, which is why the card
/// is a view with a bindable property rather than a template resource: any template on any screen can
/// host this one view, and the card's look stays defined in exactly one place.
/// </remarks>
public partial class ProductCardView : ContentView
{
    public static readonly BindableProperty ProductProperty = BindableProperty.Create(
        nameof(Product),
        typeof(ProductListItemDto),
        typeof(ProductCardView),
        propertyChanged: OnProductChanged);

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command),
        typeof(ICommand),
        typeof(ProductCardView));

    public ProductCardView()
    {
        InitializeComponent();

        // One invisible Button over the whole card is the entire input surface: it reports the press
        // and the release, which drive the lift, and the click, which runs the host's command. They
        // therefore cannot disagree about what the finger is doing, and nothing here depends on how
        // Android chooses to dispatch touch between nested views.
        Surface.Pressed += OnPressed;
        Surface.Released += OnReleased;
        Surface.Clicked += OnClicked;
    }

    /// <summary>The product the card shows. Null renders the empty card rather than throwing.</summary>
    public ProductListItemDto? Product
    {
        get => (ProductListItemDto?)GetValue(ProductProperty);
        set => SetValue(ProductProperty, value);
    }

    /// <summary>Run when the card is tapped, with <see cref="Product"/> as the parameter. The card
    /// does not decide what a tap means: every screen hosting it already owns its own navigation.</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    private void OnPressed(object? sender, EventArgs e) => Lift.Raise();

    private void OnReleased(object? sender, EventArgs e) => Lift.Release();

    private void OnClicked(object? sender, EventArgs e)
    {
        // Released has already started the settle, so the card is on its way down while the product
        // page pushes in. Navigation never waits on the animation.
        if (Command is { } command && command.CanExecute(Product))
        {
            command.Execute(Product);
        }
    }

    /// <summary>
    /// Drives the card's contents off the property instead of the inherited binding context. Inside a
    /// <c>DataTemplate</c> the two are the same object, but binding to the property is what lets the
    /// card also be placed on a page directly, with the product supplied from anywhere.
    /// </summary>
    private static void OnProductChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ProductCardView card)
        {
            card.Root.BindingContext = newValue;
        }
    }
}
