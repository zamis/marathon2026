using Microsoft.Playwright;

namespace Restaurant_Aqa_Framework.Ui;

/// <summary>Owner's group-order review and confirmation page.</summary>
public sealed class GroupOrderPage(IPage page) : BasePage(page)
{
    public ILocator Title => Page.GetByRole(AriaRole.Heading, new() { Name = "Group Pre-Order", Exact = true });
    public ILocator Participants => Page.Locator("section[aria-label='Participant pre-orders'] article.card");
    public ILocator ConsolidatedOrder => Page.Locator("section.consolidated-section");
    public ILocator GrandTotal => Page.Locator(".grand-total .total-amount");
    public ILocator SubmitButton => Page.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order", Exact = true });
    public ILocator Status => Page.Locator(".order-badge");

    public Task NavigateAsync(string reservationId) =>
        NavigateToAsync($"/reservations/{Uri.EscapeDataString(reservationId)}/group-order");

    public Task ConfirmSubmitAsync() => Page.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true }).ClickAsync();

    public Task OpenSubmitConfirmationAsync() => SubmitButton.ClickAsync();

    public ILocator RemoveGuestButton(string guestName) =>
        Page.GetByRole(AriaRole.Button, new() { Name = $"Remove {guestName}", Exact = true });

    public Task ConfirmRemoveGuestAsync() =>
        Page.GetByRole(AriaRole.Button, new() { Name = "Remove Guest", Exact = true }).ClickAsync();
}
