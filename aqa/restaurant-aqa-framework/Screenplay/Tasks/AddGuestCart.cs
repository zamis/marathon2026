using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class AddGuestCart(string guestName, int quantity = 1, bool submit = false, string groupName = "current") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrderScenario>(groupName);
        var api = new GroupOrdersApi(actor.UsingTheApi());
        var response = await api.CreateGuestSessionAsync(new CreateGuestSessionRequest(group.GroupReservationId, guestName));
        await SetUpGroupPreOrder.RequireSuccessAsync(response, "guest session creation");
        var created = await response.ReadAsAsync<CreateGuestSessionResponse>();
        var guest = new GuestCart(created.SessionId, created.SessionToken, guestName, quantity, submit);

        actor.Recall<CleanupScope>().TrackDelete(
            $"guest session:{guestName}",
            () => api.RemoveGuestAsync(group.GroupReservationId, guest.Id, group.OwnerToken));

        if (quantity > 0)
        {
            response = await api.AddDishAsync(guest.Id, group.Dish.Id, quantity, guest.Token);
            await SetUpGroupPreOrder.RequireSuccessAsync(response, "guest cart update");
        }

        if (submit)
        {
            response = await api.SubmitGuestCartAsync(guest.Id, guest.Token);
            await SetUpGroupPreOrder.RequireSuccessAsync(response, "guest cart confirmation");
        }

        group.Guests.Add(guestName, guest);
    }
}
