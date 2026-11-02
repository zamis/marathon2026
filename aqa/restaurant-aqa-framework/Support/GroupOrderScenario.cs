using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;

namespace Restaurant_Aqa_Framework.Support;

/// <summary>Per-scenario fixture state held only in the actor's Reqnroll memory.</summary>
public sealed class GroupOrderScenario
{
    public required RegisterRequest Owner { get; init; }
    public required string OwnerToken { get; init; }
    public required string ReservationId { get; init; }
    public required string GroupReservationId { get; init; }
    public required string InvitationToken { get; init; }
    public required ReservationResponse Reservation { get; init; }
    public required DishResponse Dish { get; init; }
    public Dictionary<string, GuestCart> Guests { get; } = new(StringComparer.Ordinal);
    public string? OtherCustomerToken { get; set; }
}

public sealed record GuestCart(string Id, string Token, string Name, int Quantity, bool IsSubmitted);
