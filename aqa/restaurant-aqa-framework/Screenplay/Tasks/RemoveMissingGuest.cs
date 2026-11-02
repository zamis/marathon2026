using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class RemoveMissingGuest(string groupName = "current", bool useOwner = true) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrderScenario>(groupName);
        await actor.TheLastResponse().CaptureAsync(
            await new GroupOrdersApi(actor.UsingTheApi()).RemoveGuestAsync(
                group.GroupReservationId,
                Guid.NewGuid().ToString(),
                useOwner ? group.OwnerToken : group.OtherCustomerToken));
    }
}
