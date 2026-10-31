using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Repositories.Orders;

public class OrderRepository : IOrderRepository
{
    private readonly string OrdersTableName = ResourceNames.OrdersTableName;
    private readonly string CustomerIdGlobalIndex = ResourceNames.OrdersTableCustomerIdGlobalIndex;
    private readonly string ReservationIdGlobalIndex = ResourceNames.OrdersTableReservationIdGlobalIndex;

    private readonly IDynamoDBContext _dynamoDbContext;
    private readonly IAmazonDynamoDB? _dynamoDbClient;

    public OrderRepository(IDynamoDBContext dynamoDbContext, IAmazonDynamoDB? dynamoDbClient = null)
    {
        _dynamoDbContext = dynamoDbContext ?? throw new ArgumentNullException(nameof(dynamoDbContext));
        _dynamoDbClient = dynamoDbClient;
    }

    public async Task<Order?> GetOrderByIdAsync(string orderId, string customerId)
    {
        return await _dynamoDbContext.LoadAsync<Order>(orderId, customerId, DynamoConfig.LoadConfig(OrdersTableName));
    }

    public async Task<IEnumerable<Order>> GetOrdersByCustomerIdAsync(string customerId)
    {
        var config = new QueryConfig
        {
            OverrideTableName = OrdersTableName,
            IndexName = CustomerIdGlobalIndex,
        };

        var query = _dynamoDbContext.QueryAsync<Order>(customerId, config);

        return await query.GetRemainingAsync();
    }

    public async Task<Order?> GetOrderByReservationIdAsync(string reservationId)
    {
        var config = new QueryConfig
        {
            OverrideTableName = OrdersTableName,
            IndexName = ReservationIdGlobalIndex,
        };

        var results = await _dynamoDbContext.QueryAsync<Order>(reservationId, config).GetNextSetAsync();
        Order? indexedOrder = results.FirstOrDefault(o => o.Status != OrderStatus.Cancelled);
        if (indexedOrder is null)
            return null;

        Order? currentOrder = await GetOrderByIdAsync(indexedOrder.OrderId, indexedOrder.CustomerId);
        return currentOrder?.Status == OrderStatus.Cancelled ? null : currentOrder;
    }

    public async Task CreateOrderAsync(Order order)
    {
        IAmazonDynamoDB client = _dynamoDbClient
            ?? throw new InvalidOperationException("A DynamoDB client is required for conditional order creation.");

        order.Version = 1;
        await client.PutItemAsync(new PutItemRequest
        {
            TableName = OrdersTableName,
            Item = _dynamoDbContext.ToDocument(order).ToAttributeMap(),
            ConditionExpression = "attribute_not_exists(#orderId)",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#orderId"] = "orderId"
            }
        });
    }

    public async Task SaveOrderAsync(Order order)
    {
        IAmazonDynamoDB client = _dynamoDbClient
            ?? throw new InvalidOperationException("A DynamoDB client is required for optimistic order updates.");

        long expectedVersion = order.Version;
        order.Version = expectedVersion + 1;

        await client.PutItemAsync(new PutItemRequest
        {
            TableName = OrdersTableName,
            Item = _dynamoDbContext.ToDocument(order).ToAttributeMap(),
            ConditionExpression = "attribute_exists(#orderId) AND (attribute_not_exists(#version) OR #version = :expectedVersion)",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#orderId"] = "orderId",
                ["#version"] = "orderVersion"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":expectedVersion"] = new()
                {
                    N = expectedVersion.ToString(CultureInfo.InvariantCulture)
                }
            }
        });
    }

    public async Task<bool> TrySaveSubmittedOrderAndGroupReservationAsync(Order order, GroupReservation groupReservation)
    {
        IAmazonDynamoDB client = _dynamoDbClient
            ?? throw new InvalidOperationException("A DynamoDB client is required for transactional group submission.");

        AttributeValue openStatus = GetGroupStatusAttribute(groupReservation, GroupReservationStatus.Open);
        AttributeValue submittedStatus = GetGroupStatusAttribute(groupReservation, GroupReservationStatus.Submitted);
        long expectedOrderVersion = order.Version;
        order.Version = expectedOrderVersion + 1;
        Dictionary<string, AttributeValue> orderAttributes = _dynamoDbContext.ToDocument(order).ToAttributeMap();

        try
        {
            await client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem
                    {
                        Update = new Update
                        {
                            TableName = ResourceNames.GroupReservationsTableName,
                            Key = new Dictionary<string, AttributeValue>
                            {
                                ["groupReservationId"] = new() { S = groupReservation.GroupReservationId }
                            },
                            UpdateExpression = "SET #status = :submitted, #version = if_not_exists(#version, :zero) + :one",
                            ConditionExpression = "#status = :open AND (attribute_not_exists(#version) OR #version = :expected)",
                            ExpressionAttributeNames = new Dictionary<string, string>
                            {
                                ["#status"] = "status",
                                ["#version"] = "submissionVersion"
                            },
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                            {
                                [":open"] = openStatus,
                                [":submitted"] = submittedStatus,
                                [":zero"] = new() { N = "0" },
                                [":one"] = new() { N = "1" },
                                [":expected"] = new()
                                {
                                    N = groupReservation.SubmissionVersion.ToString(CultureInfo.InvariantCulture)
                                }
                            }
                        }
                    },
                    new TransactWriteItem
                    {
                        Put = new Put
                        {
                            TableName = OrdersTableName,
                            Item = orderAttributes,
                            ConditionExpression = "attribute_exists(#orderId) AND (attribute_not_exists(#orderVersion) OR #orderVersion = :expectedOrderVersion)",
                            ExpressionAttributeNames = new Dictionary<string, string>
                            {
                                ["#orderId"] = "orderId",
                                ["#orderVersion"] = "orderVersion"
                            },
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                            {
                                [":expectedOrderVersion"] = new()
                                {
                                    N = expectedOrderVersion.ToString(CultureInfo.InvariantCulture)
                                }
                            }
                        }
                    }
                ]
            });

            return true;
        }
        catch (TransactionCanceledException exception) when (IsRetryableConflict(exception))
        {
            return false;
        }
    }

    private AttributeValue GetGroupStatusAttribute(GroupReservation groupReservation, GroupReservationStatus status)
    {
        var group = new GroupReservation
        {
            GroupReservationId = groupReservation.GroupReservationId,
            ReservationId = groupReservation.ReservationId,
            ReservationToken = groupReservation.ReservationToken,
            Status = status,
            SubmissionVersion = groupReservation.SubmissionVersion
        };

        return _dynamoDbContext.ToDocument(group).ToAttributeMap()["status"];
    }

    private static bool IsRetryableConflict(TransactionCanceledException exception) =>
        exception.CancellationReasons?.Any(reason =>
            reason.Code is "ConditionalCheckFailed" or "TransactionConflict") == true;
}