namespace PcMarket.Web.Services;

/// <summary>Holds the current cart item count for the header badge; raised when it changes.</summary>
public sealed class CartState
{
    public int Count { get; private set; }

    public event Action? Changed;

    public void Set(int count)
    {
        Count = count;
        Changed?.Invoke();
    }
}
