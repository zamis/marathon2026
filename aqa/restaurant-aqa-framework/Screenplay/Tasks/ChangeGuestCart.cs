using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class ChangeGuestCart(string guestName, int additionalQuantity, string groupName = "current") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrderScenario>(groupName);
        var guest = group.Guests[guestName];
        await actor.TheLastResponse().CaptureAsync(
            await new GroupOrdersApi(actor.UsingTheApi()).AddDishAsync(
                guest.Id, group.Dish.Id, additionalQuantity, guest.Token));
        if (actor.TheLastResponse().Succeeded)
            group.Guests[guestName] = guest with { Quantity = guest.Quantity + additionalQuantity };
    }
}
