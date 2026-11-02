---
name: marathon-run-known-tests
description: "Run existing focused tests for functionality being implemented in marathon2026. Use when a backend or frontend change has a known relevant test file or test command."
user-invocable: false
---

# Marathon Run Known Tests

## When to Use
- After implementing backend or frontend functionality when relevant existing tests and a runnable test command are known.
- When validating a production change against an existing regression or behavior test.

## Procedure
1. Identify existing tests that directly cover the changed functionality and inspect the relevant test scripts or project configuration for the supported command.
2. Run the narrowest relevant test selection after implementation. Do not substitute a build or lint command for an applicable test.
3. Keep test-source and test-configuration ownership with **Marathon Unit Tests**; run tests for validation only and do not edit their files.
4. If no relevant existing test or supported command can be identified, report that explicitly rather than running an unrelated suite.
5. Report the exact command and outcome. If tests fail, distinguish failures caused by the change from unrelated or environmental failures and hand off any required test-file changes to **Marathon Unit Tests**.
