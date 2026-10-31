using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace APIHandler.Models.GuestSessions;

public record GetGroupReservationSessionsResponse(IEnumerable<GroupSessionParticipant> Participants, decimal Total);

public record GroupSessionParticipant(
    string? Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ParticipantType>))] ParticipantType Type,
    string Name,
    IEnumerable<GroupSessionOrderItem> Items,
    decimal Total,
    bool IsSubmitted);

public record GroupSessionOrderItem(GroupSessionDish Dish, int Quantity);

public record GroupSessionDish(string Id, string Name, decimal Price);
