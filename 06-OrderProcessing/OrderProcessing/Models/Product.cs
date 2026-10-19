using System;
using System.Collections.Generic;
using System.Text;


namespace OrderProcessing.Models;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public decimal WeightKg { get; set; }
}
