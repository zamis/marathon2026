using Restaurant_Aqa_Framework.Api.Dtos.GroupOrders;

namespace Restaurant_Aqa_Framework.Screenplay.Questions;

public sealed class TheGroupStatus : IQuestion<GroupReservationStatusResponse>
{
    public Task<GroupReservationStatusResponse> AnsweredByAsync(Actor actor) =>
        Task.FromResult(actor.TheLastResponse().Json<GroupReservationStatusResponse>());
}
