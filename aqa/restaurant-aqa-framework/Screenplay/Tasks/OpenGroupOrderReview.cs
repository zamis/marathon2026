using Restaurant_Aqa_Framework.Support;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class OpenGroupOrderReview(string groupName = "current") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrderScenario>(groupName);
        await new GroupOrderPage(actor.LookingAtThePage()).NavigateAsync(group.ReservationId);
    }
}
