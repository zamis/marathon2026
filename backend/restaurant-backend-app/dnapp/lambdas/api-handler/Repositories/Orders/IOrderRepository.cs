using APIHandler.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIHandler.Repositories.Orders;

public interface IOrderRepository
{
    Task<Order?> GetOrderByIdAsync(string orderId, string customerId);
    Task<IEnumerable<Order>> GetOrdersByCustomerIdAsync(string customerId);
    Task<Order?> GetOrderByReservationIdAsync(string reservationId);
    Task CreateOrderAsync(Order order);
    Task SaveOrderAsync(Order order);
    Task<bool> TrySaveSubmittedOrderAndGroupReservationAsync(Order order, GroupReservation groupReservation);
}
