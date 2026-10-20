## Phase 1 — Questions and Answers

### 1. Why are the sync and async times almost the same when orders are awaited one after another?

Both versions process the orders sequentially, one order at a time. In the async version, each order is awaited before starting the next one, so the operations do not overlap. Therefore, the total execution times are almost the same.

### 2. Is the thread ID before an await always the same as after it? Why? Use your own log lines as evidence.

No, the thread ID is not always the same before and after an `await`. When the awaited operation completes, the continuation may run on a different thread.

My application logs show this:

```text
[03:02:17.808] [Thread 12] Before await - Thread ID: 12
[03:02:18.434] [Thread 5] After await - Thread ID: 5
```

The thread ID changed from 12 to 5, demonstrating that the continuation resumed on a different thread after the asynchronous operation.

### 3. What does the thread do while Task.Delay() is waiting? And while Thread.Sleep() is waiting?

With `Task.Delay()`, the asynchronous method yields control while waiting, allowing the thread to perform other work instead of remaining blocked.

With `Thread.Sleep()`, the current thread is blocked for the specified duration and cannot perform other work during that time.

### 4. Why should a repository method return Task<T> instead of T?

A repository method that performs asynchronous I/O should return `Task<T>` so the caller can use `await` without blocking the current thread while waiting for the result. Returning `T` would require a synchronous operation and could block the calling thread during I/O.

### 5. What is the difference between File.ReadAllText and File.ReadAllTextAsync? Does the async version occupy a thread while the disk is reading?

`File.ReadAllText()` reads a file synchronously and blocks the calling thread until the operation finishes.

`File.ReadAllTextAsync()` reads a file asynchronously and returns a `Task<string>` that can be awaited. The calling thread does not need to remain blocked while waiting for the disk I/O to complete. The asynchronous operation does not necessarily occupy a thread for the entire duration of the disk read.


## Phase 2 — Async Pricing, Concurrency, and Order Processing

### Questions and Answers

**1. What is the difference between starting a task and awaiting it?**

Starting a task allows its operation to begin. Using `await` asynchronously waits for its completion before continuing with the next statement. Other operations can run while the task is waiting.

**2. Does every Task create a new thread? Does Task.Delay() use a thread while waiting?**

No. A `Task` represents an asynchronous operation and does not necessarily create a new thread. `Task.Delay()` waits without occupying a thread for the duration of the delay.

**3. What happens if you call .Result or .Wait() on an unfinished task instead of await? Why is it dangerous?**

Both block the current thread until the task completes. This can waste thread-pool resources and, in some environments, cause deadlocks. Using `await` is generally preferable.

**4. What happens to an exception thrown inside a task that is never awaited?**

The exception may remain stored in the task and never be observed by the calling code. The application may therefore fail to handle the error properly. Tasks should be awaited or otherwise observed.

**5. When should you use Task.Run(), and when should you avoid it?**

Use `Task.Run()` for CPU-bound work that would otherwise block the calling thread, such as the invoice-rendering simulation. Avoid wrapping naturally asynchronous I/O operations in `Task.Run()` unnecessarily.

**6. What is the difference between Task and Task<T>?**

`Task` represents an asynchronous operation without a returned result. `Task<T>` represents an asynchronous operation that produces a result of type `T`.

**7. Why can a lock block not contain an await?**

The traditional `lock` statement cannot contain `await` because its monitor must be released on the same thread that acquired it. For asynchronous synchronization, use `SemaphoreSlim` with `WaitAsync()` and release it in a `finally` block.

**8. Why did the unprotected stock reservation cause a race condition, and how was it prevented?**

Both operations could read the same available stock before either wrote the updated value. As a result, both could report successful reservations for the same item. Protecting the complete read-check-update-write operation with a shared `SemaphoreSlim` ensures that reservations execute one at a time.
