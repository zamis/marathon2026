---
name: marathon-aqa-framework-engineering
description: "Implement or refactor automated tests in marathon2026's .NET 10 AQA framework. Use for Reqnroll/Gherkin, TUnit, Playwright UI automation, API tests, Screenplay tasks/questions, page objects, fixtures, and test data."
---

# Marathon AQA Framework Engineering

Extend `aqa/restaurant-aqa-framework` using its existing Reqnroll + TUnit + Playwright for .NET + Screenplay architecture.

## Layer boundaries

| Layer | Responsibility |
|---|---|
| `Features/` | Declarative business behavior in Gherkin |
| `StepDefinitions/` | Thin actor-oriented glue: perform a task, ask a question, assert in `Then` |
| `Screenplay/Tasks/` | Actions performed through an actor's abilities |
| `Screenplay/Questions/` | Values observed by an actor |
| `Ui/` | Page objects/components with locators and small UI actions; no assertions |
| `Api/` | Resource clients, DTOs, response helpers, and seeding |
| `Hooks/`, `Contexts/`, `Support/` | Lifecycle, scenario state, and shared helpers |

## Implementation procedure

1. Find the closest existing feature and corresponding bindings, tasks/questions, page objects or API client. Reuse shared vocabulary and behavior instead of duplicating it.
2. Add or update Gherkin first. Use the existing English feature language, required `@ui`/`@api` stage tags, and project lint rules.
3. Keep step definitions stateless and limited to the actor. Share per-scenario state through actor memory, not fields, statics, or `ScenarioContext.Current`.
4. Put repeated actions in `ITask` implementations and repeated observations in `IQuestion<T>` implementations. Keep setup in tasks, use existing response handling, and fail setup explicitly unless the scenario deliberately tests a refusal.
5. For UI work, use the existing browser ability and page-object patterns. Prefer role plus accessible name, then label, test ID, and CSS. Avoid brittle positional selectors and sleeps; use Playwright auto-waiting and retrying `Expect` assertions.
6. For API work, use the existing resource client and DTO conventions. Seed data per scenario and register cleanup through the existing `CleanupScope` / `TrackDelete` pattern. Do not add blanket retries; POST requests must not be retried.
7. Preserve existing stage scoping, trace grouping, parallel-scenario isolation, and generated-file conventions. Treat Playwright codegen output as scratch material and refactor it into the framework layers; do not commit generated code-behind or local run output.
8. Run the focused format/build/Gherkin/discovery checks described in `marathon-aqa-test-execution`.

## Quality rules

- Assert outcomes in `Then` steps with diagnostic context, such as the response body or observed page state.
- Do not put business rules or selectors in steps, or protocol status codes in business-language features.
- Keep changes inside the AQA framework unless the user explicitly requests a related AQA workflow/configuration change.
- Never modify application code to make an automation test pass. Report a suspected application defect with the failing scenario and evidence.
