using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Repositories.Orders;
using Moq;
using System.Threading;

namespace APIHandler.Tests.Repositories;

[Collection("Resource environment")]
public class OrderRepositoryTests
{
    static OrderRepositoryTests()
    {
        Environment.SetEnvironmentVariable("ordersTable", "orders-test");
        Environment.SetEnvironmentVariable("groupReservationsTable", "groups-test");
    }

    [Fact]
    public async Task TrySaveSubmittedOrderAndGroupReservationAsync_TransactionConflictReturnsFalseWithoutSplittingWrites()
    {
        var context = new Mock<IDynamoDBContext>(MockBehavior.Strict);
        var dynamoDb = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        var order = new Order
        {
            OrderId = "order-1",
            CustomerId = "owner-1",
            ReservationId = "reservation-1",
            ReservationAddress = "Address",
            ReservationTime = "Time",
            Status = OrderStatus.SubmittedByCustomer,
            Dishes = [new OrderItem { DishId = "dish-1", Quantity = 2 }],
            Version = 12
        };
        var group = new GroupReservation
        {
            GroupReservationId = "group-1",
            ReservationId = order.ReservationId,
            ReservationToken = "token",
            Status = GroupReservationStatus.Open,
            SubmissionVersion = 12
        };

        context.Setup(db => db.ToDocument(It.Is<Order>(candidate => candidate == order)))
            .Returns(Document.FromJson("{\"orderId\":\"order-1\",\"orderVersion\":13,\"status\":\"SubmittedByCustomer\"}"));
        context.Setup(db => db.ToDocument(It.Is<GroupReservation>(candidate =>
                candidate.Status == GroupReservationStatus.Open)))
            .Returns(Document.FromJson("{\"status\":\"Open\"}"));
        context.Setup(db => db.ToDocument(It.Is<GroupReservation>(candidate =>
                candidate.Status == GroupReservationStatus.Submitted)))
            .Returns(Document.FromJson("{\"status\":\"Submitted\"}"));

        TransactWriteItemsRequest? transaction = null;
        dynamoDb.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => transaction = request)
            .ThrowsAsync(new TransactionCanceledException("Conditional version check failed")
            {
                CancellationReasons = [new CancellationReason { Code = "ConditionalCheckFailed" }]
            });

        var repository = new OrderRepository(context.Object, dynamoDb.Object);

        bool saved = await repository.TrySaveSubmittedOrderAndGroupReservationAsync(order, group);

        Assert.False(saved);
        Assert.NotNull(transaction);
        Assert.Equal(2, transaction.TransactItems.Count);
        Assert.NotNull(transaction.TransactItems[0].Update);
        var groupUpdate = transaction.TransactItems[0].Update;
        Assert.Equal(ResourceNames.GroupReservationsTableName, groupUpdate.TableName);
        Assert.Contains("#status = :open", groupUpdate.ConditionExpression);
        Assert.Contains("#version = :expected", groupUpdate.ConditionExpression);
        Assert.Contains("SET #status = :submitted", groupUpdate.UpdateExpression);
        Assert.Contains("if_not_exists(#version, :zero) + :one", groupUpdate.UpdateExpression);
        Assert.Equal("12", groupUpdate.ExpressionAttributeValues[":expected"].N);
        Assert.Equal("Open", groupUpdate.ExpressionAttributeValues[":open"].S);
        Assert.Equal("Submitted", groupUpdate.ExpressionAttributeValues[":submitted"].S);
        Assert.NotNull(transaction.TransactItems[1].Put);
        Assert.Equal(ResourceNames.OrdersTableName, transaction.TransactItems[1].Put.TableName);
        Assert.Contains("attribute_exists(#orderId)", transaction.TransactItems[1].Put.ConditionExpression);
        Assert.Contains("#orderVersion = :expectedOrderVersion", transaction.TransactItems[1].Put.ConditionExpression);
        Assert.Equal("12", transaction.TransactItems[1].Put.ExpressionAttributeValues[":expectedOrderVersion"].N);
        Assert.Equal("13", transaction.TransactItems[1].Put.Item["orderVersion"].N);
        Assert.Equal(13, order.Version);
        dynamoDb.Verify(client => client.TransactWriteItemsAsync(
            It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrySaveSubmittedOrderAndGroupReservationAsync_WhenTransactionSucceedsReturnsTrue()
    {
        var context = new Mock<IDynamoDBContext>();
        var dynamoDb = new Mock<IAmazonDynamoDB>();
        var order = new Order
        {
            OrderId = "order-1",
            CustomerId = "owner-1",
            ReservationId = "reservation-1",
            ReservationAddress = "Address",
            ReservationTime = "Time",
            Status = OrderStatus.SubmittedByCustomer,
            Dishes = [new OrderItem { DishId = "dish-1", Quantity = 2 }]
        };
        var group = new GroupReservation
        {
            GroupReservationId = "group-1",
            ReservationId = order.ReservationId,
            ReservationToken = "token",
            Status = GroupReservationStatus.Open,
            SubmissionVersion = 3
        };
        context.Setup(db => db.ToDocument(It.IsAny<Order>()))
            .Returns(Document.FromJson("{\"orderId\":\"order-1\"}"));
        context.Setup(db => db.ToDocument(It.Is<GroupReservation>(candidate =>
                candidate.Status == GroupReservationStatus.Open)))
            .Returns(Document.FromJson("{\"status\":\"Open\"}"));
        context.Setup(db => db.ToDocument(It.Is<GroupReservation>(candidate =>
                candidate.Status == GroupReservationStatus.Submitted)))
            .Returns(Document.FromJson("{\"status\":\"Submitted\"}"));
        dynamoDb.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransactWriteItemsResponse());

        var repository = new OrderRepository(context.Object, dynamoDb.Object);

        bool saved = await repository.TrySaveSubmittedOrderAndGroupReservationAsync(order, group);

        Assert.True(saved);
        dynamoDb.Verify(client => client.TransactWriteItemsAsync(
            It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
