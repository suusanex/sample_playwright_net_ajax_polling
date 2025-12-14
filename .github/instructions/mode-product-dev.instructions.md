# Mode: Ongoing Product Development

You are contributing to a **long-lived product**.
The primary goal is to **improve features while keeping the codebase maintainable and stable**.

## Goals and priorities

- Prefer **extensibility, readability, and testability** over quick hacks.
- Respect and follow the existing **architecture, layering, and coding conventions** in this repository.
- Changes should be **easy for future maintainers to understand**.

## Scope of changes

- Focus refactoring on the **area directly related to the requested change**.
  - It is acceptable to:
    - extract helper methods
    - reduce duplication
    - improve names
    - clarify control flow
  - but only **within or near the modified feature**.
- Avoid changes that affect **unrelated modules or public APIs**:
  - Do not change public interfaces, data contracts, or widely used types unless explicitly requested.
  - Do not re-organize files or move large amounts of code in the same change.
- If you see a larger refactor opportunity:
  - briefly **describe it in comments or prose first**
  - keep the actual implementation for a separate change, unless the user explicitly asks for it now.

## Logging and observability

- Assume the code will run in **production, possibly without an attached debugger**.
- Add or adjust logs so that **issues can be diagnosed from logs alone**, while avoiding excessive noise:
  - Log at **entry / exit** of important operations.
  - Log key decisions (e.g., which branch was chosen, which provider was selected).
  - Always log failures with enough context to trace the cause.
- Use log levels carefully:
  - `INFO`: important high-level events
  - `DEBUG`: additional details useful during incident analysis, but not needed all the time
  - `WARN` / `ERROR`: actual or potential problems
- Do not add high-volume logs (e.g., per-item logs in large loops) unless explicitly requested.

## Tests

- When you change behavior, **add or update unit tests** around the modified area.
  - Prefer small, focused tests (happy-path + representative edge cases).
  - Reuse existing testing patterns and helpers in the repository.
- Keep tests local to the changed feature; do not introduce a completely new test framework in the same change.

## Style

- Match the **existing style and patterns** of this codebase:
  - naming conventions
  - error-handling strategy
  - logging framework usage
- Keep functions and classes **small and single-purpose** where practical.
- Add short comments where intent might not be obvious to a future reader.
