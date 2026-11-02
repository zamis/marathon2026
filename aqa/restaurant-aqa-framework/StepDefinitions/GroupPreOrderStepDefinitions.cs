using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;
using Restaurant_Aqa_Framework.Api.Dtos.Orders;
using Restaurant_Aqa_Framework.Screenplay;
using Restaurant_Aqa_Framework.Screenplay.Questions;
using Restaurant_Aqa_Framework.Screenplay.Tasks;
using Restaurant_Aqa_Framework.Support;
using Restaurant_Aqa_Framework.Ui;

namespace Restaurant_Aqa_Framework.StepDefinitions;

[Binding]
public sealed class GroupPreOrderStepDefinitions(Actor actor) : StepDefinitionBase(actor)
{
    [Given("the owner has created a group pre-order")]
    public Task GivenOwnerCreatedGroup() => Actor.AttemptsToAsync(new SetUpGroupPreOrder());

    [Given("the owner has created a second group pre-order")]
    public Task GivenOwnerCreatedSecondGroup() => Actor.AttemptsToAsync(new SetUpGroupPreOrder("other"));

    [Given("{string} has added {int} dishes and confirmed the cart")]
    public Task GivenGuestHasConfirmedCart(string guestName, int quantity) =>
        Actor.AttemptsToAsync(new AddGuestCart(guestName, quantity, submit: true));

    [Given("{string} has added {int} dishes but has not confirmed the cart")]
    public Task GivenGuestHasUnconfirmedCart(string guestName, int quantity) =>
        Actor.AttemptsToAsync(new AddGuestCart(guestName, quantity));

    [Given("{string} has joined without adding dishes")]
    public Task GivenGuestHasNoDishes(string guestName) =>
        Actor.AttemptsToAsync(new AddGuestCart(guestName, quantity: 0));

    [When("the owner reviews the participants")]
    [Scope(Tag = "api")]
    public Task WhenOwnerReviewsParticipants() => Actor.AttemptsToAsync(new ReadGroupParticipants());

    [When("the guest {string} changes their cart by adding {int} dish")]
    [Scope(Tag = "api")]
    public Task WhenGuestChangesOwnCart(string guestName, int quantity) =>
        Actor.AttemptsToAsync(new ChangeGuestCart(guestName, quantity));

    [Then("the participant list includes each guest's dishes, quantities and subtotal, with the correct group total")]
    [Scope(Tag = "api")]
    public async Task ThenParticipantsAndTotalsAreCorrect()
    {
        var group = Actor.Recall<GroupOrderScenario>();
        var snapshot = await Actor.AsksForAsync(new TheGroupParticipants());
        var expectedNames = group.Guests.Keys.Order(StringComparer.Ordinal).ToArray();
        var actualNames = snapshot.Participants
            .Where(participant => participant.Type == "Guest")
            .Select(participant => participant.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        await Assert.That(actualNames).IsEquivalentTo(expectedNames)
            .Because($"the group response was {Actor.TheLastResponse().Body}");

        var guestTotal = 0m;
        foreach (var (name, cart) in group.Guests)
        {
            var participant = snapshot.Participants.Single(item => item.Id == cart.Id);
            await Assert.That(participant.Items.Count).IsEqualTo(cart.Quantity == 0 ? 0 : 1)
                .Because($"dish count for {name}; response: {Actor.TheLastResponse().Body}");
            if (cart.Quantity > 0)
            {
                var item = participant.Items.Single();
                await Assert.That(item.Dish.Id).IsEqualTo(group.Dish.Id)
                    .Because($"dish identity for {name}; response: {Actor.TheLastResponse().Body}");
                await Assert.That(item.Dish.Name).IsEqualTo(group.Dish.Name)
                    .Because($"dish name for {name}; response: {Actor.TheLastResponse().Body}");
                await Assert.That(item.Dish.Price).IsEqualTo(group.Dish.Price)
                    .Because($"dish price for {name}; response: {Actor.TheLastResponse().Body}");
                await Assert.That(item.Quantity).IsEqualTo(cart.Quantity)
                    .Because($"dish quantity for {name}; response: {Actor.TheLastResponse().Body}");
            }

            var expectedSubtotal = group.Dish.Price * cart.Quantity;
            await Assert.That(participant.Items.Sum(item => item.Quantity)).IsEqualTo(cart.Quantity)
                .Because($"quantity for {name}; response: {Actor.TheLastResponse().Body}");
            await Assert.That(participant.IsSubmitted).IsEqualTo(cart.IsSubmitted)
                .Because($"confirmation state for {name}; response: {Actor.TheLastResponse().Body}");
            await Assert.That(participant.Total).IsEqualTo(expectedSubtotal)
                .Because($"subtotal for {name}; response: {Actor.TheLastResponse().Body}");
            guestTotal += expectedSubtotal;
        }

        await Assert.That(snapshot.Total).IsEqualTo(guestTotal)
            .Because($"sum of participant subtotals; response: {Actor.TheLastResponse().Body}");
    }

    [Then("{string} is listed as {string} with {int} dishes")]
    [Scope(Tag = "api")]
    public async Task ThenParticipantState(string guestName, string state, int quantity)
    {
        var group = Actor.Recall<GroupOrderScenario>();
        var cart = group.Guests[guestName];
        var participant = (await Actor.AsksForAsync(new TheGroupParticipants()))
            .Participants.Single(item => item.Id == cart.Id);
        await Assert.That(participant.IsSubmitted).IsEqualTo(state == "confirmed")
            .Because($"participant state in response: {Actor.TheLastResponse().Body}");
        await Assert.That(participant.Items.Sum(item => item.Quantity)).IsEqualTo(quantity)
            .Because($"participant dishes in response: {Actor.TheLastResponse().Body}");
    }

    [Then("the group total reflects the changed cart")]
    [Scope(Tag = "api")]
    public async Task ThenGroupTotalReflectsChangedCart()
    {
        var group = Actor.Recall<GroupOrderScenario>();
        var snapshot = await Actor.AsksForAsync(new TheGroupParticipants());
        var expected = group.Guests.Values.Sum(guest => group.Dish.Price * guest.Quantity);
        foreach (var guest in group.Guests.Values.Where(guest => guest.Quantity > 0))
        {
            var participant = snapshot.Participants.Single(item => item.Id == guest.Id);
            var item = participant.Items.Single();
            await Assert.That(item.Dish.Id).IsEqualTo(group.Dish.Id)
                .Because($"changed cart dish identity for {guest.Name}; response: {Actor.TheLastResponse().Body}");
            await Assert.That(item.Quantity).IsEqualTo(guest.Quantity)
                .Because($"changed cart quantity for {guest.Name}; response: {Actor.TheLastResponse().Body}");
            await Assert.That(participant.Total).IsEqualTo(group.Dish.Price * guest.Quantity)
                .Because($"changed cart subtotal for {guest.Name}; response: {Actor.TheLastResponse().Body}");
        }

        await Assert.That(snapshot.Total).IsEqualTo(expected)
            .Because($"response after cart update: {Actor.TheLastResponse().Body}");
    }

    [When("another customer reviews the participants")]
    [Scope(Tag = "api")]
    public async Task WhenAnotherCustomerReviewsParticipants()
    {
        await Actor.AttemptsToAsync(new SignInAnotherCustomer());
        Actor.Remember("group-access-expected-status", "403");
        await Actor.AttemptsToAsync(new ReadGroupParticipants(bearerToken: Actor.Recall<GroupOrderScenario>().OtherCustomerToken));
    }

    [When("a guest reviews the participants")]
    [Scope(Tag = "api")]
    public async Task WhenGuestReviewsParticipants()
    {
        var guest = Actor.Recall<GroupOrderScenario>().Guests.Values.First();
        Actor.Remember("group-access-expected-status", "401");
        await Actor.AttemptsToAsync(new ReadGroupParticipants(bearerToken: guest.Token));
    }

    [When("a waiter reviews the participants")]
    [Scope(Tag = "api")]
    public async Task WhenWaiterReviewsParticipants()
    {
        var waiter = await Actor.AsksForAsync(new TheWaiterSession());
        Actor.Remember("group-access-expected-status", "403");
        await Actor.AttemptsToAsync(new ReadGroupParticipants(bearerToken: waiter.IdToken));
    }

    [When("an unauthenticated visitor reviews the participants")]
    [Scope(Tag = "api")]
    public async Task WhenUnauthenticatedVisitorReviewsParticipants()
    {
        Actor.Remember("group-access-expected-status", "401");
        await Actor.AttemptsToAsync(new ReadGroupParticipants(anonymous: true));
    }

    [Then("only the owner can review the participants")]
    [Scope(Tag = "api")]
    public async Task ThenOnlyOwnerCanReviewParticipants()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        var expected = Actor.Recall<string>("group-access-expected-status");
        await Assert.That(outcome.Status).IsEqualTo(int.Parse(expected, System.Globalization.CultureInfo.InvariantCulture))
            .Because($"access response was: {outcome.Body}");
    }

    [When("the owner requests a group that does not exist")]
    [Scope(Tag = "api")]
    public Task WhenOwnerRequestsMissingGroup() => Actor.AttemptsToAsync(new ReadMissingGroupParticipants());

    [Then("the group is not found")]
    [Scope(Tag = "api")]
    public async Task ThenGroupNotFound()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(404).Because($"response body: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Contains("Group reservation was not found");
    }

    [When("the owner submits the group pre-order")]
    [Scope(Tag = "api")]
    public async Task WhenOwnerSubmitsGroup()
    {
        await Actor.AttemptsToAsync(new ReadGroupParticipants());
        Actor.Remember("submitted-participants", await Actor.AsksForAsync(new TheGroupParticipants()));
        await Actor.AttemptsToAsync(new SubmitGroupPreOrder());
    }

    [When("the owner adds a dish to their own group session")]
    [Scope(Tag = "api")]
    public Task WhenOwnerAddsDishToOwnSession() =>
        Actor.AttemptsToAsync(new AddDishToOwnerSession());

    [Then("the owner can add dishes to their own group cart")]
    [Scope(Tag = "api")]
    public async Task ThenOwnerCanUseOwnGroupCart()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Succeeded).IsTrue()
            .Because($"owner's cart update was refused with status {outcome.Status}: {outcome.Body}");
    }

    [When("the owner submits the already submitted group again")]
    [Scope(Tag = "api")]
    public Task WhenOwnerSubmitsGroupAgain() => Actor.AttemptsToAsync(new SubmitGroupPreOrder());

    [When("a non-owner submits the group pre-order")]
    [Scope(Tag = "api")]
    public async Task WhenNonOwnerSubmitsGroup()
    {
        await Actor.AttemptsToAsync(new SignInAnotherCustomer());
        await Actor.AttemptsToAsync(new SubmitGroupPreOrder(useOwner: false));
    }

    [When("the owner submits the group with no reservation order")]
    [Scope(Tag = "api")]
    public async Task WhenOwnerSubmitsWithoutOrder()
    {
        await Actor.AttemptsToAsync(new RemoveGroupOrderRecord());
        await Actor.AttemptsToAsync(new SubmitGroupPreOrder());
    }

    [When("the owner submits the group without its reservation")]
    [Scope(Tag = "api")]
    public Task WhenOwnerSubmitsWithoutReservation() =>
        Actor.AttemptsToAsync(new SubmitGroupWithMissingReservation());

    [When("the owner submits a group that does not exist")]
    [Scope(Tag = "api")]
    public Task WhenOwnerSubmitsMissingGroup() =>
        Actor.AttemptsToAsync(new SubmitToMissingGroup());

    [Then("the group has been submitted")]
    [Scope(Tag = "api")]
    public async Task ThenGroupSubmitted()
    {
        await Actor.AttemptsToAsync(new ReadGroupStatus());
        var status = await Actor.AsksForAsync(new TheGroupStatus());
        await Assert.That(status.Status).IsEqualTo("Submitted")
            .Because($"group status response: {Actor.TheLastResponse().Body}");
    }

    [Then("the waiter sees the submitted order with the confirmed guest composition")]
    [Scope(Tag = "api")]
    public async Task ThenWaiterSeesConfirmedComposition()
    {
        await Actor.AttemptsToAsync(new ReadSubmittedOrderAsWaiter());
        var order = await Actor.AsksForAsync(new TheSubmittedOrder());
        var group = Actor.Recall<GroupOrderScenario>();
        var confirmedQuantity = group.Guests.Values.Sum(guest =>
        {
            var participant = Actor.Recall<GroupSessionsResponse>("submitted-participants")
                .Participants.SingleOrDefault(item => item.Id == guest.Id);
            return participant is { IsSubmitted: true }
                ? participant.Items.Sum(item => item.Quantity)
                : 0;
        });

        await Assert.That(order.Status).IsEqualTo(OrderStatus.SubmittedByCustomer)
            .Because($"waiter order response: {Actor.TheLastResponse().Body}");
        await Assert.That(order.OrderItems.Count).IsEqualTo(1)
            .Because("identical dishes from confirmed guests must be consolidated");
        await Assert.That(order.OrderItems.Sum(item => item.Quantity)).IsEqualTo(confirmedQuantity)
            .Because($"waiter order response: {Actor.TheLastResponse().Body}");
        await Assert.That(order.OrderItems.All(item => item.Dish.Id == group.Dish.Id)).IsTrue()
            .Because($"waiter order response: {Actor.TheLastResponse().Body}");
    }

    [When("the owner submits the group without any confirmed guest carts")]
    [Scope(Tag = "api")]
    public Task WhenOwnerSubmitsNoConfirmedCarts() => Actor.AttemptsToAsync(new SubmitGroupPreOrder());

    [Then("the group submission is refused because no guest carts were confirmed")]
    [Scope(Tag = "api")]
    public async Task ThenNoConfirmedCartRefusal()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(400).Because($"response body: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Contains("There are no submitted guest sessions.");
    }

    [Then("the repeat submission is refused as already submitted")]
    [Scope(Tag = "api")]
    public async Task ThenRepeatSubmissionRefused()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(409).Because($"response body: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).IsEqualTo("Group reservation was submitted.");
    }

    [Then("a non-owner is forbidden from submitting the group")]
    [Scope(Tag = "api")]
    public async Task ThenNonOwnerCannotSubmit()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(403).Because($"response body: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).IsEqualTo("Only the reservation owner can perform this action.");
    }

    [Then("the reservation order is reported missing")]
    [Scope(Tag = "api")]
    public async Task ThenOrderMissing()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(400).Because($"response body: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Contains("Order for reservation");
    }

    [Then("the reservation is reported missing")]
    [Scope(Tag = "api")]
    public async Task ThenReservationMissing()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(404).Because($"response body: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Contains("was not found");
    }

    [Then("the participant list is empty and the total is zero")]
    [Scope(Tag = "api")]
    public async Task ThenEmptyGroupResponse()
    {
        var snapshot = await Actor.AsksForAsync(new TheGroupParticipants());
        await Assert.That(snapshot.Participants).IsEmpty()
            .Because($"empty group response: {Actor.TheLastResponse().Body}");
        await Assert.That(snapshot.Total).IsEqualTo(0m)
            .Because($"empty group response: {Actor.TheLastResponse().Body}");
    }

    [When("the owner removes {string} from the group")]
    [Scope(Tag = "api")]
    public Task WhenOwnerRemovesGuest(string guestName) =>
        Actor.AttemptsToAsync(new RemoveGuestFromGroup(guestName));

    [When("the owner removes the last guest")]
    [Scope(Tag = "api")]
    public async Task WhenOwnerRemovesLastGuest()
    {
        var guestName = Actor.Recall<GroupOrderScenario>().Guests.Keys.Single();
        await Actor.AttemptsToAsync(new RemoveGuestFromGroup(guestName));
    }

    [Then("the group contains only {string} with the remaining subtotal")]
    [Scope(Tag = "api")]
    public async Task ThenRemainingGuestOnly(string guestName)
    {
        var group = Actor.Recall<GroupOrderScenario>();
        var snapshot = await Actor.AsksForAsync(new TheGroupParticipants());
        var guest = group.Guests[guestName];
        await Assert.That(snapshot.Participants.Count(item => item.Type == "Guest")).IsEqualTo(1)
            .Because($"participants after removal: {Actor.TheLastResponse().Body}");
        await Assert.That(snapshot.Participants.Any(item => item.Id == guest.Id)).IsTrue()
            .Because($"remaining participant: {Actor.TheLastResponse().Body}");
        var remainingParticipant = snapshot.Participants.Single(item => item.Id == guest.Id);
        await Assert.That(remainingParticipant.Items.Count).IsEqualTo(1)
            .Because($"remaining guest dish: {Actor.TheLastResponse().Body}");
        await Assert.That(remainingParticipant.Items.Single().Dish.Id).IsEqualTo(group.Dish.Id)
            .Because($"remaining guest dish identity: {Actor.TheLastResponse().Body}");
        await Assert.That(remainingParticipant.Items.Single().Quantity).IsEqualTo(guest.Quantity)
            .Because($"remaining guest dish quantity: {Actor.TheLastResponse().Body}");
        var expectedSubtotal = group.Dish.Price * group.Guests[guestName].Quantity;
        await Assert.That(remainingParticipant.Total).IsEqualTo(expectedSubtotal)
            .Because($"remaining participant subtotal: {Actor.TheLastResponse().Body}");
        await Assert.That(snapshot.Total).IsEqualTo(expectedSubtotal)
            .Because($"updated total: {Actor.TheLastResponse().Body}");
    }

    [Then("the removed guest is absent and does not affect the total")]
    [Scope(Tag = "api")]
    public async Task ThenRemovedGuestExcluded()
    {
        var group = Actor.Recall<GroupOrderScenario>();
        var removed = group.Guests["Removed"];
        var snapshot = await Actor.AsksForAsync(new TheGroupParticipants());
        var expectedRemainingTotal = group.Guests.Values
            .Where(guest => guest.Name != removed.Name)
            .Sum(guest => group.Dish.Price * guest.Quantity);
        await Assert.That(snapshot.Participants.Any(item => item.Id == removed.Id)).IsFalse()
            .Because($"participants after removal: {Actor.TheLastResponse().Body}");
        await Assert.That(snapshot.Total).IsEqualTo(expectedRemainingTotal)
            .Because($"updated total: {Actor.TheLastResponse().Body}");
    }

    [Then("the remaining group total is zero")]
    [Scope(Tag = "api")]
    public async Task ThenRemainingGroupTotalZero()
    {
        var snapshot = await Actor.AsksForAsync(new TheGroupParticipants());
        await Assert.That(snapshot.Participants.Where(item => item.Type == "Guest")).IsEmpty()
            .Because($"participants after removing the last guest: {Actor.TheLastResponse().Body}");
        await Assert.That(snapshot.Participants.Count(item => item.Type == "Owner")).IsEqualTo(1)
            .Because("the application retains the reservation owner's empty session");
        await Assert.That(snapshot.Participants.Single(item => item.Type == "Owner").Items).IsEmpty();
        await Assert.That(snapshot.Total).IsEqualTo(0m)
            .Because($"participants after removing the last guest: {Actor.TheLastResponse().Body}");
    }

    [When("the owner tries to remove a guest from another group")]
    [Scope(Tag = "api")]
    public async Task WhenOwnerRemovesForeignGuest()
    {
        await Actor.AttemptsToAsync(new SetUpGroupPreOrder("other"));
        await Actor.AttemptsToAsync(new AddGuestCart("Foreign", 1, groupName: "other"));
        await Actor.AttemptsToAsync(new RemoveGuestFromGroup("Foreign", guestGroupName: "other"));
    }

    [When("the owner tries to remove a participant who does not exist")]
    [Scope(Tag = "api")]
    public Task WhenOwnerRemovesMissingGuest() => Actor.AttemptsToAsync(new RemoveMissingGuest());

    [When("a non-owner tries to remove {string}")]
    [Scope(Tag = "api")]
    public async Task WhenNonOwnerRemovesGuest(string guestName)
    {
        await Actor.AttemptsToAsync(new SignInAnotherCustomer());
        await Actor.AttemptsToAsync(new RemoveGuestFromGroup(guestName, useOwner: false));
    }

    [Then("removal is rejected because the participant belongs to another group")]
    [Scope(Tag = "api")]
    public async Task ThenForeignGuestRejected()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(400).Because($"response body: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Contains("does not belong to this group reservation");
    }

    [Then("removal is rejected because the participant does not exist")]
    [Scope(Tag = "api")]
    public async Task ThenMissingGuestRejected()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(404).Because($"response body: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).Contains("not found");
    }

    [Then("a non-owner is forbidden from removing a participant")]
    [Scope(Tag = "api")]
    public async Task ThenNonOwnerCannotRemoveGuest()
    {
        var outcome = await Actor.AsksForAsync(new TheOutcome());
        await Assert.That(outcome.Status).IsEqualTo(403).Because($"response body: {outcome.Body}");
        await Assert.That(outcome.ErrorMessage).IsEqualTo("Only the reservation owner can perform this action.");
    }

    [Given("the guest carts are ready for UI review")]
    [Scope(Tag = "ui")]
    public async Task GivenGuestCartsForUiReview()
    {
        await Actor.AttemptsToAsync(new AddGuestCart("Lina", 2));
        await Actor.AttemptsToAsync(new AddGuestCart("Noah", 1, submit: true));
        await Actor.AttemptsToAsync(new AddGuestCart("Casey", 0));
    }

    [When("the owner signs in and opens the group review")]
    [Scope(Tag = "ui")]
    public async Task WhenOwnerOpensGroupReview()
    {
        var group = Actor.Recall<GroupOrderScenario>();
        await Actor.AttemptsToAsync(new SignInThroughTheLoginForm(group.Owner.Email, group.Owner.Password));
        await Actor.AttemptsToAsync(new OpenGroupOrderReview());
    }

    [Then("the owner sees each guest's dish details, quantity, subtotal and cart state on the review page")]
    [Scope(Tag = "ui")]
    public async Task ThenOwnerSeesParticipantDetailsOnReview()
    {
        var group = Actor.Recall<GroupOrderScenario>();
        var screen = await Actor.AsksForAsync(new TheGroupOrderOnScreen());
        var lina = screen.ParticipantCards.Single(card => card.Title == "Guest: Lina");
        var noah = screen.ParticipantCards.Single(card => card.Title == "Guest: Noah");
        var casey = screen.ParticipantCards.Single(card => card.Title == "Guest: Casey");

        await Assert.That(lina.State).IsEqualTo("Still choosing").Because($"participant cards: {screen}");
        await Assert.That(lina.Items.Count).IsEqualTo(1).Because($"Lina's visible cart: {lina}");
        await Assert.That(lina.Items.Single().Name).IsEqualTo(group.Dish.Name).Because($"Lina's visible cart: {lina}");
        await Assert.That(lina.Items.Single().Quantity).IsEqualTo(2).Because($"Lina's visible cart: {lina}");
        await Assert.That(lina.Items.Single().Price).Contains((group.Dish.Price * 2).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
            .Because($"Lina's visible cart: {lina}");
        await Assert.That(lina.Subtotal).Contains((group.Dish.Price * 2).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
            .Because($"Lina's visible subtotal: {lina}");

        await Assert.That(noah.State).IsEqualTo("Submitted").Because($"participant cards: {screen}");
        await Assert.That(noah.Items.Count).IsEqualTo(1).Because($"Noah's visible cart: {noah}");
        await Assert.That(noah.Items.Single().Name).IsEqualTo(group.Dish.Name).Because($"Noah's visible cart: {noah}");
        await Assert.That(noah.Items.Single().Quantity).IsEqualTo(1).Because($"Noah's visible cart: {noah}");
        await Assert.That(noah.Items.Single().Price).Contains(group.Dish.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
            .Because($"Noah's visible cart: {noah}");
        await Assert.That(noah.Subtotal).Contains(group.Dish.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
            .Because($"Noah's visible subtotal: {noah}");

        await Assert.That(casey.State).IsEqualTo("Still choosing").Because($"participant cards: {screen}");
        await Assert.That(casey.Items).IsEmpty().Because($"Casey's visible cart: {casey}");
        var expectedTotal = group.Dish.Price * group.Guests["Noah"].Quantity;
        await Assert.That(screen.Total).Contains(expectedTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
            .Because($"consolidated total on screen (submitted carts only): {screen.Total}");
    }

    [Then("the empty guest cart is visibly empty with a zero subtotal")]
    [Scope(Tag = "ui")]
    public async Task ThenEmptyGuestCartIsVisible()
    {
        var screen = await Actor.AsksForAsync(new TheGroupOrderOnScreen());
        var emptyGuest = screen.ParticipantCards.Single(card => card.Title == "Guest: Casey");
        await Assert.That(emptyGuest.EmptyMessage).IsEqualTo("No dishes yet")
            .Because($"empty participant card: {emptyGuest}");
        await Assert.That(emptyGuest.Subtotal).Contains("0.00")
            .Because($"empty participant subtotal: {emptyGuest}");
    }

    [Then("the consolidated total reflects submitted carts")]
    [Scope(Tag = "ui")]
    public async Task ThenConsolidatedTotalReflectsSubmittedCarts()
    {
        var group = Actor.Recall<GroupOrderScenario>();
        var screen = await Actor.AsksForAsync(new TheGroupOrderOnScreen());
        var submittedTotal = group.Guests.Values
            .Where(guest => guest.IsSubmitted)
            .Sum(guest => group.Dish.Price * guest.Quantity);
        await Assert.That(screen.Total).Contains(submittedTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
            .Because($"consolidated total on screen: {screen.Total}");
    }

    [Then("the group total on the review page includes every guest cart")]
    [Scope(Tag = "ui")]
    public async Task ThenGroupTotalOnUiIncludesEveryGuestCart()
    {
        var group = Actor.Recall<GroupOrderScenario>();
        var screen = await Actor.AsksForAsync(new TheGroupOrderOnScreen());
        var allCartTotal = group.Guests.Values.Sum(guest => group.Dish.Price * guest.Quantity);
        await Assert.That(screen.Total).Contains(allCartTotal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
            .Because($"homework-required total across every participant; visible consolidated total: {screen.Total}");
    }

    [When("the owner confirms the reviewed group pre-order")]
    [Scope(Tag = "ui")]
    public Task WhenOwnerConfirmsGroupThroughUi() =>
        Actor.AttemptsToAsync(new ConfirmGroupOrderThroughUi());

    [Then("the review page shows that the pre-order was sent to the restaurant")]
    [Scope(Tag = "ui")]
    public async Task ThenUiShowsSubmitted()
    {
        var screen = await Actor.AsksForAsync(new TheGroupOrderOnScreen());
        await Assert.That(screen.Status).IsEqualTo("Sent to Restaurant")
            .Because($"review page state: {screen}");
    }

    [When("the owner removes {string} from the review page")]
    [Scope(Tag = "ui")]
    public Task WhenOwnerRemovesGuestThroughUi(string guestName) =>
        Actor.AttemptsToAsync(new RemoveGuestThroughUi(guestName));

    [Then("the review page excludes the removed guest and their dishes")]
    [Scope(Tag = "ui")]
    public async Task ThenUiExcludesRemovedGuest()
    {
        var screen = await Actor.AsksForAsync(new TheGroupOrderOnScreen());
        await Assert.That(screen.ParticipantCards.Any(card => card.Title == "Guest: Removed")).IsFalse()
            .Because($"participant cards: {string.Join(" | ", screen.Participants)}");
        var kept = Actor.Recall<GroupOrderScenario>().Guests["Kept"];
        await Assert.That(screen.ConsolidatedOrder).Contains($"× {kept.Quantity}")
            .Because($"consolidated order after removal: {screen.ConsolidatedOrder}");
    }

    [Then("the waiter sees the remaining guest's exact confirmed dish")]
    [Scope(Tag = "ui")]
    public async Task ThenWaiterSeesRemainingGuestOrder()
    {
        await Actor.AttemptsToAsync(new ReadSubmittedOrderAsWaiter());
        var order = await Actor.AsksForAsync(new TheSubmittedOrder());
        var group = Actor.Recall<GroupOrderScenario>("current");
        var kept = group.Guests["Kept"];

        await Assert.That(order.Status).IsEqualTo(OrderStatus.SubmittedByCustomer)
            .Because($"waiter order response: {Actor.TheLastResponse().Body}");
        await Assert.That(order.OrderItems.Count).IsEqualTo(1)
            .Because($"waiter order response: {Actor.TheLastResponse().Body}");
        await Assert.That(order.OrderItems.Single().Dish.Id).IsEqualTo(group.Dish.Id)
            .Because($"waiter order response: {Actor.TheLastResponse().Body}");
        await Assert.That(order.OrderItems.Single().Quantity).IsEqualTo(kept.Quantity)
            .Because($"waiter order response: {Actor.TheLastResponse().Body}");
    }
}
