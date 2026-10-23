using System;
using System.Collections.Generic;
using System.Text;


namespace OrderProcessing.Shared.Models;

public class OrderItem
{
    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}

