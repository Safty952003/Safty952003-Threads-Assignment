# Advanced C# — Threads, Async & Thread Safety

A collection of C# assignments focused on multithreading, asynchronous programming, concurrency, and thread safety using .NET.

## Student Information

- **Name:** Mahmoud Mohamed Mahmoud Elsafty

- **Group:** G1

## About the Repository

This repository contains six assignments covering common concurrency problems and practical solutions in C#.

Each assignment includes the corrected project, written answers, research notes, or execution evidence where applicable.

## Assignments

### 01 — Invoice Export

**Directory:** [`01-InvoiceExport/`](01-InvoiceExport/)

A project focused on improving an invoice-exporting workflow while preserving its original business logic.

**Topics covered:**
- Asynchronous programming
- Parallel processing
- Order and customer data processing
- Comparing sequential and parallel execution

Includes the corrected project and `ANSWERS.md`, documenting the problems and changes made.

### 02 — Table Booking

**Directory:** [`02-TableBooking/`](02-TableBooking/)

A concurrency assignment involving multiple threads competing for a limited number of available tables.

**Topics covered:**
- Race conditions
- Shared resources
- Thread safety
- Synchronization

Includes the corrected project and `ANSWERS.md`, explaining the issues and how they were fixed.

### 03 — Locking Research

**Directory:** [`03-LockingResearch/`](03-LockingResearch/)

A research assignment about locking and concurrency concepts.

Includes `ANSWERS.md` with answers to questions Q1–Q5 and related reference links.

### 04 — Menu Views

**Directory:** [`04-MenuViews/`](04-MenuViews/)

A concurrency assignment involving a shared menu-view counter.

**Topics covered:**
- Race conditions
- Lost updates
- Concurrent counter updates
- Thread-safe operations

Includes the corrected project and `ANSWERS.md`.

### 05 — Views and Emails

**Directory:** [`05-ViewsAndEmails/`](05-ViewsAndEmails/)

A project involving concurrent view tracking and email-job processing.

**Topics covered:**
- Thread safety
- Concurrent collection access
- Shared-state management
- Email-job processing

Includes the corrected project and `ANSWERS.md`.

### 06 — Order Processing

**Directory:** [`06-OrderProcessing/`](06-OrderProcessing/)

A multi-phase project covering order processing and more advanced asynchronous workflows.

**Topics covered:**
- Asynchronous order processing
- Concurrent operations
- JSON file storage
- Payment failures and refunds
- Cancellation and error handling
- Shipping quotes
- Fraud scoring
- Email retries
- Performance measurements

The directory contains the `OrderProcessing` project, sample output files, screenshots, and a dedicated README documenting the design, measurements, and assignment answers.

See [`06-OrderProcessing/README.md`](06-OrderProcessing/README.md) for details.

## Technologies

- C#
- .NET
- Tasks and `async/await`
- Threads and `ThreadPool`
- Parallel processing
- Thread safety and synchronization
- JSON file storage
- Git and GitHub

## Learning Objectives

Through these assignments, the main objectives are to:

- Understand synchronous and asynchronous execution.
- Identify and fix race conditions.
- Manage shared resources safely.
- Understand threads, tasks, and thread-pool behavior.
- Handle exceptions and cancellation in asynchronous workflows.
- Compare sequential and concurrent performance.
- Document problems, solutions, and research findings.

## How to Run

Make sure the appropriate .NET SDK is installed.

1. Clone the repository:

   ```bash
   git clone https://github.com/Safty952003/Safty952003-Threads-Assignment.git
   ```

2. Navigate to the assignment you want to run and open its corrected project directory.

   For example:

   ```bash
   cd 01-InvoiceExport/InvoiceExport
   ```

3. Restore dependencies and run the project:

   ```bash
   dotnet restore
   dotnet run
   ```

For assignment-specific instructions, refer to the corresponding `ANSWERS.md` or README file.

## Repository Structure

```text
.
├── 01-InvoiceExport/
│   ├── InvoiceExport/
│   └── ANSWERS.md
├── 02-TableBooking/
│   ├── TableBooking/
│   └── ANSWERS.md
├── 03-LockingResearch/
│   └── ANSWERS.md
├── 04-MenuViews/
│   ├── MenuViews/
│   └── ANSWERS.md
├── 05-ViewsAndEmails/
│   ├── ViewsAndEmails/
│   └── ANSWERS.md
├── 06-OrderProcessing/
│   ├── OrderProcessing/
│   ├── sample-output/
│   ├── screenshots/
│   └── README.md
└── LinkedIn/
    └── linked.md
```


