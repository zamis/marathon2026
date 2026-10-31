---
name: marathon-orchestration
description: "Coordinate multi-step marathon2026 work across requirements, backend, frontend, unit-test, and review agents. Use when sequencing specialists, checking their reports, or preparing a final delivery summary."
---

# Marathon Orchestration

## Procedure
1. Translate the request into bounded outcomes and identify affected roles.
2. For every feature description, feature request, and backlog-creation request, invoke **Marathon User story Writer** before implementation, even if the request appears clear or narrowly specified. This is mandatory: never skip this step, write stories yourself, or delegate story writing to another agent. Skip only for requests exclusively about maintenance or review that do not define features or create backlog items.
3. Split backend, frontend, and test ownership by files and deliverables. Clarify shared API contracts before parallel implementation.
4. Delegate implementation only to the relevant production-code owner. Keep unit-test files exclusively with the test agent.
5. After implementation, have the test agent add/update tests and report exact commands and results.
6. Have the code-review agent review the completed change read-only.
7. Verify each report against the assignment, changed-file ownership, acceptance criteria, and validation evidence. Route failures/findings back to the owner and repeat relevant checks.
8. Summarize completed work, files/areas, validation outcomes, review findings, and unresolved risks. Do not make changes yourself or overstate verification.

## Required specialist handoff
The User story Writer's English stories and acceptance criteria are the requirements baseline for feature and backlog work. Each implementation assignment must state its scope, permitted file ownership, excluded roles, acceptance criteria, dependencies, and expected report fields: changed files, work performed, validation command/results, assumptions, and blockers.

## Order adjustments
Skip requirements work for a clear, narrow maintenance/review request. Backend and frontend may run in parallel only when neither depends on the other's unfinished contract. Review occurs after implementation and relevant tests; fixes go back to the owning agent.
