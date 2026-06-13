using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Facilities;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteChecklistTemplateAuthoringService : IChecklistTemplateAuthoringService
{
    private readonly ILocalDatabasePath _paths;
    private readonly ILocalDatabaseBootstrapper _bootstrapper;

    public SqliteChecklistTemplateAuthoringService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
    {
        _paths = paths;
        _bootstrapper = bootstrapper;
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

    public async Task<int> CreateTemplateAsync(CreateChecklistTemplateRequest request, CancellationToken cancellationToken = default)
    {
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
            var maintenanceTypeId = await EnsureMaintenanceTypeAsync(connection, tx, request, cancellationToken).ConfigureAwait(false);
            var version = await ResolveNextTemplateVersionAsync(connection, tx, request.EquipmentTypeId, maintenanceTypeId, cancellationToken).ConfigureAwait(false);
            var templateId = await InsertTemplateAsync(connection, tx, request, maintenanceTypeId, version, templateName, cancellationToken).ConfigureAwait(false);
            await InsertFieldsAsync(connection, tx, templateId, request.Fields, cancellationToken).ConfigureAwait(false);
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return templateId;
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
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
        insert.Parameters.AddWithValue("$code", code.Length == 0 ? DBNull.Value : code);
        insert.Parameters.AddWithValue("$description", description.Length == 0 ? DBNull.Value : description);
        var scalar = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return scalar is long l ? (int)l : Convert.ToInt32(scalar);
    }

    private static async Task<int> ResolveNextTemplateVersionAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        int equipmentTypeId,
        int maintenanceTypeId,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT COALESCE(MAX(version), 0)
            FROM checklist_templates
            WHERE equipment_type_id = $et AND maintenance_type_id = $mt;
            """;
        cmd.Parameters.AddWithValue("$et", equipmentTypeId);
        cmd.Parameters.AddWithValue("$mt", maintenanceTypeId);
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
        var scenarioCode = (request.ScenarioCode ?? string.Empty).Trim();
        var top = (request.TopPlateText ?? string.Empty).Trim();
        var intro = (request.IntroModalText ?? string.Empty).Trim();
        var safety = (request.SafetyModalText ?? string.Empty).Trim();

        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO checklist_templates (
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
            VALUES ($et, $mt, $name, $scenario, $version, 1, $top, $intro, $safety, $red);
            SELECT last_insert_rowid();
            """;
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

        foreach (var f in fields.OrderBy(x => x.SortOrder))
        {
            var question = (f.QuestionText ?? string.Empty).Trim();
            if (question.Length == 0)
                throw new InvalidOperationException($"Поле #{f.SortOrder}: заполните текст вопроса.");
            var fieldTypeName = (f.FieldTypeName ?? string.Empty).Trim().ToLowerInvariant();
            if (!typeMap.TryGetValue(fieldTypeName, out var fieldTypeId))
                throw new InvalidOperationException($"Поле #{f.SortOrder}: неизвестный тип '{f.FieldTypeName}'.");

            var fieldCode = (f.FieldCode ?? string.Empty).Trim();
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
}
