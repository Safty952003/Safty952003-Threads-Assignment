namespace OrderProcessing.Phase4;

public sealed record ShippingQuote(
    string Carrier,
    decimal Price,
    long ElapsedMs)
{
    public IReadOnlyList<string> CancelledCarriers { get; init; }
        = Array.Empty<string>();
}