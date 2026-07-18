using System.Text.Json.Serialization;

namespace MRS.Application.Sync;

public sealed record SyncPushRequest(IReadOnlyList<SyncPushItem> Items);

public sealed record SyncPushItem(
	long OutboxId,
	string EntityType,
	string LocalClientUuid,
	string Operation,
	string? PayloadJson);

public sealed record SyncPushResponse(
	bool Ok,
	string Message,
	IReadOnlyList<SyncPushItemResult> Results);

public sealed record SyncPushItemResult(
	long OutboxId,
	bool Ok,
	string? Error);

public sealed record SyncPullRequest(DateTimeOffset? Since);

public sealed record SyncPullResponse(
	DateTimeOffset ServerTime,
	IReadOnlyList<SyncOrganizationRow> Organizations,
	IReadOnlyList<SyncFacilityRow> Facilities,
	IReadOnlyList<SyncFacilitySystemRow> FacilitySystems,
	IReadOnlyList<SyncInstallationRow> Installations,
	IReadOnlyList<SyncEquipmentTypeRow> EquipmentTypes,
	IReadOnlyList<TemplateSyncPayload> Templates,
	IReadOnlyList<SyncEngineerNotePullRow> EngineerNotes,
	IReadOnlyList<SyncScheduledVisitPullRow> ScheduledVisits,
	IReadOnlyList<SyncChecklistPullRow> Checklists,
	IReadOnlyList<SyncEquipmentModelRow> EquipmentModels,
	IReadOnlyList<SyncSystemEquipmentLinkRow> SystemEquipmentLinks);

public sealed record SyncEquipmentTypeRow(
	long Id,
	string TypeName,
	string? Code);

public sealed record SyncOrganizationRow(
	long Id,
	string FullName,
	string? ShortName,
	bool IsActive,
	string? LegalFormCode = null);

public sealed record SyncFacilityRow(
	long Id,
	long OrganizationId,
	string Name,
	string UiFlow,
	bool IsActive,
	string? ContractAddress = null,
	SyncAddressPayload? Address = null);

public sealed record SyncFacilitySystemRow(
	long Id,
	long FacilityId,
	string Name,
	string? Description,
	bool IsActive);

public sealed record SyncInstallationRow(
	long Id,
	long SystemId,
	long EquipmentTypeId,
	bool IsActive,
	string? CustomName = null,
	string? CustomSerialNumber = null,
	long? EquipmentModelId = null,
	string? CustomModelName = null);

public sealed record SyncEquipmentModelRow(
	long Id,
	long EquipmentTypeId,
	string? Manufacturer,
	string Name);

public sealed record SyncSystemEquipmentLinkRow(
	long SystemId,
	long EquipmentTypeId);

public sealed record SyncChecklistPullRow(
	ChecklistSyncPayload Payload,
	DateTimeOffset ServerUpdatedAt);

public sealed record ChecklistSyncPayload(
	[property: JsonPropertyName("clientUuid")] string ClientUuid,
	[property: JsonPropertyName("localId")] int LocalId,
	[property: JsonPropertyName("installationId")] int InstallationId,
	[property: JsonPropertyName("maintenanceTypeId")] int MaintenanceTypeId,
	[property: JsonPropertyName("checklistTemplateId")] int? ChecklistTemplateId,
	[property: JsonPropertyName("engineerId")] int EngineerId,
	[property: JsonPropertyName("startAt")] DateTimeOffset? StartAt,
	[property: JsonPropertyName("endAt")] DateTimeOffset? EndAt,
	[property: JsonPropertyName("status")] string Status,
	[property: JsonPropertyName("responses")] IReadOnlyList<ChecklistResponseSyncPayload> Responses);

public sealed record ChecklistResponseSyncPayload(
	[property: JsonPropertyName("templateItemId")] int TemplateItemId,
	[property: JsonPropertyName("textResponse")] string? TextResponse,
	[property: JsonPropertyName("numericResponse")] double? NumericResponse,
	[property: JsonPropertyName("booleanResponse")] bool? BooleanResponse,
	[property: JsonPropertyName("selectedOptionId")] int? SelectedOptionId,
	[property: JsonPropertyName("multiOptionIds")] IReadOnlyList<int>? MultiOptionIds);

public sealed record HierarchySyncPayload(
	[property: JsonPropertyName("clientUuid")] string ClientUuid,
	[property: JsonPropertyName("organization")] SyncOrganizationPayload Organization,
	[property: JsonPropertyName("facility")] SyncFacilityPayload Facility,
	[property: JsonPropertyName("facilitySystem")] SyncFacilitySystemPayload FacilitySystem,
	[property: JsonPropertyName("equipmentType")] SyncEquipmentTypePayload EquipmentType,
	[property: JsonPropertyName("installation")] SyncInstallationPayload Installation);

public sealed record SyncOrganizationPayload(
	[property: JsonPropertyName("localId")] int LocalId,
	[property: JsonPropertyName("fullName")] string FullName,
	[property: JsonPropertyName("shortName")] string? ShortName,
	[property: JsonPropertyName("legalFormCode")] string? LegalFormCode,
	[property: JsonPropertyName("isActive")] bool IsActive);

public sealed record SyncFacilityPayload(
	[property: JsonPropertyName("localId")] int LocalId,
	[property: JsonPropertyName("organizationLocalId")] int OrganizationLocalId,
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("contractAddress")] string? ContractAddress,
	[property: JsonPropertyName("uiFlow")] string UiFlow,
	[property: JsonPropertyName("isActive")] bool IsActive,
	[property: JsonPropertyName("address")] SyncAddressPayload Address);

public sealed record SyncAddressPayload(
	[property: JsonPropertyName("zipCode")] string? ZipCode,
	[property: JsonPropertyName("city")] string City,
	[property: JsonPropertyName("street")] string Street,
	[property: JsonPropertyName("building")] string Building,
	[property: JsonPropertyName("structure")] string? Structure,
	[property: JsonPropertyName("block")] string? Block);

public sealed record SyncFacilitySystemPayload(
	[property: JsonPropertyName("localId")] int LocalId,
	[property: JsonPropertyName("facilityLocalId")] int FacilityLocalId,
	[property: JsonPropertyName("name")] string Name,
	[property: JsonPropertyName("description")] string? Description,
	[property: JsonPropertyName("isActive")] bool IsActive);

public sealed record SyncEquipmentTypePayload(
	[property: JsonPropertyName("localId")] int LocalId,
	[property: JsonPropertyName("typeName")] string TypeName,
	[property: JsonPropertyName("code")] string? Code);

public sealed record SyncInstallationPayload(
	[property: JsonPropertyName("localId")] int LocalId,
	[property: JsonPropertyName("systemLocalId")] int SystemLocalId,
	[property: JsonPropertyName("equipmentTypeLocalId")] int EquipmentTypeLocalId,
	[property: JsonPropertyName("customName")] string? CustomName,
	[property: JsonPropertyName("customSerialNumber")] string? CustomSerialNumber,
	[property: JsonPropertyName("manufacturer")] string? Manufacturer,
	[property: JsonPropertyName("modelName")] string? ModelName,
	[property: JsonPropertyName("isActive")] bool IsActive);

public sealed record TemplateSyncPayload(
	[property: JsonPropertyName("clientUuid")] string ClientUuid,
	[property: JsonPropertyName("localId")] int LocalId,
	[property: JsonPropertyName("equipmentTypeId")] int EquipmentTypeId,
	[property: JsonPropertyName("maintenanceTypeId")] int MaintenanceTypeId,
	[property: JsonPropertyName("facilityId")] int? FacilityId,
	[property: JsonPropertyName("templateName")] string TemplateName,
	[property: JsonPropertyName("scenarioCode")] string? ScenarioCode,
	[property: JsonPropertyName("version")] int Version,
	[property: JsonPropertyName("topPlateText")] string? TopPlateText,
	[property: JsonPropertyName("introModalText")] string? IntroModalText,
	[property: JsonPropertyName("safetyModalText")] string? SafetyModalText,
	[property: JsonPropertyName("redButtonEnabled")] bool RedButtonEnabled,
	[property: JsonPropertyName("maintenanceTypeName")] string? MaintenanceTypeName,
	[property: JsonPropertyName("maintenanceTypeCode")] string? MaintenanceTypeCode,
	[property: JsonPropertyName("fields")] IReadOnlyList<TemplateFieldSyncPayload> Fields);

public sealed record TemplateFieldSyncPayload(
	[property: JsonPropertyName("sortOrder")] int SortOrder,
	[property: JsonPropertyName("fieldCode")] string? FieldCode,
	[property: JsonPropertyName("questionText")] string QuestionText,
	[property: JsonPropertyName("hintText")] string? HintText,
	[property: JsonPropertyName("fieldTypeName")] string FieldTypeName,
	[property: JsonPropertyName("validationRuleCode")] string? ValidationRuleCode,
	[property: JsonPropertyName("isRequired")] bool IsRequired,
	[property: JsonPropertyName("groupName")] string? GroupName,
	[property: JsonPropertyName("options")] IReadOnlyList<string> Options);

public sealed record EngineerNoteSyncPayload(
	[property: JsonPropertyName("clientUuid")] string ClientUuid,
	[property: JsonPropertyName("localId")] int LocalId,
	[property: JsonPropertyName("authorUserId")] int AuthorUserId,
	[property: JsonPropertyName("body")] string Body,
	[property: JsonPropertyName("deadlineDate")] DateOnly? DeadlineDate,
	[property: JsonPropertyName("title")] string? Title,
	[property: JsonPropertyName("facilityId")] int? FacilityId,
	[property: JsonPropertyName("scheduledVisitId")] int? ScheduledVisitId,
	[property: JsonPropertyName("checklistId")] int? ChecklistId,
	[property: JsonPropertyName("isCompleted")] bool IsCompleted,
	[property: JsonPropertyName("completedAt")] DateTimeOffset? CompletedAt,
	[property: JsonPropertyName("operation")] string Operation);

public sealed record ScheduledVisitSyncPayload(
	[property: JsonPropertyName("clientUuid")] string ClientUuid,
	[property: JsonPropertyName("localId")] int LocalId,
	[property: JsonPropertyName("facilityId")] int FacilityId,
	[property: JsonPropertyName("contactEmployeeId")] int? ContactEmployeeId,
	[property: JsonPropertyName("contactManualText")] string? ContactManualText,
	[property: JsonPropertyName("plannedStart")] DateOnly PlannedStart,
	[property: JsonPropertyName("plannedEnd")] DateOnly? PlannedEnd,
	[property: JsonPropertyName("notes")] string? Notes,
	[property: JsonPropertyName("prepSkipped")] bool PrepSkipped,
	[property: JsonPropertyName("status")] string Status,
	[property: JsonPropertyName("engineerUserIds")] IReadOnlyList<int> EngineerUserIds,
	[property: JsonPropertyName("operation")] string Operation);

public sealed record SyncEngineerNotePullRow(
	[property: JsonPropertyName("clientUuid")] string ClientUuid,
	[property: JsonPropertyName("id")] long Id,
	[property: JsonPropertyName("authorUserId")] int AuthorUserId,
	[property: JsonPropertyName("body")] string Body,
	[property: JsonPropertyName("deadlineDate")] DateOnly? DeadlineDate,
	[property: JsonPropertyName("title")] string? Title,
	[property: JsonPropertyName("facilityId")] int? FacilityId,
	[property: JsonPropertyName("scheduledVisitId")] int? ScheduledVisitId,
	[property: JsonPropertyName("checklistId")] int? ChecklistId,
	[property: JsonPropertyName("isCompleted")] bool IsCompleted,
	[property: JsonPropertyName("completedAt")] DateTimeOffset? CompletedAt,
	[property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt);

public sealed record SyncScheduledVisitPullRow(
	[property: JsonPropertyName("clientUuid")] string ClientUuid,
	[property: JsonPropertyName("id")] long Id,
	[property: JsonPropertyName("facilityId")] int FacilityId,
	[property: JsonPropertyName("contactEmployeeId")] int? ContactEmployeeId,
	[property: JsonPropertyName("contactManualText")] string? ContactManualText,
	[property: JsonPropertyName("plannedStart")] DateOnly PlannedStart,
	[property: JsonPropertyName("plannedEnd")] DateOnly? PlannedEnd,
	[property: JsonPropertyName("notes")] string? Notes,
	[property: JsonPropertyName("prepSkipped")] bool PrepSkipped,
	[property: JsonPropertyName("status")] string Status,
	[property: JsonPropertyName("engineerUserIds")] IReadOnlyList<int> EngineerUserIds,
	[property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt);
