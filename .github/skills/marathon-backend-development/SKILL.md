---
name: marathon-backend-development
description: "Implement and validate marathon2026 backend production changes in C#, .NET 10, AWS Lambda, and explicitly required AWS infrastructure. Use for backend API and Lambda tasks."
---

# Marathon Backend Development

## Procedure
1. Read the relevant API documentation, handler, domain and persistence code, project conventions, and accepted criteria.
2. Trace the existing request-to-response and authorization flow before editing.
3. Implement within `backend/restaurant-backend-app/`; change `infra/terraform/` only when explicitly required by the accepted task.
4. Preserve established response/error shapes, state-transition rules, and serialization conventions. Handle invalid, unauthorized, missing, and conflicting state as specified.
5. Follow the `marathon-changelog-maintenance` skill and update the backend `CHANGELOG.md` for the implementation changes.
6. Build the narrowest relevant .NET project/solution and follow `marathon-run-known-tests` to run applicable existing focused tests. Test files belong to the unit-test agent.
7. Report production and changelog files changed, behavior implemented, exact validation results, assumptions, and any cross-role work needed.

## Project safeguards
- The backend uses .NET 10 C# Lambda projects and AWS Syndicate.
- Never expose credentials or run AWS deployment/resource-changing commands.
- Do not edit frontend or test files; communicate required API/UI/test contracts to the orchestrator.
