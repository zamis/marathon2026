using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Handlers.Reservations;
using APIHandler.Models.GroupReservations;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Handlers;

public class OwnerGroupReservationHandlersTests
{
    private readonly Mock<IGuestSessionService> _guestSessions = new();
    private readonly Mock<IGroupReservationService> _groupReservations = new();
    private readonly Mock<ILambdaContext> _context = new();

    public OwnerGroupReservationHandlersTests()
    {
        _context.Setup(context => context.Logger).Returns(Mock.Of<ILambdaLogger>());
    }

    [Fact]
    public async Task GetOwnerGuestSessionId_WithOwnerClaimReturnsGuestSessionIdResponse()
    {
        _guestSessions.Setup(service => service.GetOwnerGuestSessionIdAsync("owner-1", "reservation-1"))
            .ReturnsAsync("owner-session-1");

        var response = await new GetOwnerGuestSessionIdHandler(_guestSessions.Object)
            .HandleRequest(AuthorizedRequest("reservationId"), _context.Object);

        Assert.Equal(200, response.StatusCode);
        using var document = JsonDocument.Parse(response.Body);
        Assert.Equal("owner-session-1", document.RootElement.GetProperty("guestSessionId").GetString());
        _guestSessions.Verify(service => service.GetOwnerGuestSessionIdAsync("owner-1", "reservation-1"), Times.Once);
    }

    [Fact]
    public async Task GetOwnerGuestSessionId_WithoutUserClaimReturnsUnauthorized()
    {
        var request = AuthorizedRequest("reservationId");
        request.RequestContext = new APIGatewayProxyRequest.ProxyRequestContext();

        var response = await new GetOwnerGuestSessionIdHandler(_guestSessions.Object)
            .HandleRequest(request, _context.Object);

        Assert.Equal(401, response.StatusCode);
        _guestSessions.Verify(service => service.GetOwnerGuestSessionIdAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerGuestSessionId_WithoutReservationIdReturnsBadRequest()
    {
        var request = AuthorizedRequest("reservationId");
        request.PathParameters = new Dictionary<string, string>();

        var response = await new GetOwnerGuestSessionIdHandler(_guestSessions.Object)
            .HandleRequest(request, _context.Object);

        Assert.Equal(400, response.StatusCode);
        _guestSessions.Verify(service => service.GetOwnerGuestSessionIdAsync(
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerGuestSessionId_WhenCallerIsNotOwnerReturnsForbidden()
    {
        _guestSessions.Setup(service => service.GetOwnerGuestSessionIdAsync("other-user", "reservation-1"))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));

        var response = await new GetOwnerGuestSessionIdHandler(_guestSessions.Object)
            .HandleRequest(AuthorizedRequest("reservationId", "other-user"), _context.Object);

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task GetOwnerGuestSessionId_WhenReservationOrOwnerSessionIsMissingReturnsNotFound()
    {
        _guestSessions.Setup(service => service.GetOwnerGuestSessionIdAsync("owner-1", "reservation-1"))
            .ThrowsAsync(new ReservationNotFoundException("Reservation was not found."));
        var reservationResponse = await new GetOwnerGuestSessionIdHandler(_guestSessions.Object)
            .HandleRequest(AuthorizedRequest("reservationId"), _context.Object);
        Assert.Equal(404, reservationResponse.StatusCode);

        _guestSessions.Setup(service => service.GetOwnerGuestSessionIdAsync("owner-1", "reservation-1"))
            .ThrowsAsync(new GuestSessionNotFoundException("Owner session was not found."));
        var sessionResponse = await new GetOwnerGuestSessionIdHandler(_guestSessions.Object)
            .HandleRequest(AuthorizedRequest("reservationId"), _context.Object);
        Assert.Equal(404, sessionResponse.StatusCode);
    }

    [Fact]
    public async Task SubmitGroupReservation_WithOwnerClaimReturnsDishQuantities()
    {
        _groupReservations.Setup(service => service.SubmitGroupReservationAsync("owner-1", "group-1", "reservation-1"))
            .ReturnsAsync(
            [
                new GroupReservationDishQuantity("dish-1", 3),
                new GroupReservationDishQuantity("dish-2", 2)
            ]);

        var response = await new SubmitGroupReservationHandler(_groupReservations.Object)
            .HandleRequest(SubmitRequest(), _context.Object);

        Assert.Equal(200, response.StatusCode);
        using var document = JsonDocument.Parse(response.Body);
        Assert.Collection(document.RootElement.EnumerateArray(),
            item =>
            {
                Assert.Equal("dish-1", item.GetProperty("dishId").GetString());
                Assert.Equal(3, item.GetProperty("quantity").GetInt32());
            },
            item =>
            {
                Assert.Equal("dish-2", item.GetProperty("dishId").GetString());
                Assert.Equal(2, item.GetProperty("quantity").GetInt32());
            });
        _groupReservations.Verify(
            service => service.SubmitGroupReservationAsync("owner-1", "group-1", "reservation-1"),
            Times.Once);
    }

    [Fact]
    public async Task SubmitGroupReservation_WithoutUserClaimReturnsUnauthorized()
    {
        var request = SubmitRequest();
        request.RequestContext = new APIGatewayProxyRequest.ProxyRequestContext();

        var response = await new SubmitGroupReservationHandler(_groupReservations.Object)
            .HandleRequest(request, _context.Object);

        Assert.Equal(401, response.StatusCode);
        _groupReservations.Verify(
            service => service.SubmitGroupReservationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservation_WithoutReservationIdReturnsBadRequest()
    {
        var request = SubmitRequest();
        request.Body = "{}";

        var response = await new SubmitGroupReservationHandler(_groupReservations.Object)
            .HandleRequest(request, _context.Object);

        Assert.Equal(400, response.StatusCode);
        _groupReservations.Verify(
            service => service.SubmitGroupReservationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservation_WhenServiceReportsMissingResourcesOrValidationFailureMapsStatus()
    {
        _groupReservations.Setup(service => service.SubmitGroupReservationAsync("owner-1", "group-1", "reservation-1"))
            .ThrowsAsync(new GroupReservationNotFoundException());
        var groupResponse = await new SubmitGroupReservationHandler(_groupReservations.Object)
            .HandleRequest(SubmitRequest(), _context.Object);
        Assert.Equal(404, groupResponse.StatusCode);

        _groupReservations.Setup(service => service.SubmitGroupReservationAsync("owner-1", "group-1", "reservation-1"))
            .ThrowsAsync(new ReservationNotFoundException("Reservation was not found."));
        var reservationResponse = await new SubmitGroupReservationHandler(_groupReservations.Object)
            .HandleRequest(SubmitRequest(), _context.Object);
        Assert.Equal(404, reservationResponse.StatusCode);

        _groupReservations.Setup(service => service.SubmitGroupReservationAsync("owner-1", "group-1", "reservation-1"))
            .ThrowsAsync(new ValidationException("No submitted guest sessions."));
        var validationResponse = await new SubmitGroupReservationHandler(_groupReservations.Object)
            .HandleRequest(SubmitRequest(), _context.Object);
        Assert.Equal(400, validationResponse.StatusCode);
    }

    [Fact]
    public async Task SubmitGroupReservation_WhenCallerIsNotOwnerReturnsForbidden()
    {
        _groupReservations.Setup(service => service.SubmitGroupReservationAsync("other-user", "group-1", "reservation-1"))
            .ThrowsAsync(new ActionForbiddenException("Only the reservation owner can perform this action."));

        var response = await new SubmitGroupReservationHandler(_groupReservations.Object)
            .HandleRequest(SubmitRequest("other-user"), _context.Object);

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task SubmitGroupReservation_WhenAlreadySubmittedReturnsConflict()
    {
        _groupReservations.Setup(service => service.SubmitGroupReservationAsync("owner-1", "group-1", "reservation-1"))
            .ThrowsAsync(new GroupReservationSubmittedException());

        var response = await new SubmitGroupReservationHandler(_groupReservations.Object)
            .HandleRequest(SubmitRequest(), _context.Object);

        Assert.Equal(409, response.StatusCode);
    }

    private static APIGatewayProxyRequest AuthorizedRequest(string pathParameter, string userId = "owner-1") => new()
    {
        PathParameters = new Dictionary<string, string> { [pathParameter] = "reservation-1" },
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = new APIGatewayCustomAuthorizerContext
            {
                Claims = new Dictionary<string, string> { ["sub"] = userId }
            }
        }
    };

    private static APIGatewayProxyRequest SubmitRequest(string userId = "owner-1") => new()
    {
        PathParameters = new Dictionary<string, string> { ["groupReservationId"] = "group-1" },
        Body = """{"reservationId":"reservation-1"}""",
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = new APIGatewayCustomAuthorizerContext
            {
                Claims = new Dictionary<string, string> { ["sub"] = userId }
            }
        }
    };
}
