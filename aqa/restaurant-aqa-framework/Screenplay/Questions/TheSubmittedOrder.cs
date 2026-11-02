using Restaurant_Aqa_Framework.Api.Dtos.Orders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed class TheSubmittedOrder : IQuestion<GetOrderResponse>
{
    public Task<GetOrderResponse> AnsweredByAsync(Actor actor) =>
        Task.FromResult(actor.TheLastResponse().Json<GetOrderResponse>());
}
