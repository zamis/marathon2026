using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Repositories.GroupReservations;
using Moq;
using System.Threading;

namespace APIHandler.Tests.Repositories;

[Collection("Resource environment")]
public class GroupReservationRepositoryTests
{
    static GroupReservationRepositoryTests() =>
        Environment.SetEnvironmentVariable("groupReservationsTable", "groups-test");

    [Fact]
    public async Task GetByReservationIdAsync_UsesStronglyConsistentScan()
    {
        const string reservationId = "reservation-1";
        var groups = new List<GroupReservation>
        {
            new()
            {
                GroupReservationId = "group-1",
                ReservationId = reservationId,
                ReservationToken = "token",
                Status = GroupReservationStatus.Open
            }
        };
        var context = new Mock<IDynamoDBContext>(MockBehavior.Strict);
        var search = new Mock<AsyncSearch<GroupReservation>>();
        search.Setup(result => result.GetRemainingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(groups);
        IEnumerable<ScanCondition>? conditions = null;
        ScanConfig? scanConfig = null;
        context.Setup(db => db.ScanAsync<GroupReservation>(
                It.IsAny<IEnumerable<ScanCondition>>(), It.IsAny<ScanConfig>()))
            .Callback<IEnumerable<ScanCondition>, ScanConfig>((filters, config) =>
            {
                conditions = filters;
                scanConfig = config;
            })
            .Returns(search.Object);

        var repository = new GroupReservationRepository(context.Object, Mock.Of<IAmazonDynamoDB>());

        var result = await repository.GetByReservationIdAsync(reservationId);

        Assert.Same(groups[0], result);
        Assert.NotNull(scanConfig);
        Assert.True(scanConfig.ConsistentRead);
        Assert.Equal(ResourceNames.GroupReservationsTableName, scanConfig.OverrideTableName);
        var condition = Assert.Single(conditions!);
        Assert.Equal("reservationId", condition.PropertyName);
        Assert.Equal(ScanOperator.Equal, condition.Operator);
        Assert.Equal(reservationId, Assert.Single(condition.Values));
        search.Verify(result => result.GetRemainingAsync(It.IsAny<CancellationToken>()), Times.Once);
        context.Verify(db => db.ScanAsync<GroupReservation>(
            It.IsAny<IEnumerable<ScanCondition>>(), It.IsAny<ScanConfig>()), Times.Once);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryCloseOpenGroupReservationAsync_UsesConditionalStatusUpdateAndIncrementsVersion()
    {
        var context = new Mock<IDynamoDBContext>(MockBehavior.Strict);
        context.Setup(db => db.ToDocument(It.IsAny<GroupReservation>()))
            .Returns((GroupReservation group) => Document.FromJson(
                $$"""{"status":"{{group.Status}}"}"""));
        var dynamoDb = new Mock<IAmazonDynamoDB>(MockBehavior.Strict);
        UpdateItemRequest? captured = null;
        dynamoDb.Setup(client => client.UpdateItemAsync(
                It.IsAny<UpdateItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateItemRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new UpdateItemResponse());

        var repository = new GroupReservationRepository(context.Object, dynamoDb.Object);
        var group = new GroupReservation
        {
            GroupReservationId = "group-1",
            ReservationId = "reservation-1",
            ReservationToken = "token",
            Status = GroupReservationStatus.Open,
            SubmissionVersion = 2
        };

        bool closed = await repository.TryCloseOpenGroupReservationAsync(group);

        Assert.True(closed);
        Assert.NotNull(captured);
        Assert.Equal(ResourceNames.GroupReservationsTableName, captured.TableName);
        Assert.Equal(group.GroupReservationId, captured.Key["groupReservationId"].S);
        Assert.Equal("#status = :open", captured.ConditionExpression);
        Assert.Contains("SET #status = :closed", captured.UpdateExpression);
        Assert.Contains("if_not_exists(#version, :zero) + :one", captured.UpdateExpression);
        Assert.Equal("Open", captured.ExpressionAttributeValues[":open"].S);
        Assert.Equal("Closed", captured.ExpressionAttributeValues[":closed"].S);
    }
}
