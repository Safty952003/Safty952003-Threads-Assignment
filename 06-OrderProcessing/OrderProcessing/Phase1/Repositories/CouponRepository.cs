
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1.Repositories;

public class CouponRepository
{
    private readonly JsonFileStore _store;

    public CouponRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<Coupon?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        AppLogger.Log(
            $"[CouponRepository] Opening connection to get coupon '{code}'.");

        await Task.Delay(200, cancellationToken);

        AppLogger.Log(
            $"[CouponRepository] Executing query for coupon '{code}'.");

        int queryDelay = Random.Shared.Next(300, 601);

        await Task.Delay(queryDelay, cancellationToken);

        var coupons = await _store.ReadAsync<List<Coupon>>(
            DataFiles.Coupons,
            cancellationToken);

        var coupon = coupons.FirstOrDefault(c =>
            string.Equals(
                c.Code,
                code,
                StringComparison.OrdinalIgnoreCase));

        AppLogger.Log(
            coupon is null
                ? $"[CouponRepository] Coupon '{code}' not found."
                : $"[CouponRepository] Coupon '{code}' retrieved successfully.");

        return coupon;
    }
}
