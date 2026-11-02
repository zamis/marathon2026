# Restaurant AQA framework

**Homework target: Direction 1 — marathon2026's own deployed group pre-order implementation.** The chosen approach is Reqnroll + TUnit + Playwright for .NET with Screenplay: API scenarios prepare isolated customers, reservations, groups, guests and carts, while UI scenarios sign the reservation owner in and exercise the real group-review page. Supported API calls verify authorization, validation and persisted order state; the waiter read-back verifies the submitted composition.

End-to-end tests for the Green & Tasty restaurant application: Reqnroll (Gherkin) + TUnit + Playwright for .NET, with the Screenplay pattern.

Guidance for extending the suite (writing Gherkin, steps, Screenplay, test data, working with an AI assistant) is in [CLAUDE.md](CLAUDE.md).

## Group pre-order coverage (homework Direction 1)

| Homework level / behavior | UI (`@ui`) | API (`@api`) |
|---|---|---|
| **L1 — owner review:** participants, dishes, quantities, subtotals, total; cart changes; confirmed/unconfirmed and empty-cart participants | Participant cards show dish names, quantities, line prices, subtotals, submitted/pending states, and empty carts. The visible consolidated total includes submitted carts only (documented gap below). | Exact dish identity, quantities, participant subtotals and group-total arithmetic; guest cart mutation; owner-only access (other customer, guest, waiter, unauthenticated); nonexistent group |
| **L2 — consolidated submit:** only confirmed carts, same-dish quantities combined, group becomes `Submitted`, order becomes `SubmittedByCustomer`, waiter reads exact composition | Owner confirms in the review page and sees “Sent to Restaurant” | Guest cart seeding; positive composition/state checks; no confirmed carts, missing order/reservation/group, repeated submit, and non-owner submit |
| **L3 — removal:** recalculated list/total; confirmed and last guest; foreign/missing/non-owner refusals; remove then submit | Remove a guest, confirm the remaining group and verify the resulting waiter order | Single-of-several, confirmed and last guest removal; foreign participant, missing participant and non-owner authorization |

### Contract choices and implementation gaps

The homework leaves some outcomes open. Tests choose and assert:

- A repeat group submission returns **409 Conflict**.
- A non-owner group read/removal is denied with **403 Forbidden**; an unauthenticated or guest-token read is **401 Unauthorized**.
- Removing the final guest succeeds; the backend retains the reservation owner's empty session in `participants`, while the guest list is empty and `total` is `0`.

The explicit homework expectation for a group with no joined guests is `participants: []` and `total: 0`. The current backend returns its empty Owner session in that case. The API scenario asserts the required empty list and is tagged `@known-defect`; it is not weakened to match the implementation.

The homework also requires the owner-facing group total to cover all participant carts. The current UI's “Grand Total” is calculated from submitted carts only, so it excludes dishes in an unconfirmed cart. A UI `@known-defect` scenario asserts the required all-participant total; separate UI assertions cover the currently visible per-participant dish details, quantities, subtotals, cart states, empty-cart state, and submitted-cart consolidated total. The API scenario separately verifies that guest cart changes update its all-participant total.

Requirements presently blocked by the exposed contract are kept visible rather than simulated:

- An empty cart cannot be confirmed through the guest API (`POST /guest/sessions/{id}/submit` refuses it), so a group containing a *confirmed* empty guest cart cannot be prepared without bypassing the application contract. The specification is retained as `@wip`.
- The backend creates an Owner session but exposes no guest-session token for it, and its group submit service filters Owner sessions out. A `@known-defect` API scenario tries the owner-session cart update and requires it to succeed; current authorization rejects it. Owner-created dishes therefore cannot currently be confirmed/submitted through this contract.

The group and reservation missing cases are exercised using the supported submit route with an unknown group ID or reservation ID. Seeded scenarios create a fresh location, table, dish, customer, reservation and order; guest sessions and carts are prepared through the public guest-session API. Customer accounts use the `AQA-Sample-` convention. Cleanup removes guest sessions and menu fixtures where the API allows it and cancels reservations, but the service exposes no customer-delete, reservation-delete, group-delete or Owner-session-delete route. Those records can remain as test data; use a disposable deployment.

## Running these scenarios

Prerequisites: .NET 10 SDK, Node.js (for Gherkin lint), a **disposable deployment of marathon2026's own implementation**, and a configured Waiter account with access to the seeded location. The Waiter credentials are needed both to clean up seeded location/menu data and to verify the order as a Waiter. No Admin or pre-existing test table is needed; fixtures create their own location and table.

Set `UI_URL` and `API_URL` in the ignored local `.env` or as environment variables (environment variables take precedence); set `WAITER_USERNAME` and `WAITER_PASSWORD` for live scenarios. Do not point these tests at a public/third-party site. The suite creates customer accounts and real test reservations in the configured environment. Never run scenarios against a deployment unless you are authorized and have explicitly chosen that target.

Safe checks that do not contact the configured target:

```bash
dotnet build
npx gherkin-lint -c .gherkin-lintrc "Features/*.feature"
dotnet run --no-build -- --list-tests
```

Only after verifying the target is your authorized disposable deployment, run the Level 1–3 slice with:

```bash
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=api]" --report-trx
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=ui]" --report-trx
```

Traces from failed UI steps and test reports are saved under `TestResults/`. The `@known-defect` L1 empty-group scenario is expected to expose the documented implementation defect; the `@wip` empty-confirmed-cart case is not executable until the supported contract can create that state.

## Run the tests

```bash
cp .env.example .env        # then fill in your own values (see "Configuration")
dotnet build
dotnet run --no-build -- --report-trx                          # everything
dotnet run --no-build -- --treenode-filter "/*/*/*/*[Category=smoke]"   # by tag
```

Everything a run produces lands in `TestResults/`: the TUnit report and TRX, the Reqnroll report in `reqnroll/`, and Playwright traces in `traces/` (open one with `npx playwright show-trace <file>.zip`).

Some scenarios are expected to fail (`@known-defect`, bugs in the application) or are skipped (`@wip`, no admin account configured). That is intentional.

## Quality gates

| Where | What runs |
|---|---|
| Git hooks (local) | `pre-commit`: `dotnet format` check and Gherkin lint on staged files · `commit-msg`: Conventional Commits · `pre-push`: build with analyzers |
| `aqa-quality.yml` | on every push and pull request: no generated files tracked, format, build (warnings are errors), Gherkin lint, test discovery |
| `aqa-tests.yml` | the scenarios themselves, nightly and on demand (Actions → AQA Tests → Run workflow) |

Enable the hooks once per clone, from the repository root:

```bash
dotnet tool restore --tool-manifest aqa/restaurant-aqa-framework/.config/dotnet-tools.json
git config core.hooksPath aqa/restaurant-aqa-framework/.husky
```

The hooks only act on commits and pushes that touch `aqa/restaurant-aqa-framework`. Gherkin lint needs Node.js.

## Configuration

There is no shared environment and no committed `.env`: copy `.env.example` to `.env` and fill it in with the data of **your own** deployment (see "Testing your own deployment"). `.env` is git-ignored; never commit it.

Settings come from `.env` or real environment variables (variables win):

| Setting | Meaning |
|---|---|
| `UI_URL` | where the frontend is served |
| `API_URL` | base URL of the backend API, including the stage (for example `…/develop`) |
| `WAITER_USERNAME`, `WAITER_PASSWORD` | a waiter account that already exists in the environment under test |
| `TESTTABLE_ID`, `TESTTABLE_WAITER_*` | a table and the waiter assigned to it (only for scenarios that need them) |
| `ADMIN_USERNAME`, `ADMIN_PASSWORD` | an admin account; without it the admin scenarios are skipped |

The full list, with defaults, is in `.env.example`.

## Testing your own deployment

### If you deployed the backend / frontend / DevOps homework

1. `cp .env.example .env` and set `UI_URL` and `API_URL` to your deployment (API URL includes the stage, for example `…/develop`).
2. Waiters cannot be created by signing up, so create a waiter (and, for the admin scenarios, an admin) in *your* environment and put its credentials in `WAITER_*` / `ADMIN_*`. Optionally add a table and its assigned waiter (`TESTTABLE_*`).
3. Run `dotnet build` and a small slice first (`--treenode-filter "/*/*/*/*[Category=smoke]"`).

This group-pre-order homework is specifically Direction 1. Do not redirect these scenarios to a public/third-party site or use them to make real reservations.

### CI on your own public repository

Publish your work in your own public Git repository (the link is the deliverable). Workflows are disabled in a fresh fork until you enable them. Add the same values under Settings → Secrets and variables → Actions:

- **Variables:** `AQA_UI_URL`, `AQA_API_URL`, `AQA_TESTTABLE_ID`
- **Secrets:** `AQA_WAITER_USERNAME`, `AQA_WAITER_PASSWORD`, `AQA_ADMIN_USERNAME`, `AQA_ADMIN_PASSWORD`, `AQA_TESTTABLE_WAITER_USERNAME`, `AQA_TESTTABLE_WAITER_PASSWORD`

`aqa-quality.yml` runs on every push and pull request; `aqa-tests.yml` (nightly and on demand) tests whatever those variables point at. Never put credentials in the repository, in `.env.example` or in test code.

## Presetup instructions

This framework contains PlaywriteInstaller script so you don't need to setup it manually.
For full BDD highlighting in your IDE you may need to install Reqnroll extension https://docs.reqnroll.net/latest/ide-integrations/index.html
To start the Playwright code generator open Powershell in the framework directory and enter the line:
```bash
bin/Debug/net10.0/playwright.ps1 codegen "<your UI_URL or the public site you test>"
```
Build first so `playwright.ps1` exists. The URL is optional.
