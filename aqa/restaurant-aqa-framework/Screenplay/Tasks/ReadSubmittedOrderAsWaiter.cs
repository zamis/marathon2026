using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class ReadSubmittedOrderAsWaiter(string groupName = "current") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrderScenario>(groupName);
        var waiter = await actor.AsksForAsync(new TheWaiterSession());
        await actor.TheLastResponse().CaptureAsync(
            await new OrdersApi(actor.UsingTheApi()).GetOrderAsync(group.ReservationId, waiter.IdToken));
    }
}
