using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class RemoveGuestThroughUi(string guestName) : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await page.RemoveGuestButton(guestName).ClickAsync();
        await page.ConfirmRemoveGuestAsync();
        await Microsoft.Playwright.Assertions.Expect(page.Participants.Filter(new() { HasText = guestName })).ToHaveCountAsync(0);
    }
}
