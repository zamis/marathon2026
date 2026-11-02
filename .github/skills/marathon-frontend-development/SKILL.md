---
name: marathon-frontend-development
description: "Implement and validate marathon2026 user-facing production features in Angular and TypeScript. Use for pages, components, forms, routing, services, and API integration."
---

# Marathon Frontend Development

## Procedure
1. Read the accepted criteria and inspect relevant Angular components, services, routes, styles, and API contracts.
2. Implement only frontend production files under `frontend/restaurant-frontend-app/`.
3. Follow existing Angular, TypeScript, RxJS, and Angular Material patterns; preserve responsive layout and accessibility.
4. Integrate with the agreed backend contract and handle loading, empty, success, and failure states.
5. Follow the `marathon-changelog-maintenance` skill and update or create `frontend/restaurant-frontend-app/CHANGELOG.md` for the implementation changes.
6. Run the narrowest useful frontend lint/build validation and follow `marathon-run-known-tests` to run applicable existing focused tests. Test files belong to the unit-test agent.
7. Report production and changelog files changed, behavior implemented, exact validation results, assumptions, and required backend/test follow-up.

## Boundaries
Do not modify backend, infrastructure, or test files. Do not add dependencies without explicit approval. Escalate unclear or missing API behavior rather than adding a production mock or backend workaround.
