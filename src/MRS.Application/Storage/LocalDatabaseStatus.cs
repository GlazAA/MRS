namespace MRS.Application.Storage;

public sealed record LocalDatabaseStatus(
	bool Ready,
	int SchemaVersion,
	int FieldTypeCount,
	int MaintenanceTypeCount,
	string? Error);
