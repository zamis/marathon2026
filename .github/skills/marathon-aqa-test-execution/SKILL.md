---
name: marathon-aqa-test-execution
description: "Build, lint, discover, run, and diagnose tests in marathon2026's AQA framework. Use for safe local checks, focused TUnit filters, CI workflow guidance, Reqnroll reports, Playwright traces, and flaky-test investigation."
---

# Marathon AQA Test Execution

Run the smallest check that proves the change, and clearly distinguish offline validation from scenarios that contact a deployed site.

## Safe local checks

Run commands from `aqa/restaurant-aqa-framework`:

```bash
dotnet build
npx gherkin-lint -c .gherkin-lintrc "Features/*.feature"
dotnet run --no-build -- --list-tests
```

Use the repository's exact CI commands when validating CI parity, including Release configuration, `dotnet format ... --verify-no-changes`, and the AQA quality workflow. Do not install dependencies unless a required tool is missing or dependency manifests changed.

## Scenario execution

- Before running scenarios, check that `.env` is local and ignored, and that required `UI_URL` / `API_URL` and account values target an environment the user authorized.
- Never read out, paste, or log credential values. Do not create or modify `.env` with guessed credentials.
- A filtered scenario run still contacts the configured application. Run it only when requested and configured; prefer a narrow tag/filter before a broader run.
- Examples from the framework README:

```bash
dotnet run --no-build -- --report-trx
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=smoke]"
dotnet bin/Debug/net10.0/restaurant-aqa-framework.dll --list-tests
```

The project writes test output under `TestResults/`; do not commit generated reports or traces.

## Diagnose failures

1. Identify whether the failure is in build/discovery, setup, the test assertion, or the application under test.
2. For `@ui` failures, inspect the failing step's diagnostic state and its Playwright trace. Open a trace with `npx playwright show-trace <trace-file.zip>` when available.
3. For `@api` failures, inspect the response details already captured by the test and verify the configured base URL/stage without exposing secrets.
4. Check whether a failure is an intentional `@known-defect`, an unfinished `@wip`, an environment/setup failure, a flaky test, or a genuine regression. Do not reclassify a real defect just to get a green run.
5. Fix test code only when the test is incorrect. Report likely product defects to the owning application developer.
6. If a fix changes selectors, waits, setup, or cleanup, rerun the narrow relevant check and scenario when authorized.

## Reporting

State the exact commands run and their outcomes. Distinguish build/lint/test discovery from live scenario execution, identify skipped or expected-failing scenarios, and never claim an unexecuted test passed.
