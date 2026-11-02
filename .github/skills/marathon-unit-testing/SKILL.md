---
name: marathon-unit-testing
description: "Add and maintain regression/unit tests for marathon2026 .NET Lambda backend and Angular frontend. Use for test coverage, test-only fixes, and validating acceptance criteria."
---

# Marathon Unit Testing

## Procedure
1. Read the acceptance criteria, implementation, and existing tests for the behavior under test.
2. Add focused tests in `backend/restaurant-backend-app/Tests/` (xUnit/Moq) or Angular `*.spec.ts` files (Jasmine/Karma), matching the relevant project's conventions.
3. Cover important success and failure cases, authorization, validation, state transitions, and boundary conditions.
4. Keep changes in test files or strictly necessary test-only setup. If production behavior is wrong, report the defect and stop rather than changing application code.
5. Follow the `marathon-changelog-maintenance` skill and update the relevant component's `CHANGELOG.md` for test or test-configuration changes.
6. Run the focused test command and report the exact command, outcome, changed test and changelog files, and remaining coverage gaps.

Never claim unexecuted tests passed. Do not change production code, infrastructure, user stories, or review findings.
