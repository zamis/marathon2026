using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Repositories.GroupReservations;

public class GroupReservationRepository : IGroupReservationRepository
{
    private static readonly string GroupReservationsTableName = ResourceNames.GroupReservationsTableName;
    private readonly IDynamoDBContext _dynamoDbContext;
    private readonly IAmazonDynamoDB _dynamoDbClient;

    public GroupReservationRepository(IDynamoDBContext dynamoDbContext, IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbContext = dynamoDbContext ?? throw new ArgumentNullException(nameof(dynamoDbContext));
        _dynamoDbClient = dynamoDbClient ?? throw new ArgumentNullException(nameof(dynamoDbClient));
    }

    public async Task<GroupReservation?> GetGroupReservationByIdAsync(string groupReservationId)
    {
        return await _dynamoDbContext.LoadAsync<GroupReservation>(groupReservationId, DynamoConfig.LoadConfig(GroupReservationsTableName));
    }

    public async Task DeleteGroupReservationAsync(GroupReservation groupReservation)
    {
        await _dynamoDbContext.DeleteAsync(groupReservation, DynamoConfig.DeleteConfig(GroupReservationsTableName));
    }

    public async Task SaveGroupReservationAsync(GroupReservation groupReservation)
    {
        await _dynamoDbContext.SaveAsync(groupReservation, DynamoConfig.SaveConfig(GroupReservationsTableName));
    }

    public async Task<bool> TryCloseOpenGroupReservationAsync(GroupReservation groupReservation)
    {
        AttributeValue openStatus = GetGroupStatusAttribute(groupReservation, GroupReservationStatus.Open);
        AttributeValue closedStatus = GetGroupStatusAttribute(groupReservation, GroupReservationStatus.Closed);

        try
        {
            await _dynamoDbClient.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = GroupReservationsTableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["groupReservationId"] = new() { S = groupReservation.GroupReservationId }
                },
                UpdateExpression = "SET #status = :closed, #version = if_not_exists(#version, :zero) + :one",
                ConditionExpression = "#status = :open",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#status"] = "status",
                    ["#version"] = "submissionVersion"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":open"] = openStatus,
                    [":closed"] = closedStatus,
                    [":zero"] = new() { N = "0" },
                    [":one"] = new() { N = "1" }
                }
            });

            return true;
        }
        catch (AmazonDynamoDBException exception) when (
            exception.ErrorCode is "ConditionalCheckFailedException" or "TransactionConflictException")
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

    public async Task<GroupReservation?> GetGroupReservationByTokenAsync(string token)
    {
        var results = await _dynamoDbContext.QueryAsync<GroupReservation?>(token, new QueryConfig
        {
            OverrideTableName = GroupReservationsTableName,
            IndexName = GlobalSecondaryIndexes.GroupReservation.ReservationToken,
        }).GetRemainingAsync();

        return results.FirstOrDefault();
    }

    public async Task<GroupReservation?> GetByReservationIdAsync(string reservationId)
    {
        var results = await _dynamoDbContext.ScanAsync<GroupReservation>(
            [new ScanCondition("reservationId", ScanOperator.Equal, reservationId)],
            new ScanConfig
            {
                OverrideTableName = GroupReservationsTableName,
                ConsistentRead = true
            }).GetRemainingAsync();

        return results.FirstOrDefault();
    }
}
