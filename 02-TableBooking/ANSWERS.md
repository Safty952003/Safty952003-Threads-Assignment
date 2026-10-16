# Assignment 02 — Answers

Student name: Mahmoud Mohamed Elsafty  
Group / ID:G1

## What is wrong

The original code has a race condition. Multiple threads can check `TablesLeft` at the same time and find available tables before any of them updates the counter. Because there is no synchronization, more customers may book tables than the available number, and the counters can become incorrect or negative.

## How you fixed it

I used a `lock`  to protect the booking operation. This ensures that only one thread at a time can check table availability and update `TablesLeft` and `Booked`.

I also changed `Program.cs` to use `Task.Run()` and a `Task[]`. All tasks are started first and then awaited one by one.

After the fix, repeated runs produced the correct final totals:

```text
Booked=5, TablesLeft=0
```