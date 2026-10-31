using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class DeleteGuestSessionHandlerTests
{
    private readonly Mock<IGuestSessionService> _service = new();
    private readonly Mock<ILambdaContext> _context = new();

    public DeleteGuestSessionHandlerTests()
    {
        _context.Setup(context => context.Logger).Returns(Mock.Of<ILambdaLogger>());
    }

    [Fact]
    public async Task HandleRequest_WithOwnerClaim_ReturnsUpdatedSnapshot()
    {
        _service.Setup(service => service.DeleteGuestSessionAsync("owner-1", "group-1", "session-deleted"))
            .ReturnsAsync(new GetGroupReservationSessionsResponse(
            [
                new GroupSessionParticipant(
                    "session-remaining",
                    ParticipantType.Guest,
                    "Mina",
                    [new GroupSessionOrderItem(new GroupSessionDish("pasta", "Pasta", 7.50m), 2)],
                    15.00m,
                    false)
            ],
            15.00m));

        var response = await new DeleteGuestSessionHandler(_service.Object)
            .HandleRequest(AuthorizedRequest(guestSessionId: "session-deleted"), _context.Object);

        Assert.Equal(200, response.StatusCode);
        using var document = JsonDocument.Parse(response.Body);
        var root = document.RootElement;
        Assert.Equal(new[] { "participants", "total" }, root.EnumerateObject().Select(property => property.Name));
        Assert.Equal(15.00m, root.GetProperty("total").GetDecimal());
        var participant = Assert.Single(root.GetProperty("participants").EnumerateArray());
        Assert.Equal("session-remaining", participant.GetProperty("id").GetString());
        Assert.Equal(15.00m, participant.GetProperty("total").GetDecimal());
        Assert.DoesNotContain(
            root.GetProperty("participants").EnumerateArray(),
            item => item.GetProperty("id").GetString() == "session-deleted");
        _service.Verify(
            service => service.DeleteGuestSessionAsync("owner-1", "group-1", "session-deleted"),
            Times.Once);
    }

    [Fact]
    public async Task HandleRequest_WithoutUserClaim_ReturnsUnauthorizedWithoutCallingService()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                ["groupReservationId"] = "group-1",
                ["guestSessionId"] = "session-1"
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext()
        };

        var response = await new DeleteGuestSessionHandler(_service.Object)
            .HandleRequest(request, _context.Object);

        Assert.Equal(401, response.StatusCode);
        _service.Verify(
            service => service.DeleteGuestSessionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleRequest_WhenGuestSessionDoesNotExist_ReturnsNotFound()
    {
        _service.Setup(service => service.DeleteGuestSessionAsync("owner-1", "group-1", "missing-session"))
            .ThrowsAsync(new GuestSessionNotFoundException("Guest session was not found."));

        var response = await new DeleteGuestSessionHandler(_service.Object)
            .HandleRequest(AuthorizedRequest(guestSessionId: "missing-session"), _context.Object);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_WhenSessionBelongsToAnotherGroup_ReturnsBadRequest()
    {
        _service.Setup(service => service.DeleteGuestSessionAsync("owner-1", "group-1", "session-1"))
            .ThrowsAsync(new ValidationException("Guest session does not belong to this group reservation."));

        var response = await new DeleteGuestSessionHandler(_service.Object)
            .HandleRequest(AuthorizedRequest(), _context.Object);

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_WhenDeletingOwnerSession_ReturnsBadRequest()
    {
        _service.Setup(service => service.DeleteGuestSessionAsync("owner-1", "group-1", "session-1"))
            .ThrowsAsync(new ValidationException("The reservation owner's session cannot be deleted."));

        var response = await new DeleteGuestSessionHandler(_service.Object)
            .HandleRequest(AuthorizedRequest(), _context.Object);

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_WhenServiceForbidsNonOwner_ReturnsForbidden()
    {
        _service.Setup(service => service.DeleteGuestSessionAsync("other-user", "group-1", "session-1"))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));

        var response = await new DeleteGuestSessionHandler(_service.Object)
            .HandleRequest(AuthorizedRequest(userId: "other-user"), _context.Object);

        Assert.Equal(403, response.StatusCode);
        _service.Verify(
            service => service.DeleteGuestSessionAsync("other-user", "group-1", "session-1"),
            Times.Once);
    }

    [Fact]
    public async Task HandleRequest_WhenGroupReservationIsNotOpen_ReturnsConflict()
    {
        _service.Setup(service => service.DeleteGuestSessionAsync("owner-1", "group-1", "session-1"))
            .ThrowsAsync(new GroupReservationNotOpenException());

        var response = await new DeleteGuestSessionHandler(_service.Object)
            .HandleRequest(AuthorizedRequest(), _context.Object);

        Assert.Equal(409, response.StatusCode);
        _service.Verify(
            service => service.DeleteGuestSessionAsync("owner-1", "group-1", "session-1"),
            Times.Once);
    }

    private static APIGatewayProxyRequest AuthorizedRequest(
        string userId = "owner-1",
        string guestSessionId = "session-1") => new()
    {
        PathParameters = new Dictionary<string, string>
        {
            ["groupReservationId"] = "group-1",
            ["guestSessionId"] = guestSessionId
        },
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = new APIGatewayCustomAuthorizerContext
            {
                Claims = new Dictionary<string, string> { ["sub"] = userId }
            }
        }
    };
}
