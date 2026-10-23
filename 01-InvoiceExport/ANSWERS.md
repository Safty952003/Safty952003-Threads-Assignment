# Assignment 01 — Answers

Student name:  Mahmoud Mohamed Elsafty
Group / ID:G1

## Problems with the current design 

1. **Blocking Operations:** The code uses `Thread.Sleep()`, which blocks the current thread during delays.
2. **Synchronous File I/O:** `File.ReadAllText()` and `File.WriteAllText()` perform file operations synchronously, blocking the thread until the operations finish.
3. **Sequential Processing:** Orders are processed one by one. Each order must finish before the next order starts, increasing the total execution time when many orders need to be exported.
4. **No Asynchronous Flow:** The methods use synchronous signatures, so the application cannot take advantage of asynchronous waiting for these operations.


## What you changed

- Replaced `Thread.Sleep()` with `await Task.Delay()` to avoid blocking the current thread during simulated delays.
- Replaced `File.ReadAllText()` and `File.WriteAllText()` with their asynchronous versions.
- Changed the database, customer file store, and invoice writer methods to use `async` and `await`.
- Started the order-export tasks first, stored them in a `List<Task>`, and awaited them one by one without using an API that waits for a group of tasks.
- Preserved the same business flow: load order → read customer profile → write invoice.
- Kept the invoice content unchanged. The output order differs because the tasks can finish at different times.

## Console Output Before the Changes

```text
Starting invoice export...
Customer files: C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\01-InvoiceExport\InvoiceExport\bin\Debug\net9.0\Data
Invoice output: C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\invoices

Wrote C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\invoices\invoice-1001.txt
Wrote C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\invoices\invoice-1002.txt
Wrote C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\invoices\invoice-1003.txt
Wrote C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\invoices\invoice-1004.txt
Wrote C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\invoices\invoice-1005.txt
Export finished in 6.0 s
```

## Console Output After the Changes

```text
Starting invoice export...
Customer files: C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\01-InvoiceExport\InvoiceExport\bin\Debug\net9.0\Data
Invoice output: C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\01-InvoiceExport\InvoiceExport\bin\Debug\net9.0\invoices

Wrote C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\01-InvoiceExport\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1005.txt
Wrote C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\01-InvoiceExport\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1002.txt
Wrote C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\01-InvoiceExport\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1001.txt
Wrote C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\01-InvoiceExport\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1004.txt
Wrote C:\Users\mahmoud\source\repos\Safty952003-Threads-Assignment\01-InvoiceExport\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1003.txt
Export finished in 1.9 s
```
