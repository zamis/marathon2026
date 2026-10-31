using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservations;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.GroupReservations;
using Moq;

namespace APIHandler.Tests.Services;

public class SubmitGroupReservationServiceTests
{
    private const string GroupId = "group-1";
    private const string ReservationId = "reservation-1";
    private const string OwnerId = "owner-1";

    private readonly Mock<IGroupReservationRepository> _groups = new();
    private readonly Mock<IReservationRepository> _reservations = new();
    private readonly Mock<IGuestSessionRepository> _sessions = new();
    private readonly Mock<IOrderRepository> _orders = new();
    private readonly GroupReservationService _service;

    public SubmitGroupReservationServiceTests()
    {
        _service = new GroupReservationService(
            _groups.Object,
            Mock.Of<APIHandler.Repositories.Tables.ITableRepository>(),
            _reservations.Object,
            _sessions.Object,
            Mock.Of<APIHandler.Services.Auth.IUserService>(),
            Mock.Of<APIHandler.Services.Locations.ILocationService>(),
            _orders.Object);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_ConsolidatesSubmittedGuestDishesAndPersistsStatuses()
    {
        var group = new GroupReservation
        {
            GroupReservationId = GroupId,
            ReservationId = ReservationId,
            ReservationToken = "token",
            Status = GroupReservationStatus.Open
        };
        var order = CreateOrder();
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync(GroupId)).ReturnsAsync(group);
        _reservations.Setup(repository => repository.GetReservationByIdAsync(ReservationId))
            .ReturnsAsync(CreateReservation(OwnerId));
        _orders.Setup(repository => repository.GetOrderByReservationIdAsync(ReservationId)).ReturnsAsync(order);
        _orders.Setup(repository => repository.TrySaveSubmittedOrderAndGroupReservationAsync(
                It.IsAny<Order>(), It.IsAny<GroupReservation>()))
            .ReturnsAsync(true);
        _sessions.Setup(repository => repository.GetAllForSubmissionByGroupReservationIdAsync(GroupId)).ReturnsAsync(
        [
            new GuestSession
            {
                Id = "owner-session",
                GroupReservationId = GroupId,
                GuestName = "Owner",
                IsOwnerSession = true,
                IsSubmitted = true,
                Dishes = [new OrderItem { DishId = "owner-only-dish", Quantity = 99 }]
            },
            new GuestSession
            {
                Id = "submitted-1",
                GroupReservationId = GroupId,
                GuestName = "Guest 1",
                IsSubmitted = true,
                Dishes =
                [
                    new OrderItem { DishId = "dish-a", Quantity = 2 },
                    new OrderItem { DishId = "dish-b", Quantity = 1 }
                ]
            },
            new GuestSession
            {
                Id = "submitted-2",
                GroupReservationId = GroupId,
                GuestName = "Guest 2",
                IsSubmitted = true,
                Dishes =
                [
                    new OrderItem { DishId = "dish-a", Quantity = 3 },
                    new OrderItem { DishId = "dish-c", Quantity = 4 }
                ]
            },
            new GuestSession
            {
                Id = "not-submitted",
                GroupReservationId = GroupId,
                GuestName = "Guest 3",
                IsSubmitted = false,
                Dishes = [new OrderItem { DishId = "dish-a", Quantity = 100 }]
            }
        ]);

        var result = await _service.SubmitGroupReservationAsync(OwnerId, GroupId, ReservationId);

        Assert.Equal(
            new Dictionary<string, int>
            {
                ["dish-a"] = 5,
                ["dish-b"] = 1,
                ["dish-c"] = 4
            },
            result.ToDictionary(item => item.DishId, item => item.Quantity));
        Assert.Equal(
            new Dictionary<string, int>
            {
                ["dish-a"] = 5,
                ["dish-b"] = 1,
                ["dish-c"] = 4
            },
            order.Dishes.ToDictionary(item => item.DishId, item => item.Quantity));
        Assert.All(order.Dishes, item => Assert.Equal(OrderItemState.Pending, item.State));
        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        Assert.Equal(GroupReservationStatus.Open, group.Status);
        _orders.Verify(repository => repository.TrySaveSubmittedOrderAndGroupReservationAsync(
            order,
            It.Is<GroupReservation>(submittedGroup =>
                submittedGroup.GroupReservationId == GroupId &&
                submittedGroup.SubmissionVersion == group.SubmissionVersion)), Times.Once);
        _orders.Verify(repository => repository.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
        _groups.Verify(repository => repository.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_MissingGroupThrowsNotFound()
    {
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync(GroupId))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(
            () => _service.SubmitGroupReservationAsync(OwnerId, GroupId, ReservationId));

        _reservations.Verify(repository => repository.GetReservationByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_MissingReservationThrowsNotFound()
    {
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync(GroupId))
            .ReturnsAsync(CreateGroup());
        _reservations.Setup(repository => repository.GetReservationByIdAsync(ReservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(
            () => _service.SubmitGroupReservationAsync(OwnerId, GroupId, ReservationId));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_NonOwnerIsForbidden()
    {
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync(GroupId))
            .ReturnsAsync(CreateGroup());
        _reservations.Setup(repository => repository.GetReservationByIdAsync(ReservationId))
            .ReturnsAsync(CreateReservation("someone-else"));

        await Assert.ThrowsAsync<ActionForbiddenException>(
            () => _service.SubmitGroupReservationAsync(OwnerId, GroupId, ReservationId));

        _orders.Verify(repository => repository.GetOrderByReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_MissingOrderThrowsValidationException()
    {
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync(GroupId))
            .ReturnsAsync(CreateGroup());
        _reservations.Setup(repository => repository.GetReservationByIdAsync(ReservationId))
            .ReturnsAsync(CreateReservation(OwnerId));
        _orders.Setup(repository => repository.GetOrderByReservationIdAsync(ReservationId))
            .ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitGroupReservationAsync(OwnerId, GroupId, ReservationId));

        _sessions.Verify(repository => repository.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_NoSubmittedGuestSessionThrowsValidationException()
    {
        SetupExistingOrderAndSessions(
            new GuestSession
            {
                Id = "owner-session",
                GroupReservationId = GroupId,
                GuestName = "Owner",
                IsOwnerSession = true,
                IsSubmitted = true,
                Dishes = [new OrderItem { DishId = "owner-dish", Quantity = 1 }]
            },
            new GuestSession
            {
                Id = "guest-session",
                GroupReservationId = GroupId,
                GuestName = "Guest",
                IsSubmitted = false,
                Dishes = [new OrderItem { DishId = "guest-dish", Quantity = 2 }]
            });

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitGroupReservationAsync(OwnerId, GroupId, ReservationId));

        _orders.Verify(repository => repository.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
        _groups.Verify(repository => repository.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_SubmittedSessionsWithoutDishesThrowsValidationException()
    {
        SetupExistingOrderAndSessions(
            new GuestSession
            {
                Id = "guest-session",
                GroupReservationId = GroupId,
                GuestName = "Guest",
                IsSubmitted = true,
                Dishes = []
            });

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.SubmitGroupReservationAsync(OwnerId, GroupId, ReservationId));

        _orders.Verify(repository => repository.TrySaveSubmittedOrderAndGroupReservationAsync(
            It.IsAny<Order>(), It.IsAny<GroupReservation>()), Times.Never);
        _groups.Verify(repository => repository.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_VersionConflictRetriesWithFreshGroupAndIncludesNewGuestCart()
    {
        GroupReservation initialGroup = CreateGroup();
        GroupReservation firstAttemptGroup = CreateGroup();
        GroupReservation retriedGroup = CreateGroup();
        firstAttemptGroup.SubmissionVersion = 4;
        retriedGroup.SubmissionVersion = 5;

        _groups.SetupSequence(repository => repository.GetGroupReservationByIdAsync(GroupId))
            .ReturnsAsync(initialGroup)
            .ReturnsAsync(firstAttemptGroup)
            .ReturnsAsync(retriedGroup);
        _reservations.Setup(repository => repository.GetReservationByIdAsync(ReservationId))
            .ReturnsAsync(CreateReservation(OwnerId));
        Order order = CreateOrder();
        _orders.Setup(repository => repository.GetOrderByReservationIdAsync(ReservationId))
            .ReturnsAsync(order);

        var existingGuest = SubmittedGuest("guest-1", "dish-a", 2);
        var newlySubmittedGuest = SubmittedGuest("guest-2", "dish-b", 3);
        _sessions.SetupSequence(repository =>
                repository.GetAllForSubmissionByGroupReservationIdAsync(GroupId))
            .ReturnsAsync([existingGuest])
            .ReturnsAsync([existingGuest, newlySubmittedGuest]);

        GroupReservation? firstTryGroup = null;
        GroupReservation? retryGroup = null;
        List<OrderItem>? firstTryDishes = null;
        List<OrderItem>? retryDishes = null;
        int saveAttempt = 0;
        _orders.Setup(repository => repository.TrySaveSubmittedOrderAndGroupReservationAsync(
                It.IsAny<Order>(), It.IsAny<GroupReservation>()))
            .Returns((Order submittedOrder, GroupReservation submittedGroup) =>
            {
                saveAttempt++;
                if (saveAttempt == 1)
                {
                    firstTryDishes = submittedOrder.Dishes.ToList();
                    firstTryGroup = submittedGroup;
                    return Task.FromResult(false);
                }

                retryDishes = submittedOrder.Dishes.ToList();
                retryGroup = submittedGroup;
                return Task.FromResult(true);
            });

        List<GroupReservationDishQuantity> result =
            await _service.SubmitGroupReservationAsync(OwnerId, GroupId, ReservationId);

        Assert.Equal(4, firstTryGroup!.SubmissionVersion);
        Assert.Equal(5, retryGroup!.SubmissionVersion);
        Assert.Equal(new Dictionary<string, int> { ["dish-a"] = 2 }, ToDishQuantities(firstTryDishes!));
        Assert.Equal(new Dictionary<string, int>
        {
            ["dish-a"] = 2,
            ["dish-b"] = 3
        }, ToDishQuantities(retryDishes!));
        Assert.Equal(new Dictionary<string, int>
        {
            ["dish-a"] = 2,
            ["dish-b"] = 3
        }, result.ToDictionary(item => item.DishId, item => item.Quantity));
        _sessions.Verify(repository => repository.GetAllForSubmissionByGroupReservationIdAsync(GroupId), Times.Exactly(2));
        _orders.Verify(repository => repository.TrySaveSubmittedOrderAndGroupReservationAsync(
            It.IsAny<Order>(), It.IsAny<GroupReservation>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SubmitGroupReservationAsync_OrderVersionConflictRetriesWithFreshOrderVersion()
    {
        _groups.SetupSequence(repository => repository.GetGroupReservationByIdAsync(GroupId))
            .ReturnsAsync(CreateGroup())
            .ReturnsAsync(CreateGroup())
            .ReturnsAsync(CreateGroup());
        _reservations.Setup(repository => repository.GetReservationByIdAsync(ReservationId))
            .ReturnsAsync(CreateReservation(OwnerId));
        var staleOrder = CreateOrder();
        staleOrder.Version = 6;
        var latestOrder = CreateOrder();
        latestOrder.Version = 7;
        _orders.SetupSequence(repository => repository.GetOrderByReservationIdAsync(ReservationId))
            .ReturnsAsync(staleOrder)
            .ReturnsAsync(latestOrder);
        _sessions.Setup(repository => repository.GetAllForSubmissionByGroupReservationIdAsync(GroupId))
            .ReturnsAsync([SubmittedGuest("guest-1", "dish-a", 2)]);

        var observedVersions = new List<long>();
        int saveAttempt = 0;
        _orders.Setup(repository => repository.TrySaveSubmittedOrderAndGroupReservationAsync(
                It.IsAny<Order>(), It.IsAny<GroupReservation>()))
            .Returns((Order order, GroupReservation _) =>
            {
                observedVersions.Add(order.Version);
                saveAttempt++;
                return Task.FromResult(saveAttempt == 2);
            });

        var result = await _service.SubmitGroupReservationAsync(OwnerId, GroupId, ReservationId);

        Assert.Equal(new long[] { 6, 7 }, observedVersions);
        Assert.Equal(new Dictionary<string, int> { ["dish-a"] = 2 },
            result.ToDictionary(item => item.DishId, item => item.Quantity));
        _orders.Verify(repository => repository.GetOrderByReservationIdAsync(ReservationId), Times.Exactly(2));
        _orders.Verify(repository => repository.TrySaveSubmittedOrderAndGroupReservationAsync(
            It.IsAny<Order>(), It.IsAny<GroupReservation>()), Times.Exactly(2));
    }

    private void SetupExistingOrderAndSessions(params GuestSession[] sessions)
    {
        _groups.Setup(repository => repository.GetGroupReservationByIdAsync(GroupId))
            .ReturnsAsync(CreateGroup());
        _reservations.Setup(repository => repository.GetReservationByIdAsync(ReservationId))
            .ReturnsAsync(CreateReservation(OwnerId));
        _orders.Setup(repository => repository.GetOrderByReservationIdAsync(ReservationId))
            .ReturnsAsync(CreateOrder());
        _sessions.Setup(repository => repository.GetAllForSubmissionByGroupReservationIdAsync(GroupId))
            .ReturnsAsync(sessions.ToList());
    }

    private static GuestSession SubmittedGuest(string id, string dishId, int quantity) => new()
    {
        Id = id,
        GroupReservationId = GroupId,
        GuestName = id,
        IsSubmitted = true,
        Dishes = [new OrderItem { DishId = dishId, Quantity = quantity }]
    };

    private static Dictionary<string, int> ToDishQuantities(IEnumerable<OrderItem> items) =>
        items.ToDictionary(item => item.DishId, item => item.Quantity);

    private static GroupReservation CreateGroup() => new()
    {
        GroupReservationId = GroupId,
        ReservationId = ReservationId,
        ReservationToken = "token",
        Status = GroupReservationStatus.Open
    };

    private static Reservation CreateReservation(string ownerId) => new()
    {
        Id = ReservationId,
        TableId = "table-1",
        CustomerId = ownerId,
        WaiterId = "waiter-1"
    };

    private static Order CreateOrder() => new()
    {
        OrderId = "order-1",
        CustomerId = OwnerId,
        ReservationId = ReservationId,
        ReservationAddress = "Address",
        ReservationTime = "Time",
        Dishes = [new OrderItem { DishId = "old-dish", Quantity = 20 }],
        Status = OrderStatus.Created
    };
}
