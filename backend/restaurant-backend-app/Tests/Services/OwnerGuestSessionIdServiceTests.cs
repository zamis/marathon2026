using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Services;

public class OwnerGuestSessionIdServiceTests
{
    private const string ReservationId = "reservation-1";
    private const string GroupId = "group-1";
    private const string OwnerId = "owner-1";

    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IGroupReservationRepository> _groups = new();
    private readonly Mock<IGuestSessionRepository> _sessions = new();
    private readonly GuestSessionService _service;

    public OwnerGuestSessionIdServiceTests()
    {
        _service = new GuestSessionService(
            _groups.Object,
            _sessions.Object,
            Mock.Of<IDishRepository>(),
            _reservations.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<ITableRepository>());
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_ReturnsMatchingOwnersSessionId()
    {
        SetupReservation(CreateReservation(OwnerId));
        _groups.Setup(repository => repository.GetByReservationIdAsync(ReservationId))
            .ReturnsAsync(CreateGroup());
        _sessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync(
            [
                new GuestSession
                {
                    Id = "another-users-owner-session",
                    GroupReservationId = GroupId,
                    GuestName = "Owner",
                    OwnerUserId = "other-user",
                    IsOwnerSession = true,
                    Dishes = []
                },
                new GuestSession
                {
                    Id = "guest-session",
                    GroupReservationId = GroupId,
                    GuestName = "Guest",
                    OwnerUserId = OwnerId,
                    IsOwnerSession = false,
                    Dishes = []
                },
                new GuestSession
                {
                    Id = "owner-session-123",
                    GroupReservationId = GroupId,
                    GuestName = "Owner",
                    OwnerUserId = OwnerId,
                    IsOwnerSession = true,
                    Dishes = []
                }
            ]);

        var sessionId = await _service.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId);

        Assert.Equal("owner-session-123", sessionId);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_MissingReservationThrowsNotFound()
    {
        _reservations.Setup(repository => repository.GetReservationByIdAsync(ReservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => _service.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId));

        _groups.Verify(repository => repository.GetByReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_NonOwnerIsForbidden()
    {
        SetupReservation(CreateReservation("someone-else"));

        await Assert.ThrowsAsync<ActionForbiddenException>(
            () => _service.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId));

        _groups.Verify(repository => repository.GetByReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_MissingGroupThrowsGuestSessionNotFound()
    {
        SetupReservation(CreateReservation(OwnerId));
        _groups.Setup(repository => repository.GetByReservationIdAsync(ReservationId))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GuestSessionNotFoundException>(
            () => _service.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId));
    }

    [Fact]
    public async Task GetOwnerGuestSessionIdAsync_MissingOwnersSessionThrowsGuestSessionNotFound()
    {
        SetupReservation(CreateReservation(OwnerId));
        _groups.Setup(repository => repository.GetByReservationIdAsync(ReservationId))
            .ReturnsAsync(CreateGroup());
        _sessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync(
            [
                new GuestSession
                {
                    Id = "guest-session",
                    GroupReservationId = GroupId,
                    GuestName = "Guest",
                    OwnerUserId = OwnerId,
                    IsOwnerSession = false,
                    Dishes = []
                }
            ]);

        await Assert.ThrowsAsync<GuestSessionNotFoundException>(
            () => _service.GetOwnerGuestSessionIdAsync(OwnerId, ReservationId));
    }

    private void SetupReservation(Reservation reservation) =>
        _reservations.Setup(repository => repository.GetReservationByIdAsync(ReservationId))
            .ReturnsAsync(reservation);

    private static Reservation CreateReservation(string ownerId) => new()
    {
        Id = ReservationId,
        TableId = "table-1",
        CustomerId = ownerId,
        WaiterId = "waiter-1"
    };

    private static GroupReservation CreateGroup() => new()
    {
        GroupReservationId = GroupId,
        ReservationId = ReservationId,
        ReservationToken = "token"
    };
}
