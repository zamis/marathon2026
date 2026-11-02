using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class SubmitToMissingGroup(string groupName = "current") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrderScenario>(groupName);
        await actor.TheLastResponse().CaptureAsync(
            await new GroupOrdersApi(actor.UsingTheApi()).SubmitGroupAsync(
                Guid.NewGuid().ToString(), group.ReservationId, group.OwnerToken));
    }
}
