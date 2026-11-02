using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class AddDishToOwnerSession(string groupName = "current") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrderScenario>(groupName);
        var api = new GroupOrdersApi(actor.UsingTheApi());
        var ownerSessionResponse = await api.GetOwnerSessionAsync(group.ReservationId, group.OwnerToken);
        await SetUpGroupPreOrder.RequireSuccessAsync(ownerSessionResponse, "owner session lookup");
        var ownerSession = await ownerSessionResponse.ReadAsAsync<OwnerGuestSessionResponse>();
        await actor.TheLastResponse().CaptureAsync(
            await api.AddDishAsync(ownerSession.GuestSessionId, group.Dish.Id, 1, group.OwnerToken));
    }
}
