---
name: marathon-aqa-test-design
description: "Design risk-based automated acceptance and end-to-end test coverage for marathon2026. Use when translating homework-AQA requirements into scenarios, deciding UI versus API coverage, defining boundaries, or reviewing coverage gaps."
---

# Marathon AQA Test Design

Turn observable requirements into independent, maintainable scenarios for `aqa/restaurant-aqa-framework`.

## Procedure

1. Read the relevant section of `homework-aqa.md`, the framework `README.md`, and `CLAUDE.md`. Identify whether the target is Direction 1 (the student's own implementation) or Direction 2 (existing functionality).
2. Map each requirement to an observable outcome and a scenario. Cover success paths, authorization, validation, boundary values, state changes, empty/missing data, and the relevant user roles.
3. For Direction 1 group pre-order work, include the required API authorization/validation and preparation-data coverage as well as UI behavior where available. If participant removal is not exposed in the UI, cover that behavior through the API. Include the owner, guests, and waiter where the criteria require them.
4. For Direction 2, cover the appropriate assignment level: registration/login/menu; booking and its rules; or pre-order, waiter workflows, and feedback. Use explicit boundary cases from the homework rather than only testing typical values.
5. Keep scenarios independent and avoid relying on records already in the shared environment. Plan scenario-owned setup and cleanup with the existing seeding helpers.
6. For unspecified behavior, choose a consistent expected result, record the choice in the solution README when applicable, and assert that behavior. Preserve explicitly required empty results such as `participants: []` and `total: 0`.
7. Use Gherkin tags consistently: `@ui` or `@api` for the execution stage, and descriptive tags such as `@smoke`, `@negative`, `@validation`, or `@data-dependent` where useful.
8. Review the scenario list for duplicates and uncovered acceptance criteria before implementation.

## Scenario-writing rules

- Describe business intent and what a persona sees, not implementation details such as URLs, HTTP verbs, status codes, JSON, IDs, selectors, or button labels.
- Use personas such as visitor, guest, customer, waiter, and manager.
- Keep one behavior per scenario. Use `Rule:` to group related behavior and `Scenario Outline` for the same behavior across data values.
- Keep test data minimal: include only values that affect the rule and generate incidental values in the test.
- On a public site, automate only permitted flows. Do not make purchases or real bookings, and respect the site's terms.

## Output

When asked to plan or assess coverage, return a concise requirement-to-scenario matrix with the behavior, scenario(s), UI/API stage, and any setup or coverage gap. Do not claim a scenario exists until it has been implemented.
