using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class RemoveGuestFromGroup(
    string guestName,
    string groupName = "current",
    bool useOwner = true,
    string guestGroupName = "current") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrderScenario>(groupName);
        var guest = actor.Recall<GroupOrderScenario>(guestGroupName).Guests[guestName];
        await actor.TheLastResponse().CaptureAsync(
            await new GroupOrdersApi(actor.UsingTheApi()).RemoveGuestAsync(
                group.GroupReservationId, guest.Id, useOwner ? group.OwnerToken : group.OtherCustomerToken));
    }
}
