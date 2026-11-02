namespace Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

public sealed record GetGroupInviteTokenResponse(string Token);

public sealed record OwnerGuestSessionResponse(string GuestSessionId);

public sealed record GroupInviteResponse(
    string GroupReservationId,
    string LocationId,
    string RestaurantName,
    string Address,
    DateTime StartTime,
    DateTime EndTime,
    int GuestCount,
    int TableNumber,
    string InviterName);

public sealed record CreateGuestSessionRequest(string GroupReservationId, string GuestName);

public sealed record CreateGuestSessionResponse(string SessionId, string SessionToken);

public sealed record UpdateGuestSessionDishRequest(string DishId, int Quantity);

public sealed record GroupSessionsResponse(IReadOnlyList<GroupSessionParticipant> Participants, decimal Total);

public sealed record GroupSessionParticipant(
    string? Id,
    string Type,
    string Name,
    IReadOnlyList<GroupSessionOrderItem> Items,
    decimal Total,
    bool IsSubmitted);

public sealed record GroupSessionOrderItem(GroupSessionDish Dish, int Quantity);

public sealed record GroupSessionDish(string Id, string Name, decimal Price);

public sealed record GroupReservationStatusResponse(int CurrentParticipants, int MaxCapacity, string Status);

public sealed record SubmitGroupOrderRequest(string ReservationId);

public sealed record SubmittedGroupDish(string DishId, int Quantity);
