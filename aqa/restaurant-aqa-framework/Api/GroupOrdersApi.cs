using Microsoft.Playwright;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Api;

/// <summary>Protocol boundary for group reservations and their guest sessions.</summary>
public sealed class GroupOrdersApi(IAPIRequestContext api) : ApiClientBase(api)
{
    public Task<IAPIResponse> GetInviteTokenAsync(string reservationId, string? bearerToken = null) =>
        GetAsync(Url($"/reservations/{reservationId}/invite-token"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> GetInviteAsync(string token) =>
        GetAsync(Url($"/guest/invites/{token}"));

    public Task<IAPIResponse> GetOwnerSessionAsync(string reservationId, string? bearerToken = null) =>
        GetAsync(Url($"/reservations/{reservationId}/guest-session-id"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> CreateGuestSessionAsync(CreateGuestSessionRequest request) =>
        Api.PostAsync(Url("/guest/sessions"), new APIRequestContextOptions
        {
            DataString = Serialize(request),
            Headers = Headers(),
        });

    public Task<IAPIResponse> AddDishAsync(string sessionId, string dishId, int quantity, string sessionToken) =>
        Api.PostAsync(Url($"/guest/sessions/{sessionId}/dishes"), new APIRequestContextOptions
        {
            DataString = Serialize(new UpdateGuestSessionDishRequest(dishId, quantity)),
            Headers = Headers(sessionToken),
        });

    public Task<IAPIResponse> SubmitGuestCartAsync(string sessionId, string sessionToken) =>
        Api.PostAsync(Url($"/guest/sessions/{sessionId}/submit"), new APIRequestContextOptions
        {
            DataString = "{}",
            Headers = Headers(sessionToken),
        });

    public Task<IAPIResponse> GetParticipantsAsync(string groupReservationId, string? bearerToken = null) =>
        GetAsync(Url($"/guest/groups/{groupReservationId}/sessions"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> GetStatusAsync(string groupReservationId, string? bearerToken = null) =>
        GetAsync(Url($"/guest/groups/{groupReservationId}/status"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> RemoveGuestAsync(
        string groupReservationId, string sessionId, string? bearerToken = null) =>
        DeleteAsync(Url($"/guest/groups/{groupReservationId}/sessions/{sessionId}"),
            new APIRequestContextOptions { Headers = Headers(bearerToken) });

    public Task<IAPIResponse> SubmitGroupAsync(
        string groupReservationId, string reservationId, string? bearerToken = null) =>
        Api.PostAsync(Url($"/group-reservations/{groupReservationId}/submit"),
            new APIRequestContextOptions
            {
                DataString = Serialize(new SubmitGroupOrderRequest(reservationId)),
                Headers = Headers(bearerToken),
            });
}
