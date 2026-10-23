
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1.Repositories;

public class SyncCouponRepository
{
    private readonly JsonFileStore _store;

    public SyncCouponRepository(JsonFileStore store)
    {
        _store = store;
    }

    public Coupon? GetByCode(string code)
    {
        AppLogger.Log(
            $"[SyncCouponRepository] Opening connection to get coupon '{code}'.");

        Thread.Sleep(200);

        AppLogger.Log(
            $"[SyncCouponRepository] Executing query for coupon '{code}'.");

        Thread.Sleep(Random.Shared.Next(300, 601));

        var coupons = _store.Read<List<Coupon>>(DataFiles.Coupons);

        return coupons.FirstOrDefault(c =>
            string.Equals(
                c.Code,
                code,
                StringComparison.OrdinalIgnoreCase));
    }
}

