using Amazon.CognitoIdentityProvider.Model;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GuestSessions;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Services;

public class GetGroupReservationSessionsServiceTests
{
    private const string GroupId = "group-1";
    private const string OwnerId = "owner-1";

    private readonly Mock<IGuestSessionRepository> _guestSessions = new();
    private readonly Mock<IGroupReservationRepository> _groups = new();
    private readonly Mock<IDishRepository> _dishes = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly GuestSessionService _service;

    public GetGroupReservationSessionsServiceTests()
    {
        _service = new GuestSessionService(
            _groups.Object,
            _guestSessions.Object,
            _dishes.Object,
            _reservations.Object,
            _users.Object,
            Mock.Of<APIHandler.Repositories.Tables.ITableRepository>());
    }

    [Fact]
    public async Task GetGroupReservationSessionsAsync_OwnerReceivesParticipantDetailsAndExactTotals()
    {
        SetupReservationOwner(OwnerId);
        var sessions = new List<GuestSession>
        {
            new GuestSession
            {
                Id = "session-owner",
                GroupReservationId = GroupId,
                GuestName = "Host",
                IsOwnerSession = true,
                IsSubmitted = true,
                Dishes =
                [
                    new OrderItem { DishId = "coffee", Quantity = 2 },
                    new OrderItem { DishId = "pasta", Quantity = 1 }
                ]
            },
            new GuestSession
            {
                Id = "session-guest",
                GroupReservationId = GroupId,
                GuestName = "Mina",
                IsOwnerSession = false,
                IsSubmitted = false,
                Dishes =
                [
                    new OrderItem { DishId = "pasta", Quantity = 3 },
                    new OrderItem { DishId = "soup", Quantity = 2 }
                ]
            }
        };
        var dishes = new[]
        {
            new Dish { Id = "coffee", Name = "Coffee", Price = 2.35m },
            new Dish { Id = "pasta", Name = "Pasta", Price = 10.00m },
            new Dish { Id = "soup", Name = "Soup", Price = 4.20m }
        };
        _guestSessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync(sessions);
        _dishes.Setup(repository => repository.GetDishesByIdsAsync(
                It.Is<IEnumerable<string>>(ids => ids.Count() == 3 && ids.ToHashSet().SetEquals(new[] { "coffee", "pasta", "soup" }))))
            .ReturnsAsync(dishes);
        _users.Setup(repository => repository.GetUserByIdAsync(OwnerId))
            .ReturnsAsync(new UserType
            {
                Attributes = [new AttributeType { Name = "given_name", Value = "Host" }]
            });

        var response = await _service.GetGroupReservationSessionsAsync(OwnerId, GroupId);

        var participants = response.Participants.ToArray();
        Assert.Equal(2, participants.Length);
        Assert.Equal("session-owner", participants[0].Id);
        Assert.Equal(ParticipantType.Owner, participants[0].Type);
        Assert.Equal("Host", participants[0].Name);
        Assert.Equal(14.70m, participants[0].Total);
        Assert.True(participants[0].IsSubmitted);
        Assert.Collection(participants[0].Items,
            item =>
            {
                Assert.Equal(new GroupSessionDish("coffee", "Coffee", 2.35m), item.Dish);
                Assert.Equal(2, item.Quantity);
            },
            item =>
            {
                Assert.Equal(new GroupSessionDish("pasta", "Pasta", 10.00m), item.Dish);
                Assert.Equal(1, item.Quantity);
            });

        Assert.Equal("session-guest", participants[1].Id);
        Assert.Equal(ParticipantType.Guest, participants[1].Type);
        Assert.Equal("Mina", participants[1].Name);
        Assert.Equal(38.40m, participants[1].Total);
        Assert.False(participants[1].IsSubmitted);
        Assert.Collection(participants[1].Items,
            item =>
            {
                Assert.Equal(new GroupSessionDish("pasta", "Pasta", 10.00m), item.Dish);
                Assert.Equal(3, item.Quantity);
            },
            item =>
            {
                Assert.Equal(new GroupSessionDish("soup", "Soup", 4.20m), item.Dish);
                Assert.Equal(2, item.Quantity);
            });
        Assert.Equal(53.10m, response.Total);
        _guestSessions.Verify(repository => repository.GetAllByGroupReservationIdAsync(GroupId), Times.Once);
        _dishes.Verify(repository => repository.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()), Times.Once);
    }

    [Fact]
    public async Task GetGroupReservationSessionsAsync_NoParticipants_ReturnsEmptyListAndZeroTotal()
    {
        SetupReservationOwner(OwnerId);
        _guestSessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync(new List<GuestSession>());
        _dishes.Setup(repository => repository.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(Array.Empty<Dish>());

        var response = await _service.GetGroupReservationSessionsAsync(OwnerId, GroupId);

        Assert.Empty(response.Participants);
        Assert.Equal(0m, response.Total);
        _dishes.Verify(repository => repository.GetDishesByIdsAsync(It.Is<IEnumerable<string>>(ids => !ids.Any())), Times.Once);
    }

    [Fact]
    public async Task GetGroupReservationSessionsAsync_MissingDishDataThrowsInsteadOfDroppingOrFabricatingItem()
    {
        SetupReservationOwner(OwnerId);
        _guestSessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync(
            [
                new GuestSession
                {
                    Id = "session-guest",
                    GroupReservationId = GroupId,
                    GuestName = "Mina",
                    IsOwnerSession = false,
                    Dishes = [new OrderItem { DishId = "missing-dish", Quantity = 2 }]
                }
            ]);
        _dishes.Setup(repository => repository.GetDishesByIdsAsync(
                It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "missing-dish" }))))
            .ReturnsAsync(Array.Empty<Dish>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetGroupReservationSessionsAsync(OwnerId, GroupId));

        Assert.Equal("Guest session 'session-guest' references missing dish 'missing-dish'.", exception.Message);
    }

    [Fact]
    public async Task GetGroupReservationSessionsAsync_OwnerParticipantUsesResolvedCognitoGivenName()
    {
        SetupReservationOwner(OwnerId);
        _guestSessions.Setup(repository => repository.GetAllByGroupReservationIdAsync(GroupId))
            .ReturnsAsync(
            [
                new GuestSession
                {
                    Id = "session-owner",
                    GroupReservationId = GroupId,
                    GuestName = "Owner",
                    IsOwnerSession = true,
                    Dishes = []
                }
            ]);
        _dishes.Setup(repository => repository.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(Array.Empty<Dish>());
        _users.Setup(repository => repository.GetUserByIdAsync(OwnerId))
            .ReturnsAsync(new UserType
            {
                Attributes = [new AttributeType { Name = "given_name", Value = "Alex" }]
            });

        var response = await _service.GetGroupReservationSessionsAsync(OwnerId, GroupId);

        var participant = Assert.Single(response.Participants);
        Assert.Equal(ParticipantType.Owner, participant.Type);
        Assert.Equal("Alex", participant.Name);
        _users.Verify(repository => repository.GetUserByIdAsync(OwnerId), Times.Once);
    }

    [Fact]
    public async Task GetGroupReservationSessionsAsync_NonOwnerIsForbiddenBeforeLoadingSessions()
    {
        SetupReservationOwner("somebody-else");

        var exception = await Assert.ThrowsAsync<ActionForbiddenException>(
            () => _service.GetGroupReservationSessionsAsync(OwnerId, GroupId));

        Assert.Equal("Only the reservation owner can perform this action.", exception.Message);
        _guestSessions.Verify(repository => repository.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
        _dishes.Verify(repository => repository.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task GetGroupReservationSessionsAsync_MissingGroupThrowsNotFound()
    {
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync(GroupId))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(
            () => _service.GetGroupReservationSessionsAsync(OwnerId, GroupId));
        _reservations.Verify(repository => repository.GetReservationByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetGroupReservationSessionsAsync_MissingParentReservationThrowsNotFound()
    {
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync(GroupId))
            .ReturnsAsync(new GroupReservation
            {
                GroupReservationId = GroupId,
                ReservationId = "reservation-1",
                ReservationToken = "token"
            });
        _reservations.Setup(repository => repository.GetReservationByIdAsync("reservation-1"))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => _service.GetGroupReservationSessionsAsync(OwnerId, GroupId));
        _guestSessions.Verify(repository => repository.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    private void SetupReservationOwner(string customerId)
    {
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync(GroupId))
            .ReturnsAsync(new GroupReservation
            {
                GroupReservationId = GroupId,
                ReservationId = "reservation-1",
                ReservationToken = "token"
            });
        _reservations.Setup(repository => repository.GetReservationByIdAsync("reservation-1"))
            .ReturnsAsync(new Reservation
            {
                Id = "reservation-1",
                TableId = "table-1",
                WaiterId = "waiter-1",
                CustomerId = customerId
            });
    }
}
