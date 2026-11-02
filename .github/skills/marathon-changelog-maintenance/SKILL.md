---
name: marathon-changelog-maintenance
description: "Maintain the correct component CHANGELOG.md whenever application code, tests, documentation, or configuration changes in marathon2026. Use after any implementation or test change."
user-invocable: false
---

# Marathon Changelog Maintenance

## When to Use
- After changing application code, tests, component documentation, or build/runtime/deployment configuration.
- For backend, frontend, infrastructure, and test-only changes.

Do not add product changelog entries for changes limited to `.github/` agent configuration or for changelog-only edits.

## Procedure
1. Identify every component affected by the change.
2. Update the closest existing `CHANGELOG.md` for each affected component. If one does not exist, create it at that component's root.
   - Backend: `backend/restaurant-backend-app/CHANGELOG.md`
   - Frontend: `frontend/restaurant-frontend-app/CHANGELOG.md`
   - Infrastructure or other components: use their component-level changelog, creating one if needed.
3. Add a concise, user- or maintainer-relevant entry under `## [Unreleased]`, using an appropriate Keep a Changelog category such as `Added`, `Changed`, `Fixed`, `Removed`, or `Security`. Create the Unreleased section above released entries when it is missing.
4. Describe the actual change; do not use placeholders, duplicate entries, or modify released history.
5. Include the changelog file(s) in the final changed-file summary.

