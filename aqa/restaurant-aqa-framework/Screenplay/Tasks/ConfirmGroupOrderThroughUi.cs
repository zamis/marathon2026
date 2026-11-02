using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class ConfirmGroupOrderThroughUi : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.OpenSubmitConfirmationAsync();
        await page.ConfirmSubmitAsync();
        actor.Remember("group-order-submitted-ui", true);
    }
}
