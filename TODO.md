# Outstanding findings

- [ ] Verify whether `backend/restaurant-backend-app/deployment_resources.json` is authoritative for deployment; if so, change `/guest/group/{groupReservationId}/sessions` to the required `/guest/groups/{groupReservationId}/sessions` to match `backend/restaurant-backend-app/dnapp/lambdas/api-handler/Routing/RouteRegistry.cs`.
- [ ] Run the backend test suite in `backend/restaurant-backend-app/Tests`; tests were not run because the prior task runner reported the command runner unavailable, so no test result is known.
