# Assignment 04 — Answers

Student name: Mahmoud Mohamed Elsafty  
Group / ID: G1

## Why Expected and Actual disagree

`Views++` is not an atomic operation. Multiple threads can read the same value and overwrite each other's updates, causing lost increments. This is a race condition.

## What you changed

- Replaced `Views++` with `Interlocked.Increment(ref Views)` to make the counter update atomic and thread-safe.

## When would you protect the update differently

- I would use `lock` when the update involves multiple related operations or shared state that must be protected together, rather than a single counter increment.