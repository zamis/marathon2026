using APIHandler.Handlers.GuestSessions;
using APIHandler.Routing;
using System;

namespace APIHandler.Tests.Routing;

public class GroupReservationSessionsRouteTests
{
    [Fact]
    public void RouteRegistry_MapsGroupReservationSessionsGetRouteToHandler()
    {
        var requiredConfiguration = new[]
        {
            "REGION",
            "FEEDBACK_TABLE_NAME",
            "WaiterListTableName",
            "locationsTable",
            "dishesTable",
            "profileImagesTable",
            "reportsTable",
            "reportsHistoryTable",
            "COGNITO_USER_POOL_ID",
            "COGNITO_USER_POOL_CLIENT_ID",
            "reservationsTable",
            "groupReservationsTable",
            "tablesTable",
            "ordersTable",
            "notificationsTable",
            "notificationsUserIdIndex",
            "reservationsTableGlobalIndex",
            "waitersGlobalIndex",
            "reservationsCustomerIdGlobalIndex",
            "reservationsWaiterIdGlobalIndex",
            "imagesBucketName",
            "feedbackGlobalIndex",
            "profileImagesBucketName",
            "OrderIndexReservationId",
            "reports_queue_name",
            "guestSessionTable",
            "tableLocationIdIndex"
        };
        var previousValues = requiredConfiguration.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable);
        foreach (var name in requiredConfiguration)
        {
            Environment.SetEnvironmentVariable(name, name == "REGION" ? "us-east-1" : "test-value");
        }

        try
        {
            var route = Assert.Single(RouteRegistry.GetAllRoutes().Where(candidate =>
                candidate.Route.Method == "GET" &&
                candidate.Route.Template == "/guest/groups/{groupReservationId}/sessions"));

            Assert.IsType<GetGroupReservationSessionsHandler>(route.Handler);
            var pathParameters = route.Match("/guest/groups/group-1/sessions");
            Assert.NotNull(pathParameters);
            var groupReservationId = Assert.Single(pathParameters);
            Assert.Equal("groupReservationId", groupReservationId.Key);
            Assert.Equal("group-1", groupReservationId.Value);
            Assert.Null(route.Match("/guest/groups/group-1/sessions/extra"));
        }
        finally
        {
            foreach (var (name, value) in previousValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
