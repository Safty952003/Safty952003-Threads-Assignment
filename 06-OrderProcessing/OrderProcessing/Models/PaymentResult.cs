using System;
using System.Collections.Generic;
using System.Text;


namespace OrderProcessing.Models;

public class PaymentResult
{
    public bool Success { get; set; }

    public string? TransactionId { get; set; }

    public PaymentStatus Status { get; set; }

    public string? Message { get; set; }
}

