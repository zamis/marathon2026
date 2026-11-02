using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed record GroupOrderOnScreen(
    IReadOnlyList<string> Participants,
    IReadOnlyList<ParticipantCardOnScreen> ParticipantCards,
    string Total,
    string Status,
    string ConsolidatedOrder);

public sealed record ParticipantDishOnScreen(string Name, int Quantity, string Price);

public sealed record ParticipantCardOnScreen(
    string Title,
    string State,
    IReadOnlyList<ParticipantDishOnScreen> Items,
    string? EmptyMessage,
    string Subtotal);

public sealed class TheGroupOrderOnScreen : IQuestion<GroupOrderOnScreen>
{
    public async Task<GroupOrderOnScreen> AnsweredByAsync(Actor actor)
    {
        var page = new GroupOrderPage(actor.LookingAtThePage());
        await Microsoft.Playwright.Assertions.Expect(page.Title).ToBeVisibleAsync();
        if (actor.TryRecall<bool>(out var submitted) && submitted)
            await Microsoft.Playwright.Assertions.Expect(page.Status).ToHaveTextAsync("Sent to Restaurant");
        var participants = new List<string>();
        var participantCards = new List<ParticipantCardOnScreen>();
        foreach (var card in await page.Participants.AllAsync())
        {
            participants.Add(await card.InnerTextAsync());
            var items = new List<ParticipantDishOnScreen>();
            foreach (var item in await card.Locator(".items .item").AllAsync())
            {
                var quantityText = await item.Locator(".qty").InnerTextAsync();
                var quantity = int.Parse(
                    quantityText.Replace("×", string.Empty, StringComparison.Ordinal).Trim(),
                    System.Globalization.CultureInfo.InvariantCulture);
                items.Add(new ParticipantDishOnScreen(
                    await item.Locator(".name").InnerTextAsync(),
                    quantity,
                    await item.Locator(".price").InnerTextAsync()));
            }

            var emptyLocator = card.Locator(".empty");
            participantCards.Add(new ParticipantCardOnScreen(
                await card.Locator(".title").InnerTextAsync(),
                await card.Locator(".chip").InnerTextAsync(),
                items,
                await emptyLocator.CountAsync() == 0 ? null : await emptyLocator.InnerTextAsync(),
                await card.Locator(".subtotal span").Nth(1).InnerTextAsync()));
        }

        return new GroupOrderOnScreen(
            participants,
            participantCards,
            await page.GrandTotal.InnerTextAsync(),
            await actor.LookingAtThePage().Locator(".order-badge").InnerTextAsync(),
            await page.ConsolidatedOrder.InnerTextAsync());
    }
}
