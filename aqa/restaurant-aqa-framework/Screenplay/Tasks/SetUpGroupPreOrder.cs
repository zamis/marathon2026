using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Api.Dtos.Reservations;
using Restaurant_Aqa_Framework.Api.Dtos.Tables;
using Restaurant_Aqa_Framework.Api.Dtos.Dishes;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

/// <summary>Seeds an isolated location, menu, customer, reservation, group and order through supported APIs.</summary>
public sealed class SetUpGroupPreOrder(string name = "current") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        await actor.AttemptsToAsync(new SetUpLocation());
        await actor.AttemptsToAsync(new AddDish());
        await actor.AttemptsToAsync(new AddTable(seats: 6));

        var api = actor.UsingTheApi();
        var auth = new AuthApi(api);
        var owner = SampleDataFactory.RegisterRequest();
        await RequireSuccessAsync(await auth.RegisterAsync(owner), "customer registration");

        var signInResponse = await auth.SignInAsync(new SignInRequest(owner.Email, owner.Password));
        if (!signInResponse.Ok)
            throw new InvalidOperationException($"Owner sign-in failed with status {signInResponse.Status}.");
        var ownerToken = (await signInResponse.ReadAsAsync<SignInResponse>()).IdToken;

        var table = actor.Recall<TableResponse>();
        var reservationApi = new ReservationsApi(api);
        var reservationResponse = await reservationApi.CreateReservationAsync(
            SampleDataFactory.CreateReservationRequest(table.Id, DateTime.UtcNow.AddDays(2), guestCount: 6),
            ownerToken);
        await RequireSuccessAsync(reservationResponse, "group reservation creation");
        var reservation = await reservationResponse.ReadAsAsync<ReservationResponse>();

        actor.Recall<CleanupScope>().TrackDelete(
            $"reservation:{reservation.Id}",
            () => reservationApi.CancelReservationAsync(reservation.Id, ownerToken));

        var ordersApi = new OrdersApi(api);
        var orderResponse = await ordersApi.CreateOrderAsync(
            new CreateOrderRequest(reservation.Id, [new OrderItemRequest(actor.Recall<DishResponse>().Id, 1)]),
            ownerToken);
        await RequireSuccessAsync(orderResponse, "reservation order creation");
        actor.Recall<CleanupScope>().TrackDelete($"order:{reservation.Id}", () => ordersApi.DeleteOrderAsync(reservation.Id, ownerToken));

        var groupApi = new GroupOrdersApi(api);
        var inviteTokenResponse = await groupApi.GetInviteTokenAsync(reservation.Id, ownerToken);
        await RequireSuccessAsync(inviteTokenResponse, "invitation token retrieval");
        var invitationToken = (await inviteTokenResponse.ReadAsAsync<GetGroupInviteTokenResponse>()).Token;
        var inviteResponse = await groupApi.GetInviteAsync(invitationToken);
        await RequireSuccessAsync(inviteResponse, "group invitation lookup");
        var invite = await inviteResponse.ReadAsAsync<GroupInviteResponse>();

        actor.Remember(name, new GroupOrderScenario
        {
            Owner = owner,
            OwnerToken = ownerToken,
            ReservationId = reservation.Id,
            Reservation = reservation,
            GroupReservationId = invite.GroupReservationId,
            InvitationToken = invitationToken,
            Dish = actor.Recall<DishResponse>(),
        });
    }

    internal static async Task RequireSuccessAsync(Microsoft.Playwright.IAPIResponse response, string action)
    {
        if (!response.Ok)
            throw new InvalidOperationException($"The {action} setup failed with status {response.Status}: {await response.ReadErrorMessageAsync()}");
    }
}
