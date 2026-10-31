using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class GetGroupReservationSessionsHandlerTests
{
    private readonly Mock<IGuestSessionService> _service = new();
    private readonly Mock<ILambdaContext> _context = new();

    public GetGroupReservationSessionsHandlerTests()
    {
        _context.Setup(context => context.Logger).Returns(Mock.Of<ILambdaLogger>());
    }

    [Fact]
    public async Task HandleRequest_WithOwnerClaim_ReturnsCompleteJsonResponse()
    {
        _service.Setup(service => service.GetGroupReservationSessionsAsync("owner-1", "group-1"))
            .ReturnsAsync(new GetGroupReservationSessionsResponse(
            [
                new GroupSessionParticipant(
                    "session-1",
                    ParticipantType.Owner,
                    "Host",
                    [new GroupSessionOrderItem(new GroupSessionDish("dish-1", "Tea", 2.50m), 2)],
                    5.00m,
                    true)
            ],
            5.00m));

        var response = await new GetGroupReservationSessionsHandler(_service.Object)
            .HandleRequest(AuthorizedRequest(), _context.Object);

        Assert.Equal(200, response.StatusCode);
        using var document = JsonDocument.Parse(response.Body);
        var root = document.RootElement;
        Assert.Equal(new[] { "participants", "total" }, root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(5.00m, root.GetProperty("total").GetDecimal());

        var participant = Assert.Single(root.GetProperty("participants").EnumerateArray());
        Assert.Equal(
            new[] { "id", "type", "name", "items", "total", "isSubmitted" },
            participant.EnumerateObject().Select(property => property.Name));
        Assert.Equal("session-1", participant.GetProperty("id").GetString());
        Assert.Equal("Owner", participant.GetProperty("type").GetString());
        Assert.Equal("Host", participant.GetProperty("name").GetString());
        Assert.Equal(5.00m, participant.GetProperty("total").GetDecimal());
        Assert.True(participant.GetProperty("isSubmitted").GetBoolean());

        var item = Assert.Single(participant.GetProperty("items").EnumerateArray());
        Assert.Equal(new[] { "dish", "quantity" }, item.EnumerateObject().Select(property => property.Name));
        var dish = item.GetProperty("dish");
        Assert.Equal(new[] { "id", "name", "price" }, dish.EnumerateObject().Select(property => property.Name));
        Assert.Equal("dish-1", dish.GetProperty("id").GetString());
        Assert.Equal("Tea", dish.GetProperty("name").GetString());
        Assert.Equal(2.50m, dish.GetProperty("price").GetDecimal());
        Assert.Equal(2, item.GetProperty("quantity").GetInt32());

        _service.Verify(service => service.GetGroupReservationSessionsAsync("owner-1", "group-1"), Times.Once);
    }

    [Fact]
    public async Task HandleRequest_WithoutUserClaim_ReturnsUnauthorizedWithoutCallingService()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { ["groupReservationId"] = "group-1" },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext()
        };

        var response = await new GetGroupReservationSessionsHandler(_service.Object)
            .HandleRequest(request, _context.Object);

        Assert.Equal(401, response.StatusCode);
        _service.Verify(service => service.GetGroupReservationSessionsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_WithoutGroupReservationId_ReturnsBadRequest()
    {
        var request = AuthorizedRequest();
        request.PathParameters = null;

        var response = await new GetGroupReservationSessionsHandler(_service.Object)
            .HandleRequest(request, _context.Object);

        Assert.Equal(400, response.StatusCode);
        _service.Verify(service => service.GetGroupReservationSessionsAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequest_WhenServiceForbidsNonOwner_ReturnsForbidden()
    {
        _service.Setup(service => service.GetGroupReservationSessionsAsync("other-user", "group-1"))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));

        var response = await new GetGroupReservationSessionsHandler(_service.Object)
            .HandleRequest(AuthorizedRequest("other-user"), _context.Object);

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_WhenGroupReservationDoesNotExist_ReturnsNotFound()
    {
        _service.Setup(service => service.GetGroupReservationSessionsAsync("owner-1", "group-1"))
            .ThrowsAsync(new GroupReservationNotFoundException());

        var response = await new GetGroupReservationSessionsHandler(_service.Object)
            .HandleRequest(AuthorizedRequest(), _context.Object);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_WhenParentReservationDoesNotExist_ReturnsNotFound()
    {
        _service.Setup(service => service.GetGroupReservationSessionsAsync("owner-1", "group-1"))
            .ThrowsAsync(new ReservationNotFoundException("Parent reservation not found."));

        var response = await new GetGroupReservationSessionsHandler(_service.Object)
            .HandleRequest(AuthorizedRequest(), _context.Object);

        Assert.Equal(404, response.StatusCode);
    }

    private static APIGatewayProxyRequest AuthorizedRequest(string userId = "owner-1") => new()
    {
        PathParameters = new Dictionary<string, string> { ["groupReservationId"] = "group-1" },
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = new APIGatewayCustomAuthorizerContext
            {
                Claims = new Dictionary<string, string> { ["sub"] = userId }
            }
        }
    };
}
