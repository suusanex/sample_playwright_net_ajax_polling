# Mode: Minimal Patch / Hotfix

You are making a **minimal, targeted fix** to existing code.
The primary goal is to **change as little as possible** while correctly addressing the reported issue.

## Goals and priorities

- Keep the diff **as small and focused as possible**.
- Do **not** perform opportunistic refactoring.
- Preserve existing behavior everywhere except where the fix is explicitly required.

## Scope of changes

- Only modify the **lines and functions that are strictly necessary** for the fix.
- Do **not**:
  - change public APIs or data contracts
  - rename methods, classes, or files
  - move code between files or layers
  - reformat large blocks of code
- If a refactor seems obviously beneficial, **do not perform it in this mode**.
  - Instead, mention it as a separate follow-up suggestion in prose.

## Logging

- Even though the change is minimal, you **must** ensure that:
  - if the same problem happens again
  - or if the new logic misbehaves  
  it can be detected from logs.
- Add **just enough logging** around the modified behavior to:
  - detect when the new path is taken
  - record key parameters or IDs
  - capture error conditions and their causes
- Keep logging low-volume:
  - Prefer a single log per operation over per-item logs.
  - Use appropriate log levels (`INFO` / `WARN` / `ERROR`) according to severity.

## Tests

- Do not introduce large new test suites or frameworks.
- If practical, **add or adjust a small, focused test** that:
  - reproduces the original problem
  - verifies that the new behavior is correct
- If adding a test would require heavy restructuring, skip it and instead:
  - clearly describe how to manually reproduce and verify the fix.

## Style

- Match the surrounding code style and patterns exactly.
- Avoid reformatting existing lines that are not directly part of the fix.
- Keep comments short and focused on **why the fix is necessary** rather than restating what the code does.
