namespace MRS.Application.Sync;

public sealed record SyncOutboxEntry(
	long Id,
	string EntityType,
	string LocalClientUuid,
	string Operation,
	string? PayloadJson,
	DateTimeOffset CreatedAt,
	int RetryCount);

public sealed record SyncOutboxEnqueueRequest(
	string EntityType,
	string LocalClientUuid,
	string Operation,
	string PayloadJson);

public sealed record SyncEntityRef(string EntityType, int LocalId);
