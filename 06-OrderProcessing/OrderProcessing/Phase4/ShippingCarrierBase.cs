using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public abstract class ShippingCarrierBase : IShippingCarrier
{
    private readonly int _minDelayMs;
    private readonly int _maxDelayMs;
    private readonly decimal _priceFactor;
    private readonly HashSet<int> _failForOrderIds;
    private readonly bool _failForEvenOrderIds;

    public string Name { get; }

    protected ShippingCarrierBase(
        string name,
        int minDelayMs,
        int maxDelayMs,
        decimal priceFactor,
        IEnumerable<int>? failForOrderIds = null,
        bool failForEvenOrderIds = false)
    {
        Name = name;
        _minDelayMs = minDelayMs;
        _maxDelayMs = maxDelayMs;
        _priceFactor = priceFactor;

        _failForOrderIds = new HashSet<int>(
            failForOrderIds ?? Enumerable.Empty<int>());

        _failForEvenOrderIds = failForEvenOrderIds;
    }

    public async Task<decimal> GetQuoteAsync(
        Order order,
        CancellationToken ct)
    {
        try
        {
            await Task.Delay(150, ct);

            if (_failForOrderIds.Contains(order.Id) ||
                (_failForEvenOrderIds && order.Id % 2 == 0))
            {
                throw new ShippingProviderUnavailableException(
                    $"{Name} unavailable");
            }

            await Task.Delay(
                Random.Shared.Next(
                    _minDelayMs,
                    _maxDelayMs + 1),
                ct);

            return Math.Round(
                order.ShippingCost * _priceFactor,
                2,
                MidpointRounding.AwayFromZero);
        }
        catch (OperationCanceledException)
        {
            AppLogger.Log($"[{Name}] cancelled");
            throw;
        }
    }
}
public sealed class FastShip : ShippingCarrierBase
{
    public FastShip(
        IEnumerable<int>? failForOrderIds = null)
        : base(
            "FastShip",
            500,
            1500,
            1.00m,
            failForOrderIds)
    {
    }
}

public sealed class CairoExpress : ShippingCarrierBase
{
    public CairoExpress(
        IEnumerable<int>? failForOrderIds = null)
        : base(
            "CairoExpress",
            300,
            2000,
            0.95m,
            failForOrderIds,
            failForEvenOrderIds: true)
    {
    }
}

public sealed class NileCourier : ShippingCarrierBase
{
    public NileCourier(
        IEnumerable<int>? failForOrderIds = null)
        : base(
            "NileCourier",
            800,
            1200,
            1.10m,
            failForOrderIds)
    {
    }
}