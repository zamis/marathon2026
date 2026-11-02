using Restaurant_Aqa_Framework.Api;
using Restaurant_Aqa_Framework.Api.Dtos.Auth;
using Restaurant_Aqa_Framework.Api.Seeding;
using Restaurant_Aqa_Framework.Support;

namespace Restaurant_Aqa_Framework.Screenplay.Tasks;

public sealed class SignInAnotherCustomer(string groupName = "current") : ITask
{
    public async Task PerformAsAsync(Actor actor)
    {
        var group = actor.Recall<GroupOrderScenario>(groupName);
        var credentials = SampleDataFactory.RegisterRequest();
        var auth = new AuthApi(actor.UsingTheApi());
        var registration = await auth.RegisterAsync(credentials);
        await SetUpGroupPreOrder.RequireSuccessAsync(registration, "second customer registration");
        var response = await auth.SignInAsync(new SignInRequest(credentials.Email, credentials.Password));
        if (!response.Ok)
            throw new InvalidOperationException($"Second customer sign-in failed with status {response.Status}.");
        group.OtherCustomerToken = (await response.ReadAsAsync<SignInResponse>()).IdToken;
    }
}
