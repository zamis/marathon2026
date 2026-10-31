using System.Text.Json.Serialization;

namespace APIHandler.Models.GroupReservations;

public record GroupReservationDishQuantity(
    [property: JsonPropertyName("dishId")] string DishId,
    [property: JsonPropertyName("quantity")] int Quantity);
