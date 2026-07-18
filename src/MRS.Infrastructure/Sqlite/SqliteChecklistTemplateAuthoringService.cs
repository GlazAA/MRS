using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Facilities;
using MRS.Application.Storage;
using MRS.Application.Sync;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteChecklistTemplateAuthoringService : IChecklistTemplateAuthoringService
{
    private readonly ILocalDatabasePath _paths;
    private readonly ILocalDatabaseBootstrapper _bootstrapper;
    private readonly ISyncOutboxService _outbox;

    public SqliteChecklistTemplateAuthoringService(
        ILocalDatabasePath paths,
        ILocalDatabaseBootstrapper bootstrapper,
        ISyncOutboxService outbox)
    {
        _paths = paths;
        _bootstrapper = bootstrapper;
        _outbox = outbox;
    }

    public async Task<IReadOnlyList<HierarchyOption>> GetEquipmentTypesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, type_name
            FROM equipment_types
            ORDER BY type_name;
            """;

        var list = new List<HierarchyOption>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            list.Add(new HierarchyOption(reader.GetInt32(0), reader.GetString(1)));
        return list;
    }

    public async Task<IReadOnlyList<MaintenanceTypeOption>> GetMaintenanceTypesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, type_name, code
            FROM maintenance_types
            ORDER BY type_name;
            """;

        var list = new List<MaintenanceTypeOption>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new MaintenanceTypeOption(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

		return list;
	}

	public async Task<IReadOnlyList<TemplateCloneSourceOption>> ListTemplatesForCloneAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT
				ct.id,
				ct.template_name,
				ct.equipment_type_id,
				et.type_name,
				ct.maintenance_type_id,
				mt.type_name,
				ct.facility_id,
				f.name,
				COALESCE(NULLIF(TRIM(o.short_name), ''), o.full_name),
				(
					SELECT COUNT(1)
					FROM checklist_template_items cti
					WHERE cti.checklist_template_id = ct.id
				) AS field_count
			FROM checklist_templates ct
			INNER JOIN equipment_types et ON et.id = ct.equipment_type_id
			INNER JOIN maintenance_types mt ON mt.id = ct.maintenance_type_id
			LEFT JOIN facilities f ON f.id = ct.facility_id
			LEFT JOIN organizations o ON o.id = f.organization_id
			WHERE ct.is_active = 1
			ORDER BY
				CASE WHEN ct.facility_id IS NULL THEN 1 ELSE 0 END,
				COALESCE(o.full_name, ''),
				COALESCE(f.name, ''),
				et.type_name, mt.type_name, ct.template_name, ct.version DESC;
			""";

		var list = new List<TemplateCloneSourceOption>();
		var seenPair = new HashSet<string>(StringComparer.Ordinal);
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
		{
			var etId = reader.GetInt32(2);
			var mtId = reader.GetInt32(4);
			int? facilityId = reader.IsDBNull(6) ? null : reader.GetInt32(6);
			// Одна актуальная версия на пару оборудование+ТО+объект (или общий).
			var key = $"{etId}:{mtId}:{facilityId?.ToString() ?? "_"}";
			if (!seenPair.Add(key))
				continue;

			list.Add(new TemplateCloneSourceOption(
				reader.GetInt32(0),
				reader.GetString(1),
				etId,
				reader.GetString(3),
				mtId,
				reader.GetString(5),
				facilityId,
				reader.IsDBNull(7) ? null : reader.GetString(7),
				reader.IsDBNull(8) ? null : reader.GetString(8),
				reader.GetInt32(9)));
		}

		return list;
	}

	public async Task<TemplateCloneDraft> GetTemplateCloneDraftAsync(int templateId, CancellationToken cancellationToken = default)
	{
		if (templateId <= 0)
			throw new InvalidOperationException("Выберите шаблон-образец.");

		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
		using var metaCmd = connection.CreateCommand();
		metaCmd.CommandText = """
			SELECT ct.id, ct.template_name, ct.equipment_type_id, ct.maintenance_type_id,
			       ct.top_plate_text, ct.safety_modal_text, ct.red_button_enabled,
			       ct.facility_id, f.organization_id
			FROM checklist_templates ct
			LEFT JOIN facilities f ON f.id = ct.facility_id
			WHERE ct.id = $id AND ct.is_active = 1;
			""";
		metaCmd.Parameters.AddWithValue("$id", templateId);

		string templateName;
		int equipmentTypeId;
		int maintenanceTypeId;
		string? topPlate;
		string? safety;
		bool redButton;
		int? facilityId;
		int? organizationId;
		await using (var reader = await metaCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
		{
			if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				throw new InvalidOperationException("Шаблон-образец не найден.");

			templateName = reader.GetString(1);
			equipmentTypeId = reader.GetInt32(2);
			maintenanceTypeId = reader.GetInt32(3);
			topPlate = reader.IsDBNull(4) ? null : reader.GetString(4);
			safety = reader.IsDBNull(5) ? null : reader.GetString(5);
			redButton = reader.GetInt32(6) != 0;
			facilityId = reader.IsDBNull(7) ? null : reader.GetInt32(7);
			organizationId = reader.IsDBNull(8) ? null : reader.GetInt32(8);
		}

		using var fieldsCmd = connection.CreateCommand();
		fieldsCmd.CommandText = """
			SELECT cti.id, cti.sort_order, cti.field_code, cti.question_text, cti.hint_text,
			       ft.type_name, cti.is_required
			FROM checklist_template_items cti
			INNER JOIN field_types ft ON ft.id = cti.field_type_id
			WHERE cti.checklist_template_id = $tid
			ORDER BY cti.sort_order;
			""";
		fieldsCmd.Parameters.AddWithValue("$tid", templateId);

		var rawFields = new List<(int ItemId, int SortOrder, string? FieldCode, string Question, string? Hint, string TypeName, bool Required)>();
		await using (var reader = await fieldsCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
		{
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				var fieldCode = reader.IsDBNull(2) ? null : reader.GetString(2);
				if (ChecklistFieldCodes.IsEndTime(fieldCode) || ChecklistFieldCodes.IsUnitNumber(fieldCode))
					continue;

				rawFields.Add((
					reader.GetInt32(0),
					reader.GetInt32(1),
					fieldCode,
					reader.GetString(3),
					reader.IsDBNull(4) ? null : reader.GetString(4),
					NormalizeAuthoringFieldType(reader.GetString(5)),
					reader.GetInt32(6) != 0));
			}
		}

		var fields = new List<TemplateCloneFieldDraft>(rawFields.Count);
		foreach (var row in rawFields)
		{
			var options = await LoadOptionLabelsAsync(connection, row.ItemId, cancellationToken).ConfigureAwait(false);
			fields.Add(new TemplateCloneFieldDraft(
				row.SortOrder,
				row.FieldCode,
				row.Question,
				row.Hint,
				row.TypeName,
				row.Required,
				options));
		}

		if (fields.Count == 0)
			throw new InvalidOperationException("В шаблоне-образце нет полей для копирования.");

		return new TemplateCloneDraft(
			templateId,
			templateName,
			equipmentTypeId,
			maintenanceTypeId,
			facilityId,
			organizationId,
			topPlate,
			safety,
			redButton,
			fields);
	}

	private static string NormalizeAuthoringFieldType(string typeName)
	{
		var t = (typeName ?? string.Empty).Trim().ToLowerInvariant();
		return t switch
		{
			"radio" => "dropdown",
			"checkbox" => "dropdown_multiple",
			_ => t
		};
	}

	private static async Task<IReadOnlyList<string>> LoadOptionLabelsAsync(
		SqliteConnection connection,
		int templateItemId,
		CancellationToken cancellationToken)
	{
		using var cmd = connection.CreateCommand();
		cmd.CommandText = """
			SELECT option_label
			FROM checklist_template_item_options
			WHERE checklist_template_item_id = $iid
			ORDER BY sort_order;
			""";
		cmd.Parameters.AddWithValue("$iid", templateItemId);
		var list = new List<string>();
		await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			list.Add(reader.GetString(0));
		return list;
	}

	public async Task<int> CreateTemplateAsync(CreateChecklistTemplateRequest request, CancellationToken cancellationToken = default)
    {
        if (request.FacilityId <= 0)
            throw new InvalidOperationException("Выберите объект (площадку) — шаблон привязывается к объекту.");
        if (request.EquipmentTypeId <= 0)
            throw new InvalidOperationException("Выберите тип оборудования.");
        var templateName = (request.TemplateName ?? string.Empty).Trim();
        if (templateName.Length == 0)
            throw new InvalidOperationException("Укажите название шаблона.");
        if (request.Fields is null || request.Fields.Count == 0)
            throw new InvalidOperationException("Добавьте хотя бы одно поле в шаблон.");

        await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);
        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await EnsureFacilityExistsAsync(connection, tx, request.FacilityId, cancellationToken).ConfigureAwait(false);
            var maintenanceTypeId = await EnsureMaintenanceTypeAsync(connection, tx, request, cancellationToken).ConfigureAwait(false);
            var version = await ResolveNextTemplateVersionAsync(
                connection, tx, request.FacilityId, request.EquipmentTypeId, maintenanceTypeId, cancellationToken).ConfigureAwait(false);
            var templateId = await InsertTemplateAsync(connection, tx, request, maintenanceTypeId, version, templateName, cancellationToken).ConfigureAwait(false);
            await InsertFieldsAsync(connection, tx, templateId, request.Fields, cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);

            var json = await SqliteTemplateSyncPayloadBuilder.BuildAsync(_paths, _bootstrapper, templateId, cancellationToken)
                .ConfigureAwait(false);
            var syncPayload = System.Text.Json.JsonSerializer.Deserialize<TemplateSyncPayload>(json);
            var uuid = syncPayload?.ClientUuid ?? Guid.NewGuid().ToString();
            await _outbox.EnqueueAsync(new SyncOutboxEnqueueRequest("checklist_template", uuid, "insert", json), cancellationToken)
                .ConfigureAwait(false);

            return templateId;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task EnsureFacilityExistsAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        int facilityId,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT 1 FROM facilities WHERE id = $id AND is_active = 1 LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", facilityId);
        if (await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            throw new InvalidOperationException("Объект не найден или неактивен.");
    }

    private static async Task<int> EnsureMaintenanceTypeAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        CreateChecklistTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ExistingMaintenanceTypeId is int existingId && existingId > 0)
            return existingId;

        var name = (request.NewMaintenanceTypeName ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("Выберите вид ТО или задайте новый.");
        var code = (request.NewMaintenanceTypeCode ?? string.Empty).Trim();
        if (code.Length == 0)
            code = TemplateFieldCodeGenerator.SuggestMaintenanceTypeCode(name);
        code = await EnsureUniqueMaintenanceCodeAsync(connection, tx, code, cancellationToken).ConfigureAwait(false);
        var description = (request.NewMaintenanceTypeDescription ?? string.Empty).Trim();

        using (var find = connection.CreateCommand())
        {
            find.Transaction = tx;
            find.CommandText = """
                SELECT id
                FROM maintenance_types
                WHERE TRIM(type_name) = $name
                   OR (NULLIF($code, '') IS NOT NULL AND TRIM(COALESCE(code, '')) = $code)
                LIMIT 1;
                """;
            find.Parameters.AddWithValue("$name", name);
            find.Parameters.AddWithValue("$code", code);
            var existing = await find.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null)
                return Convert.ToInt32(existing);
        }

        using var insert = connection.CreateCommand();
        insert.Transaction = tx;
        insert.CommandText = """
            INSERT INTO maintenance_types (type_name, code, description)
            VALUES ($name, $code, $description);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$name", name);
        insert.Parameters.AddWithValue("$code", code);
        insert.Parameters.AddWithValue("$description", description.Length == 0 ? DBNull.Value : description);
        var scalar = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is long l ? (int)l : Convert.ToInt32(scalar);
    }

    private static async Task<int> ResolveNextTemplateVersionAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        int facilityId,
        int equipmentTypeId,
        int maintenanceTypeId,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT COALESCE(MAX(version), 0)
            FROM checklist_templates
            WHERE equipment_type_id = $et
              AND maintenance_type_id = $mt
              AND facility_id = $fid;
            """;
        cmd.Parameters.AddWithValue("$et", equipmentTypeId);
        cmd.Parameters.AddWithValue("$mt", maintenanceTypeId);
        cmd.Parameters.AddWithValue("$fid", facilityId);
        var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var current = Convert.ToInt32(scalar ?? 0);
        return current + 1;
    }

    private static async Task<int> InsertTemplateAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        CreateChecklistTemplateRequest request,
        int maintenanceTypeId,
        int version,
        string templateName,
        CancellationToken cancellationToken)
    {
        var scenarioCode = await ResolveScenarioCodeAsync(connection, tx, request, cancellationToken).ConfigureAwait(false);
        var top = (request.TopPlateText ?? string.Empty).Trim();
        var intro = (request.IntroModalText ?? string.Empty).Trim();
        var safety = (request.SafetyModalText ?? string.Empty).Trim();

        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO checklist_templates (
                facility_id,
                equipment_type_id,
                maintenance_type_id,
                template_name,
                scenario_code,
                version,
                is_active,
                top_plate_text,
                intro_modal_text,
                safety_modal_text,
                red_button_enabled
            )
            VALUES ($fid, $et, $mt, $name, $scenario, $version, 1, $top, $intro, $safety, $red);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$fid", request.FacilityId);
        cmd.Parameters.AddWithValue("$et", request.EquipmentTypeId);
        cmd.Parameters.AddWithValue("$mt", maintenanceTypeId);
        cmd.Parameters.AddWithValue("$name", templateName);
        cmd.Parameters.AddWithValue("$scenario", scenarioCode.Length == 0 ? DBNull.Value : scenarioCode);
        cmd.Parameters.AddWithValue("$version", version);
        cmd.Parameters.AddWithValue("$top", top.Length == 0 ? DBNull.Value : top);
        cmd.Parameters.AddWithValue("$intro", intro.Length == 0 ? DBNull.Value : intro);
        cmd.Parameters.AddWithValue("$safety", safety.Length == 0 ? DBNull.Value : safety);
        cmd.Parameters.AddWithValue("$red", request.RedButtonEnabled ? 1 : 0);
        var scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is long l ? (int)l : Convert.ToInt32(scalar);
    }

    private static async Task InsertFieldsAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        int templateId,
        IReadOnlyList<CreateTemplateFieldRequest> fields,
        CancellationToken cancellationToken)
    {
        var typeMap = await LoadFieldTypeMapAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        var usedFieldCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var f in fields.OrderBy(x => x.SortOrder))
        {
            var question = (f.QuestionText ?? string.Empty).Trim();
            if (question.Length == 0)
                throw new InvalidOperationException($"Поле #{f.SortOrder}: заполните текст вопроса.");
            var fieldTypeName = (f.FieldTypeName ?? string.Empty).Trim().ToLowerInvariant();
            if (!typeMap.TryGetValue(fieldTypeName, out var fieldTypeId))
                throw new InvalidOperationException($"Поле #{f.SortOrder}: неизвестный тип '{f.FieldTypeName}'.");

            var fieldCode = (f.FieldCode ?? string.Empty).Trim();
            if (fieldCode.Length == 0)
                fieldCode = TemplateFieldCodeGenerator.SuggestFromQuestion(question, usedFieldCodes);
            usedFieldCodes.Add(fieldCode);
            var hint = (f.HintText ?? string.Empty).Trim();
            var group = (f.GroupName ?? string.Empty).Trim();
            var validation = (f.ValidationRuleCode ?? string.Empty).Trim();

            int itemId;
            using (var insertItem = connection.CreateCommand())
            {
                insertItem.Transaction = tx;
                insertItem.CommandText = """
                    INSERT INTO checklist_template_items (
                        checklist_template_id,
                        sort_order,
                        field_code,
                        question_text,
                        hint_text,
                        field_type_id,
                        validation_rule_code,
                        is_required,
                        group_name
                    )
                    VALUES ($tid, $sort, $code, $question, $hint, $ftid, $validation, $required, $group);
                    SELECT last_insert_rowid();
                    """;
                insertItem.Parameters.AddWithValue("$tid", templateId);
                insertItem.Parameters.AddWithValue("$sort", f.SortOrder);
                insertItem.Parameters.AddWithValue("$code", fieldCode.Length == 0 ? DBNull.Value : fieldCode);
                insertItem.Parameters.AddWithValue("$question", question);
                insertItem.Parameters.AddWithValue("$hint", hint.Length == 0 ? DBNull.Value : hint);
                insertItem.Parameters.AddWithValue("$ftid", fieldTypeId);
                insertItem.Parameters.AddWithValue("$validation", validation.Length == 0 ? DBNull.Value : validation);
                insertItem.Parameters.AddWithValue("$required", f.IsRequired ? 1 : 0);
                insertItem.Parameters.AddWithValue("$group", group.Length == 0 ? DBNull.Value : group);
                var scalar = await insertItem.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                itemId = scalar is long l ? (int)l : Convert.ToInt32(scalar);
            }

            var options = (f.Options ?? []).Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => o.Trim()).ToList();
            if (options.Count == 0)
                continue;

            var sortOrder = 1;
            foreach (var option in options)
            {
                using var insertOption = connection.CreateCommand();
                insertOption.Transaction = tx;
                insertOption.CommandText = """
                    INSERT INTO checklist_template_item_options (
                        checklist_template_item_id,
                        sort_order,
                        option_label
                    )
                    VALUES ($iid, $sort, $label);
                    """;
                insertOption.Parameters.AddWithValue("$iid", itemId);
                insertOption.Parameters.AddWithValue("$sort", sortOrder++);
                insertOption.Parameters.AddWithValue("$label", option);
                await insertOption.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<Dictionary<string, int>> LoadFieldTypeMapAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT id, type_name
            FROM field_types;
            """;
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            map[reader.GetString(1)] = reader.GetInt32(0);
        return map;
    }

    private static async Task<string> EnsureUniqueMaintenanceCodeAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        string baseCode,
        CancellationToken cancellationToken)
    {
        var code = baseCode;
        var n = 2;
        while (await MaintenanceCodeExistsAsync(connection, tx, code, cancellationToken).ConfigureAwait(false))
        {
            code = $"{baseCode}-{n}";
            n++;
        }

        return code;
    }

    private static async Task<bool> MaintenanceCodeExistsAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        string code,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT COUNT(1)
            FROM maintenance_types
            WHERE TRIM(COALESCE(code, '')) = $code;
            """;
        cmd.Parameters.AddWithValue("$code", code);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
    }

    private static async Task<string> ResolveScenarioCodeAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        CreateChecklistTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var scenario = (request.ScenarioCode ?? string.Empty).Trim();
        if (scenario.Length > 0)
            return scenario;

        var baseCode = TemplateFieldCodeGenerator.SuggestScenarioCode(request.TemplateName, request.EquipmentTypeId);
        var code = baseCode;
        var n = 2;
        while (await ScenarioCodeExistsAsync(connection, tx, code, cancellationToken).ConfigureAwait(false))
        {
            code = $"{baseCode}-{n}";
            n++;
        }

        return code;
    }

    private static async Task<bool> ScenarioCodeExistsAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        string code,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT COUNT(1)
            FROM checklist_templates
            WHERE TRIM(COALESCE(scenario_code, '')) = $code;
            """;
        cmd.Parameters.AddWithValue("$code", code);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
    }
}
