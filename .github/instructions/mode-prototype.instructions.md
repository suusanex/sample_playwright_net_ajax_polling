# Mode: Prototype / Exploration

You are helping with **prototype and feasibility experiments**.
The primary goal is to **quickly validate approaches and libraries**, not to build long-term production code.

## Goals and priorities

- Prefer **modern, actively maintained libraries and APIs** over legacy ones.
- Follow each library's **official / recommended usage patterns**.
- It's OK if the code is a bit rough as long as it:
  - clearly demonstrates the approach
  - is easy to read and debug by a single developer

## Scope of changes

- You may introduce **new files, helper functions, or local refactors** if they make the prototype easier to understand or extend later.
- You **do not need** to design a full architecture or abstraction layer.
- Avoid over-engineering: no unnecessary interfaces, DI containers, or generic frameworks unless they directly help with the experiment.

## Logging and diagnostics

- Assume the code will be run **many times while debugging**.
- Add **verbose logging** around:
  - external I/O (network calls, file access, DB, cloud APIs)
  - important branches and decisions
  - error handling paths
- Error logs must clearly state:
  - what we tried to do
  - what went wrong (include exception type and message)
  - key parameters or IDs involved
- Use log levels consistently:
  - `DEBUG` for detailed flow and parameters
  - `INFO` for main high-level steps
  - `WARN` / `ERROR` for failures and unexpected conditions

## Tests

- Unit tests are **nice to have but optional**.
- If a small test makes the prototype more understandable (e.g., one or two happy-path tests), you can add it.
- Do **not** spend time on full coverage or complex test scaffolding.

## Style

- Follow the existing code style in this repository when obvious.
- Use clear, intention-revealing names.
- Prefer straightforward, linear code over "clever" abstractions.
