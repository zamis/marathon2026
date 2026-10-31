using APIHandler.Constants.Dishes;
using APIHandler.Constants.Orders;
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

public class ConditionalCartMutationTests
{
    private readonly Mock<IGroupReservationRepository> _groups = new();
    private readonly Mock<IGuestSessionRepository> _sessions = new();
    private readonly Mock<IDishRepository> _dishes = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<ITableRepository> _tables = new();
    private readonly GuestSessionService _service;

    public ConditionalCartMutationTests()
    {
        _service = new GuestSessionService(
            _groups.Object,
            _sessions.Object,
            _dishes.Object,
            _reservations.Object,
            Mock.Of<IUserRepository>(),
            _tables.Object);
    }

    [Fact]
    public async Task AddDishAsync_VersionConflictReloadsBothVersionsAndDoesNotApplyQuantityTwice()
    {
        GuestSession initialRead = CreateSession();
        GuestSession firstAttempt = CreateSession(sessionVersion: 4, quantity: 2);
        GuestSession retryAttempt = CreateSession(sessionVersion: 5, quantity: 2);
        GroupReservation locationGroup = CreateGroup(7);
        GroupReservation firstAttemptGroup = CreateGroup(8);
        GroupReservation retryGroup = CreateGroup(9);
        var dish = new Dish { Id = "dish-1", State = DishState.Available, LocationId = "location-1" };

        _sessions.SetupSequence(repository => repository.GetByGuestSessionId("session-1"))
            .ReturnsAsync(initialRead)
            .ReturnsAsync(firstAttempt)
            .ReturnsAsync(retryAttempt);
        _groups.SetupSequence(repository => repository.GetGroupReservationByIdAsync("group-1"))
            .ReturnsAsync(locationGroup) // Initial session validation
            .ReturnsAsync(locationGroup) // Location lookup
            .ReturnsAsync(firstAttemptGroup) // First attempt session validation
            .ReturnsAsync(firstAttemptGroup) // First conditional update
            .ReturnsAsync(retryGroup) // Retry session validation
            .ReturnsAsync(retryGroup); // Retry conditional update
        _reservations.Setup(repository => repository.GetReservationByIdAsync("reservation-1"))
            .ReturnsAsync(new Reservation
            {
                Id = "reservation-1",
                TableId = "table-1",
                WaiterId = "waiter-1"
            });
        _tables.Setup(repository => repository.GetTableAsync("table-1"))
            .ReturnsAsync(new Table { Id = "table-1", LocationId = "location-1" });
        _dishes.Setup(repository => repository.GetDishesByLocationIdAsync("location-1"))
            .ReturnsAsync([dish]);

        var attemptedCarts = new List<(long SessionVersion, long GroupVersion, int Quantity)>();
        int updateAttempt = 0;
        _sessions.Setup(repository =>
                repository.TryUpdateCartAsync(It.IsAny<GuestSession>(), It.IsAny<GroupReservation>()))
            .Returns((GuestSession session, GroupReservation group) =>
            {
                attemptedCarts.Add((session.SessionVersion, group.SubmissionVersion, session.Dishes.Single().Quantity));
                updateAttempt++;
                return Task.FromResult(updateAttempt == 2);
            });

        await _service.AddDishAsync("session-1", "dish-1", 3);

        Assert.Equal(
            new[] { (4L, 8L, 5), (5L, 9L, 5) },
            attemptedCarts);
        _sessions.Verify(repository => repository.TryUpdateCartAsync(
            It.IsAny<GuestSession>(), It.IsAny<GroupReservation>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CartMutation_AfterSessionSubmissionIsRejected(bool removeDish)
    {
        var submittedSession = CreateSession(sessionVersion: 3, quantity: 2);
        submittedSession.IsSubmitted = true;
        var group = CreateGroup(5);
        _sessions.Setup(repository => repository.GetByGuestSessionId(submittedSession.Id))
            .ReturnsAsync(submittedSession);
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync(group.GroupReservationId))
            .ReturnsAsync(group);

        if (removeDish)
        {
            await Assert.ThrowsAsync<GuestSessionSubmittedException>(
                () => _service.RemoveDishAsync(submittedSession.Id, "dish-1", 1));
        }
        else
        {
            await Assert.ThrowsAsync<GuestSessionSubmittedException>(
                () => _service.AddDishAsync(submittedSession.Id, "dish-1", 1));
        }

        Assert.Equal(2, submittedSession.Dishes.Single().Quantity);
        _sessions.Verify(repository => repository.TryUpdateCartAsync(
            It.IsAny<GuestSession>(), It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task SubmitCartAsync_WhenConflictReloadsAsSubmittedDoesNotSubmitAgain()
    {
        GuestSession firstRead = CreateSession(quantity: 2);
        GuestSession submittedOnRetry = CreateSession(quantity: 2);
        submittedOnRetry.IsSubmitted = true;
        var group = CreateGroup(6);
        _sessions.SetupSequence(repository => repository.GetByGuestSessionId("session-1"))
            .ReturnsAsync(firstRead)
            .ReturnsAsync(submittedOnRetry);
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync("group-1"))
            .ReturnsAsync(group);
        _dishes.Setup(repository => repository.GetDishesByIdsAsync(
                It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([new Dish { Id = "dish-1", State = DishState.Available, LocationId = "location-1" }]);
        _sessions.Setup(repository => repository.TrySubmitCartAsync(firstRead, group))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<GuestSessionSubmittedException>(
            () => _service.SubmitCartAsync("session-1"));

        _sessions.Verify(repository => repository.TrySubmitCartAsync(
            It.IsAny<GuestSession>(), It.IsAny<GroupReservation>()), Times.Once);
    }

    private static GuestSession CreateSession(long sessionVersion = 0, int quantity = 1) => new()
    {
        Id = "session-1",
        GroupReservationId = "group-1",
        GuestName = "Guest",
        Dishes = [new OrderItem { DishId = "dish-1", Quantity = quantity, State = OrderItemState.Pending }],
        SessionVersion = sessionVersion
    };

    private static GroupReservation CreateGroup(long submissionVersion) => new()
    {
        GroupReservationId = "group-1",
        ReservationId = "reservation-1",
        ReservationToken = "token",
        Status = GroupReservationStatus.Open,
        SubmissionVersion = submissionVersion
    };
}
