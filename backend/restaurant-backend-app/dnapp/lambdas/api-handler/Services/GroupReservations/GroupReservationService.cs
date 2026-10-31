using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Models.GuestReservation;
using APIHandler.Models.GroupReservations;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Services.GroupReservations;

public class GroupReservationService(
    IGroupReservationRepository groupReservationRepository,
    ITableRepository tableRepository,
    IReservationRepository reservationRepository,
    IGuestSessionRepository guestSessionRepository,
    IUserService userService,
    ILocationService locationService,
    IOrderRepository orderRepository) : IGroupReservationService
{
    private const string DefaultRestaurantName = "Green & Tasty";

    public async Task<GetGroupReservationResponse> GetGroupReservationByTokenAsync(string token)
    {
        GroupReservation groupReservation = await groupReservationRepository.GetGroupReservationByTokenAsync(token)
            ?? throw new GroupReservationNotFoundException();

        if (groupReservation.Status != GroupReservationStatus.Open)
            throw new GroupReservationExpiredException();

        Task<Reservation?> reservationTask = reservationRepository.GetReservationByIdAsync(groupReservation.ReservationId);
        Task<int> countTask = guestSessionRepository.CountByGroupReservationId(groupReservation.GroupReservationId);

        Reservation reservation = await reservationTask ?? throw new ValidationException("Associated reservation not found.");
        int joinedGuestsCount = await countTask;

        if (joinedGuestsCount + 1 >= reservation.GuestCount)
            throw new GuestLimitExceededException();

        Table table = await tableRepository.GetTableAsync(reservation.TableId)
            ?? throw new InvalidOperationException("Associated table not found.");

        Location? location = await locationService.GetLocationByIdAsync(table.LocationId);
        string restaurantName = DefaultRestaurantName;
        string address = location?.Address ?? "Unknown Address";

        string inviterName = "Unknown";
        if (!string.IsNullOrWhiteSpace(reservation.CustomerId))
        {
            inviterName = await userService.GetUserName(reservation.CustomerId) ?? "Unknown";
        }
        return new GetGroupReservationResponse(
            groupReservation.GroupReservationId,
            table.LocationId,
            restaurantName,
            address,
            reservation.StartTime,
            reservation.EndTime,
            reservation.GuestCount,
            table.TableNumber,
            inviterName);
    }

    public async Task<GroupReservation> GetGroupReservationAsync(string groupReservationId)
    {
        GroupReservation? groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId);
        return groupReservation ?? throw new InvalidOperationException($"Group reservation with ID {groupReservationId} not found.");
    }

    public async Task<GroupReservation> CreateGroupReservationAsync(string reservationId)
    {
        var groupReservation = new GroupReservation
        {
            GroupReservationId = Guid.NewGuid().ToString(),
            ReservationId = reservationId,
            ReservationToken = Guid.NewGuid().ToString("N"),
            Status = GroupReservationStatus.Open,
        };

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);

        Reservation? reservation = await reservationRepository.GetReservationByIdAsync(reservationId);
        if (!string.IsNullOrWhiteSpace(reservation?.CustomerId))
            await EnsureOwnerSessionAsync(groupReservation, reservation.CustomerId);

        return groupReservation;
    }

    private async Task EnsureOwnerSessionAsync(GroupReservation groupReservation, string userId)
    {
        var sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId);
        if (sessions.Any(session => session.IsOwnerSession && session.OwnerUserId == userId))
            return;

        await guestSessionRepository.SaveAsync(new GuestSession
        {
            Id = Guid.NewGuid().ToString(),
            GroupReservationId = groupReservation.GroupReservationId,
            GuestName = "Owner",
            Dishes = [],
            IsOwnerSession = true,
            OwnerUserId = userId
        });
    }

    public async Task CreateGroupReservationIfDoesNotExistAsync(string userId, string reservationId)
    {
        GroupReservation? group = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (group is not null)
        {
            await EnsureOwnerSessionAsync(group, userId);
            return;
        }

        await CreateGroupReservationAsync(reservationId);
    }

    public async Task UpdateGroupReservationStatusAsync(string groupReservationId, GroupReservationStatus status)
    {
        var groupReservation = await GetGroupReservationAsync(groupReservationId);

        groupReservation.Status = status;

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
    }

    public async Task DeleteGroupReservationAsync(string groupReservationId)
    {
        var groupReservation = await GetGroupReservationAsync(groupReservationId);

        await groupReservationRepository.DeleteGroupReservationAsync(groupReservation);
    }

    public async Task CloseGroupReservationAsync(string reservationId)
    {
        GroupReservation? groupReservation = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (groupReservation is null)
            return;

        while (true)
        {
            if (groupReservation.Status == GroupReservationStatus.Submitted)
                return;

            if (groupReservation.Status == GroupReservationStatus.Closed)
                return;

            if (await groupReservationRepository.TryCloseOpenGroupReservationAsync(groupReservation))
                return;

            groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservation.GroupReservationId);
            if (groupReservation is null || groupReservation.Status != GroupReservationStatus.Open)
                return;
        }
    }

    public async Task<GetGroupInviteTokenResponse> GetGroupReservationInviteTokenAsync(string reservationId)
    {
        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException($"Reservation {reservationId} was not found.");

        if (reservation.GuestCount < 2)
            throw new ValidationException("Retrieving invitation token for the reservation with only one guest is not permitted.");

        if (reservation.Status != ReservationStatus.Reserved)
            throw new GroupReservationExpiredException("The group reservation is expired.");

        GroupReservation groupReservation = (await groupReservationRepository.GetByReservationIdAsync(reservationId))!;

        if (groupReservation.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();

        if (groupReservation.Status == GroupReservationStatus.Closed)
            throw new GroupReservationExpiredException();

        return new GetGroupInviteTokenResponse(groupReservation.ReservationToken);
    }

    public async Task<List<GroupReservationDishQuantity>> SubmitGroupReservationAsync(
        string userId,
        string groupReservationId,
        string reservationId)
    {
        GroupReservation groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();

        Reservation reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException($"Reservation {reservationId} was not found.");

        if (!string.Equals(groupReservation.ReservationId, reservationId, StringComparison.Ordinal))
            throw new ValidationException("Reservation does not belong to this group reservation.");

        if (!string.Equals(reservation.CustomerId, userId, StringComparison.OrdinalIgnoreCase))
            throw new ActionForbiddenException("Only the reservation owner can perform this action.");

        if (groupReservation.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();

        if (groupReservation.Status == GroupReservationStatus.Closed)
            throw new GroupReservationExpiredException();

        while (true)
        {
            groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
                ?? throw new GroupReservationNotFoundException();

            if (groupReservation.Status == GroupReservationStatus.Submitted)
                throw new GroupReservationSubmittedException();

            if (groupReservation.Status == GroupReservationStatus.Closed)
                throw new GroupReservationExpiredException();

            Order order = await orderRepository.GetOrderByReservationIdAsync(reservationId)
                ?? throw new ValidationException($"Order for reservation '{reservationId}' was not found.");

            List<GuestSession> sessions =
                await guestSessionRepository.GetAllForSubmissionByGroupReservationIdAsync(groupReservationId);
            List<GuestSession> submittedSessions = sessions
                .Where(session => session.IsSubmitted && !session.IsOwnerSession)
                .ToList();

            if (submittedSessions.Count == 0)
                throw new ValidationException("There are no submitted guest sessions.");

            List<GroupReservationDishQuantity> consolidatedDishes = submittedSessions
                .SelectMany(session => session.Dishes ?? [])
                .GroupBy(item => item.DishId)
                .Select(group => new GroupReservationDishQuantity(group.Key, group.Sum(item => item.Quantity)))
                .ToList();

            if (consolidatedDishes.Count == 0)
                throw new ValidationException("Submitted guest sessions contain no dishes.");

            order.Dishes = consolidatedDishes.Select(dish => new OrderItem
            {
                DishId = dish.DishId,
                Quantity = dish.Quantity,
                State = APIHandler.Constants.Orders.OrderItemState.Pending
            }).ToList();
            order.Status = APIHandler.Entities.OrderStatus.SubmittedByCustomer;

            if (await orderRepository.TrySaveSubmittedOrderAndGroupReservationAsync(order, groupReservation))
                return consolidatedDishes;
        }
    }
}
