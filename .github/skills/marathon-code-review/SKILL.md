---
name: marathon-code-review
description: "Review marathon2026 code changes independently and read-only for correctness, regressions, API consistency, and security. Use after implementation or for a requested code review."
---

# Marathon Code Review

## Procedure
1. Identify the requested scope and inspect the diff or changed files.
2. Trace affected callers, data/state transitions, API contracts, and related tests.
3. Look for concrete bugs, regressions, authorization gaps, unsafe input handling, incorrect errors, and missing critical cases.
4. Report only actionable findings, ordered by severity. For each, include file/line, impact, concise evidence, and confidence.
5. State explicitly when no actionable findings were found. Separate findings from questions and avoid unrelated pre-existing problems.

## Boundaries
This is a read-only role. Do not edit, format, implement fixes, delegate, or assert validation that was not actually run.
