using System.Text.Json;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

internal static class SyncEntityLockExtractor
{
	internal static IReadOnlyList<SyncEntityRef> Extract(string entityType, string payloadJson)
	{
		if (string.IsNullOrWhiteSpace(payloadJson))
			return Array.Empty<SyncEntityRef>();

		using var doc = JsonDocument.Parse(payloadJson);
		var root = doc.RootElement;

		return entityType.ToLowerInvariant() switch
		{
			"hierarchy" => ExtractHierarchy(root),
			"checklist_template" => Single("checklist_template", root, "localId"),
			"checklist" => Single("checklist", root, "localId"),
			"engineer_note" => Single("engineer_note", root, "localId"),
			"scheduled_visit" => Single("scheduled_visit", root, "localId"),
			_ => Array.Empty<SyncEntityRef>()
		};
	}

	private static IReadOnlyList<SyncEntityRef> Single(string entityType, JsonElement root, string propertyName)
	{
		if (!root.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Number)
			return Array.Empty<SyncEntityRef>();
		return [new SyncEntityRef(entityType, prop.GetInt32())];
	}

	private static IReadOnlyList<SyncEntityRef> ExtractHierarchy(JsonElement root)
	{
		var list = new List<SyncEntityRef>(5);
		AddNested(list, "organization", root, "organization");
		AddNested(list, "facility", root, "facility");
		AddNested(list, "facility_system", root, "facilitySystem");
		AddNested(list, "equipment_type", root, "equipmentType");
		AddNested(list, "installation", root, "installation");
		return list;
	}

	private static void AddNested(List<SyncEntityRef> list, string entityType, JsonElement root, string propertyName)
	{
		if (!root.TryGetProperty(propertyName, out var nested))
			return;
		if (!nested.TryGetProperty("localId", out var idProp) || idProp.ValueKind != JsonValueKind.Number)
			return;
		list.Add(new SyncEntityRef(entityType, idProp.GetInt32()));
	}
}
