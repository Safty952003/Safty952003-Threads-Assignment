using System;
using System.Collections.Generic;
using System.Text;


namespace OrderProcessing.Models;

public class Coupon
{
    public string Code { get; set; } = string.Empty;

    public int DiscountPercentage { get; set; }

    public DateTime ExpiryDate { get; set; }
}
