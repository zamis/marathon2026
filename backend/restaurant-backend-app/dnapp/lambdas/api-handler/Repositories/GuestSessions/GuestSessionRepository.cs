using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Repositories.GuestSessions;

public class GuestSessionRepository(
    IDynamoDBContext context,
    IAmazonDynamoDB? dynamoDbClient = null) : IGuestSessionRepository
{
    public async Task<GuestSession?> GetByGuestSessionId(string id) =>
        await context.LoadAsync<GuestSession?>(id, DynamoConfig.LoadConfig(ResourceNames.GuestSessionTableName));

    public async Task<GuestSession?> GetByGroupReservationId(string groupReservationId)
    {
        List<GuestSession> result = await GetAllByGroupReservationIdAsync(groupReservationId);
        return result.FirstOrDefault();
    }

    public async Task<GuestSession?> GetByTokenHash(string tokenHash)
    {
        List<GuestSession> result = await context.QueryAsync<GuestSession>(tokenHash, new QueryConfig
        {
            OverrideTableName = ResourceNames.GuestSessionTableName,
            IndexName = GlobalSecondaryIndexes.GuestSession.SessionTokenHash
        }).GetRemainingAsync();

        return result.FirstOrDefault();
    }

    public async Task<List<GuestSession>> GetAllByGroupReservationIdAsync(string groupReservationId)
    {
        return await context.ScanAsync<GuestSession>(
            [new ScanCondition("groupReservationId", ScanOperator.Equal, groupReservationId)],
            new ScanConfig
            {
                OverrideTableName = ResourceNames.GuestSessionTableName,
                ConsistentRead = true
            }).GetRemainingAsync();
    }

    public async Task<List<GuestSession>> GetAllForSubmissionByGroupReservationIdAsync(string groupReservationId)
    {
        return await GetAllByGroupReservationIdAsync(groupReservationId);
    }

    public async Task SaveAsync(GuestSession guestSession)
    {
        IAmazonDynamoDB client = dynamoDbClient
            ?? throw new InvalidOperationException("A DynamoDB client is required for transactional session creation.");
        Dictionary<string, AttributeValue> sessionAttributes = context.ToDocument(guestSession).ToAttributeMap();

        await client.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems =
            [
                new TransactWriteItem
                {
                    Put = new Put
                    {
                        TableName = ResourceNames.GuestSessionTableName,
                        Item = sessionAttributes,
                        ConditionExpression = "attribute_not_exists(#sessionId)",
                        ExpressionAttributeNames = new Dictionary<string, string>
                        {
                            ["#sessionId"] = "guestSessionId"
                        }
                    }
                },
                new TransactWriteItem
                {
                    Update = CreateOpenGroupVersionUpdate(guestSession.GroupReservationId)
                }
            ]
        });
    }

    public async Task<bool> TrySubmitCartAsync(GuestSession guestSession, GroupReservation groupReservation)
    {
        if (groupReservation.Status != GroupReservationStatus.Open)
            return false;

        IAmazonDynamoDB client = dynamoDbClient
            ?? throw new InvalidOperationException("A DynamoDB client is required for transactional cart submission.");
        AttributeValue openStatus = context.ToDocument(groupReservation).ToAttributeMap()["status"];

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
                            TableName = ResourceNames.GuestSessionTableName,
                            Key = new Dictionary<string, AttributeValue>
                            {
                                ["guestSessionId"] = new() { S = guestSession.Id }
                            },
                            UpdateExpression = "SET #submitted = :true",
                            ConditionExpression = "attribute_exists(#sessionId) AND (attribute_not_exists(#submitted) OR #submitted = :false)",
                            ExpressionAttributeNames = new Dictionary<string, string>
                            {
                                ["#sessionId"] = "guestSessionId",
                                ["#submitted"] = "isSubmitted"
                            },
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                            {
                                [":true"] = new() { BOOL = true },
                                [":false"] = new() { BOOL = false }
                            }
                        }
                    },
                    new TransactWriteItem
                    {
                        Update = new Update
                        {
                            TableName = ResourceNames.GroupReservationsTableName,
                            Key = new Dictionary<string, AttributeValue>
                            {
                                ["groupReservationId"] = new() { S = groupReservation.GroupReservationId }
                            },
                            UpdateExpression = "SET #version = if_not_exists(#version, :zero) + :one",
                            ConditionExpression = "#status = :open",
                            ExpressionAttributeNames = new Dictionary<string, string>
                            {
                                ["#status"] = "status",
                                ["#version"] = "submissionVersion"
                            },
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                            {
                                [":open"] = openStatus,
                                [":zero"] = new() { N = "0" },
                                [":one"] = new() { N = "1" }
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

    public async Task<bool> TryUpdateCartAsync(GuestSession guestSession, GroupReservation groupReservation)
    {
        IAmazonDynamoDB client = dynamoDbClient
            ?? throw new InvalidOperationException("A DynamoDB client is required for transactional cart updates.");
        Dictionary<string, AttributeValue> sessionAttributes = context.ToDocument(guestSession).ToAttributeMap();

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
                            TableName = ResourceNames.GuestSessionTableName,
                            Key = new Dictionary<string, AttributeValue>
                            {
                                ["guestSessionId"] = new() { S = guestSession.Id }
                            },
                            UpdateExpression = "SET #dishes = :dishes, #sessionVersion = if_not_exists(#sessionVersion, :zero) + :one",
                            ConditionExpression = "#groupReservationId = :groupId AND (attribute_not_exists(#sessionVersion) OR #sessionVersion = :expectedSessionVersion) AND (attribute_not_exists(#submitted) OR #submitted = :false)",
                            ExpressionAttributeNames = new Dictionary<string, string>
                            {
                                ["#dishes"] = "dishes",
                                ["#sessionVersion"] = "sessionVersion",
                                ["#groupReservationId"] = "groupReservationId",
                                ["#submitted"] = "isSubmitted"
                            },
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                            {
                                [":dishes"] = sessionAttributes["dishes"],
                                [":zero"] = new() { N = "0" },
                                [":one"] = new() { N = "1" },
                                [":expectedSessionVersion"] = new()
                                {
                                    N = guestSession.SessionVersion.ToString(CultureInfo.InvariantCulture)
                                },
                                [":false"] = new() { BOOL = false },
                                [":groupId"] = new() { S = guestSession.GroupReservationId }
                            }
                        }
                    },
                    new TransactWriteItem
                    {
                        Update = CreateOpenGroupVersionUpdate(
                            groupReservation.GroupReservationId,
                            groupReservation.SubmissionVersion)
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

    public async Task<int> CountByGroupReservationId(string groupReservationId)
    {
        List<GuestSession> result = await GetAllByGroupReservationIdAsync(groupReservationId);
        return result.Count(session => !session.IsOwnerSession);
    }

    public async Task DeleteAsync(GuestSession session)
    {
        IAmazonDynamoDB client = dynamoDbClient
            ?? throw new InvalidOperationException("A DynamoDB client is required for transactional session deletion.");

        try
        {
            await client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem
                    {
                        Delete = new Delete
                        {
                            TableName = ResourceNames.GuestSessionTableName,
                            Key = new Dictionary<string, AttributeValue>
                            {
                                ["guestSessionId"] = new() { S = session.Id }
                            },
                            ConditionExpression = "#groupReservationId = :groupId",
                            ExpressionAttributeNames = new Dictionary<string, string>
                            {
                                ["#groupReservationId"] = "groupReservationId"
                            },
                            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                            {
                                [":groupId"] = new() { S = session.GroupReservationId }
                            }
                        }
                    },
                    new TransactWriteItem
                    {
                        Update = CreateOpenGroupVersionUpdate(session.GroupReservationId)
                    }
                ]
            });
        }
        catch (TransactionCanceledException exception) when (IsGroupStatusConditionFailure(exception))
        {
            GroupReservation? currentGroup = await context.LoadAsync<GroupReservation>(
                session.GroupReservationId,
                DynamoConfig.LoadConfig(ResourceNames.GroupReservationsTableName));

            if (currentGroup?.Status is GroupReservationStatus.Closed or GroupReservationStatus.Submitted)
                throw new GroupReservationNotOpenException();

            throw;
        }
    }

    private Update CreateOpenGroupVersionUpdate(string groupReservationId, long? expectedVersion = null)
    {
        var group = new GroupReservation
        {
            GroupReservationId = groupReservationId,
            ReservationId = string.Empty,
            ReservationToken = string.Empty,
            Status = GroupReservationStatus.Open
        };
        var expressionValues = new Dictionary<string, AttributeValue>
        {
            [":open"] = context.ToDocument(group).ToAttributeMap()["status"],
            [":zero"] = new() { N = "0" },
            [":one"] = new() { N = "1" }
        };
        string versionCondition = string.Empty;

        if (expectedVersion.HasValue)
        {
            expressionValues[":expectedVersion"] = new()
            {
                N = expectedVersion.Value.ToString(CultureInfo.InvariantCulture)
            };
            versionCondition = " AND (attribute_not_exists(#version) OR #version = :expectedVersion)";
        }

        return new Update
        {
            TableName = ResourceNames.GroupReservationsTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["groupReservationId"] = new() { S = groupReservationId }
            },
            UpdateExpression = "SET #version = if_not_exists(#version, :zero) + :one",
            ConditionExpression = "#status = :open" + versionCondition,
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#status"] = "status",
                ["#version"] = "submissionVersion"
            },
            ExpressionAttributeValues = expressionValues
        };
    }

    private AttributeValue GetGroupStatusAttribute(GroupReservation groupReservation, GroupReservationStatus status)
    {
        GroupReservation group = new()
        {
            GroupReservationId = groupReservation.GroupReservationId,
            ReservationId = groupReservation.ReservationId,
            ReservationToken = groupReservation.ReservationToken,
            Status = status,
            SubmissionVersion = groupReservation.SubmissionVersion
        };

        return context.ToDocument(group).ToAttributeMap()["status"];
    }

    private static bool IsRetryableConflict(TransactionCanceledException exception) =>
        exception.CancellationReasons?.Any(reason =>
            reason.Code is "ConditionalCheckFailed" or "TransactionConflict") == true;

    private static bool IsGroupStatusConditionFailure(TransactionCanceledException exception) =>
        exception.CancellationReasons is { Count: > 1 } reasons &&
        reasons[1].Code == "ConditionalCheckFailed";
}
