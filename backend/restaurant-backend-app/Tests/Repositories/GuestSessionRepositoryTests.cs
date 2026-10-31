using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.GuestSessions;
using Moq;
using System.Threading;

namespace APIHandler.Tests.Repositories;

[Collection("Resource environment")]
public class GuestSessionRepositoryTests
{
    static GuestSessionRepositoryTests()
    {
        Environment.SetEnvironmentVariable("guestSessionTable", "guest-sessions-test");
        Environment.SetEnvironmentVariable("groupReservationsTable", "groups-test");
    }

    [Fact]
    public async Task GetAllByGroupReservationIdAsync_UsesStronglyConsistentScanInsteadOfGsi()
    {
        const string groupId = "group-1";
        var sessions = new List<GuestSession>
        {
            new() { Id = "session-1", GroupReservationId = groupId, GuestName = "Guest 1", Dishes = [] }
        };
        var context = new Mock<IDynamoDBContext>(MockBehavior.Strict);
        var search = new Mock<AsyncSearch<GuestSession>>();
        search.Setup(result => result.GetRemainingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        IEnumerable<ScanCondition>? conditions = null;
        ScanConfig? scanConfig = null;
        context.Setup(db => db.ScanAsync<GuestSession>(
                It.IsAny<IEnumerable<ScanCondition>>(), It.IsAny<ScanConfig>()))
            .Callback<IEnumerable<ScanCondition>, ScanConfig>((filters, config) =>
            {
                conditions = filters;
                scanConfig = config;
            })
            .Returns(search.Object);

        var repository = new GuestSessionRepository(context.Object);

        var result = await repository.GetAllByGroupReservationIdAsync(groupId);

        Assert.Same(sessions, result);
        Assert.NotNull(scanConfig);
        Assert.True(scanConfig.ConsistentRead);
        Assert.Equal(ResourceNames.GuestSessionTableName, scanConfig.OverrideTableName);
        var condition = Assert.Single(conditions!);
        Assert.Equal("groupReservationId", condition.PropertyName);
        Assert.Equal(ScanOperator.Equal, condition.Operator);
        Assert.Equal(groupId, Assert.Single(condition.Values));
        search.Verify(result => result.GetRemainingAsync(It.IsAny<CancellationToken>()), Times.Once);
        context.Verify(db => db.ScanAsync<GuestSession>(
            It.IsAny<IEnumerable<ScanCondition>>(), It.IsAny<ScanConfig>()), Times.Once);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryUpdateCartAsync_UsesAtomicVersionConditionsAndReturnsRetryableConflict()
    {
        var context = CreateContext();
        var dynamoDb = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        TransactWriteItemsRequest? captured = null;
        dynamoDb.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => captured = request)
            .ThrowsAsync(new TransactionCanceledException("Version conflict")
            {
                CancellationReasons = [new CancellationReason { Code = "ConditionalCheckFailed" }]
            });
        var repository = new GuestSessionRepository(context.Object, dynamoDb.Object);
        var session = CreateSession();
        session.SessionVersion = 4;
        var group = CreateGroup();
        group.SubmissionVersion = 8;

        bool updated = await repository.TryUpdateCartAsync(session, group);

        Assert.False(updated);
        Assert.NotNull(captured);
        Assert.Equal(2, captured.TransactItems.Count);
        var sessionUpdate = Assert.IsType<Update>(captured.TransactItems[0].Update);
        Assert.Equal(ResourceNames.GuestSessionTableName, sessionUpdate.TableName);
        Assert.Contains("#groupReservationId = :groupId", sessionUpdate.ConditionExpression);
        Assert.Contains("#sessionVersion = :expectedSessionVersion", sessionUpdate.ConditionExpression);
        Assert.Contains("#submitted = :false", sessionUpdate.ConditionExpression);
        Assert.Equal("4", sessionUpdate.ExpressionAttributeValues[":expectedSessionVersion"].N);
        var groupUpdate = Assert.IsType<Update>(captured.TransactItems[1].Update);
        Assert.Equal(ResourceNames.GroupReservationsTableName, groupUpdate.TableName);
        Assert.Contains("#status = :open", groupUpdate.ConditionExpression);
        Assert.Contains("#version = :expectedVersion", groupUpdate.ConditionExpression);
        Assert.Equal("8", groupUpdate.ExpressionAttributeValues[":expectedVersion"].N);
    }

    [Fact]
    public async Task TrySubmitCartAsync_ConditionallySubmitsOnlyAnOpenUnsubmittedSession()
    {
        var context = CreateContext();
        var dynamoDb = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        TransactWriteItemsRequest? captured = null;
        dynamoDb.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var repository = new GuestSessionRepository(context.Object, dynamoDb.Object);
        var session = CreateSession();
        var group = CreateGroup();

        bool submitted = await repository.TrySubmitCartAsync(session, group);

        Assert.True(submitted);
        Assert.NotNull(captured);
        Assert.Equal(2, captured.TransactItems.Count);
        var sessionUpdate = Assert.IsType<Update>(captured.TransactItems[0].Update);
        Assert.Contains("attribute_exists(#sessionId)", sessionUpdate.ConditionExpression);
        Assert.Contains("#submitted = :false", sessionUpdate.ConditionExpression);
        Assert.Equal(true, sessionUpdate.ExpressionAttributeValues[":true"].BOOL);
        var groupUpdate = Assert.IsType<Update>(captured.TransactItems[1].Update);
        Assert.Contains("#status = :open", groupUpdate.ConditionExpression);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MembershipTransactions_RequireOpenGroupAndConditionOnSessionMembership(bool createSession)
    {
        var context = CreateContext();
        var dynamoDb = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        TransactWriteItemsRequest? captured = null;
        dynamoDb.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new TransactWriteItemsResponse());
        var repository = new GuestSessionRepository(context.Object, dynamoDb.Object);
        var session = CreateSession();

        if (createSession)
            await repository.SaveAsync(session);
        else
            await repository.DeleteAsync(session);

        Assert.NotNull(captured);
        Assert.Equal(2, captured.TransactItems.Count);
        var membershipWrite = captured.TransactItems[0];
        var groupUpdate = Assert.IsType<Update>(captured.TransactItems[1].Update);
        Assert.Equal(ResourceNames.GroupReservationsTableName, groupUpdate.TableName);
        Assert.Contains("#status = :open", groupUpdate.ConditionExpression);
        Assert.Contains("if_not_exists(#version, :zero) + :one", groupUpdate.UpdateExpression);

        if (createSession)
        {
            var put = Assert.IsType<Put>(membershipWrite.Put);
            Assert.Equal(ResourceNames.GuestSessionTableName, put.TableName);
            Assert.Contains("attribute_not_exists(#sessionId)", put.ConditionExpression);
        }
        else
        {
            var delete = Assert.IsType<Delete>(membershipWrite.Delete);
            Assert.Equal(ResourceNames.GuestSessionTableName, delete.TableName);
            Assert.Contains("#groupReservationId = :groupId", delete.ConditionExpression);
            Assert.Equal(session.GroupReservationId, delete.ExpressionAttributeValues[":groupId"].S);
        }
    }

    [Theory]
    [InlineData(GroupReservationStatus.Closed)]
    [InlineData(GroupReservationStatus.Submitted)]
    public async Task DeleteAsync_WhenGroupStatusConditionFailsForClosedOrSubmittedGroup_ThrowsNotOpen(
        GroupReservationStatus status)
    {
        var context = CreateContext();
        LoadConfig? loadConfig = null;
        context.Setup(db => db.LoadAsync<GroupReservation>(
                It.IsAny<object>(), It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()))
            .Callback<object, LoadConfig, CancellationToken>((_, config, _) => loadConfig = config)
            .ReturnsAsync(new GroupReservation
            {
                GroupReservationId = "group-1",
                ReservationId = "reservation-1",
                ReservationToken = "token",
                Status = status
            });
        var dynamoDb = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        dynamoDb.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransactionCanceledException("Group is no longer open")
            {
                CancellationReasons =
                [
                    new CancellationReason { Code = "None" },
                    new CancellationReason { Code = "ConditionalCheckFailed" }
                ]
            });
        var repository = new GuestSessionRepository(context.Object, dynamoDb.Object);

        await Assert.ThrowsAsync<GroupReservationNotOpenException>(
            () => repository.DeleteAsync(CreateSession()));

        context.Verify(db => db.LoadAsync<GroupReservation>(
            "group-1", It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(loadConfig);
        Assert.True(loadConfig.ConsistentRead);
        Assert.Equal(ResourceNames.GroupReservationsTableName, loadConfig.OverrideTableName);
    }

    [Fact]
    public async Task DeleteAsync_WhenSessionConditionFails_DoesNotMisclassifyAsClosedGroup()
    {
        var context = CreateContext();
        var dynamoDb = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        var cancellation = new TransactionCanceledException("Session condition failed")
        {
            CancellationReasons =
            [
                new CancellationReason { Code = "ConditionalCheckFailed" },
                new CancellationReason { Code = "None" }
            ]
        };
        dynamoDb.Setup(client => client.TransactWriteItemsAsync(
                It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(cancellation);
        var repository = new GuestSessionRepository(context.Object, dynamoDb.Object);

        var thrown = await Assert.ThrowsAsync<TransactionCanceledException>(
            () => repository.DeleteAsync(CreateSession()));

        Assert.Same(cancellation, thrown);
        context.Verify(db => db.LoadAsync<GroupReservation>(
            It.IsAny<object>(), It.IsAny<LoadConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IDynamoDBContext> CreateContext()
    {
        var context = new Mock<IDynamoDBContext>(MockBehavior.Strict);
        context.Setup(db => db.ToDocument(It.IsAny<GuestSession>()))
            .Returns(Document.FromJson("""{"dishes":[{"dishId":"dish-1","quantity":2}]}"""));
        context.Setup(db => db.ToDocument(It.IsAny<GroupReservation>()))
            .Returns((GroupReservation group) => Document.FromJson(
                $$"""{"status":"{{group.Status}}"}"""));
        return context;
    }

    private static GuestSession CreateSession() => new()
    {
        Id = "session-1",
        GroupReservationId = "group-1",
        GuestName = "Guest",
        Dishes = [new OrderItem { DishId = "dish-1", Quantity = 2 }]
    };

    private static GroupReservation CreateGroup() => new()
    {
        GroupReservationId = "group-1",
        ReservationId = "reservation-1",
        ReservationToken = "token",
        Status = GroupReservationStatus.Open
    };
}
