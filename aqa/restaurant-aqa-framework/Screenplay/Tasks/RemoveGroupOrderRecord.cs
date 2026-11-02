using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class RemoveGroupOrderRecord(string groupName = "current") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrderScenario>(groupName);
        await actor.TheLastResponse().CaptureAsync(
            await new OrdersApi(actor.UsingTheApi()).DeleteOrderAsync(group.ReservationId, group.OwnerToken));
    }
}
