using System.Text.Json.Serialization;

namespace APIHandler.Models.GroupReservations;

public class SubmitGroupReservationRequest
{
    [JsonPropertyName("reservationId")]
    public string? ReservationId { get; set; }
}
