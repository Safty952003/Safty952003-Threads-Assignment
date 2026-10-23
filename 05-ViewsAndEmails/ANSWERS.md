
# Assignment 05 — Answers

Student name: Mahmoud Mohamed Elsafty
Group / ID: G1

## Failures you observed

- In one run, the program threw an `InvalidOperationException` because multiple threads modified the `Dictionary` at the same time.
- In another run, the expected view count was 50000, but the actual count was 31904 because concurrent increments were lost.
- The email counts were correct in the second run, but this did not guarantee thread safety.

## What you changed

- Replaced `Dictionary` with `ConcurrentDictionary` and used `AddOrUpdate` to safely update view counts.
- Replaced `Queue` with `ConcurrentQueue` and used `TryDequeue` to safely retrieve email jobs.
- Kept a `lock` around `SentEmails.Add` because `List` is not thread-safe.
- Removed the exception-handling workaround because concurrent collection methods handle these concurrent operations safely.

## Collection safety vs business-rule safety

- A thread-safe collection protects its internal data from concurrent access problems, but it does not automatically make a sequence of operations follow the correct business rules. For example, checking a queue's count and then dequeuing is not an atomic operation. Using `TryDequeue` combines checking and removing an item into one safe operation.
