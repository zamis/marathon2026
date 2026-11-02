using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class ReadGroupParticipants(
    string groupName = "current",
    string? bearerToken = null,
    bool anonymous = false) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrderScenario>(groupName);
        await actor.TheLastResponse().CaptureAsync(
            await new GroupOrdersApi(actor.UsingTheApi()).GetParticipantsAsync(
                group.GroupReservationId, anonymous ? null : bearerToken ?? group.OwnerToken));
    }
}
