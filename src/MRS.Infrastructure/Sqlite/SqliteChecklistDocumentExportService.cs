using System.Globalization;
using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteChecklistDocumentExportService : IChecklistDocumentExportService
{
    private readonly ILocalDatabasePath _paths;
    private readonly ILocalDatabaseBootstrapper _bootstrapper;

    public SqliteChecklistDocumentExportService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
    {
        _paths = paths;
        _bootstrapper = bootstrapper;
    }

    public async Task<ChecklistDocumentExportModel> GetDocumentModelAsync(int checklistId, CancellationToken cancellationToken = default)
    {
        await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
            .ConfigureAwait(false);

        var header = await LoadHeaderAsync(connection, checklistId, cancellationToken).ConfigureAwait(false);
        var templateId = await ResolveTemplateIdAsync(connection, checklistId, cancellationToken).ConfigureAwait(false);
        var answers = await LoadAnswersAsync(connection, checklistId, templateId, header, cancellationToken).ConfigureAwait(false);
        return new ChecklistDocumentExportModel(header, answers);
    }

    public async Task<ChecklistDocumentExportFile> ExportDocAsync(int checklistId, CancellationToken cancellationToken = default)
    {
        var model = await GetDocumentModelAsync(checklistId, cancellationToken).ConfigureAwait(false);
        var html = BuildWordHtml(model);

        var stamp = (model.Header.StartedAt ?? DateTimeOffset.Now).ToLocalTime().ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
        var fileName = $"checklist_{model.Header.ChecklistId}_{SanitizeToken(model.Header.InstallationLabel)}_{stamp}.doc";
        var payload = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(html)).ToArray();
        return new ChecklistDocumentExportFile(fileName, "application/msword", payload);
    }

    public async Task<ChecklistDocumentExportFile> ExportZipAsync(
        IReadOnlyCollection<int> checklistIds,
        CancellationToken cancellationToken = default)
    {
        var ids = checklistIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("Не выбраны контрольные листы для выгрузки.");

        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var checklistId in ids)
            {
                var doc = await ExportDocAsync(checklistId, cancellationToken).ConfigureAwait(false);
                var entry = archive.CreateEntry(doc.FileName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(doc.Content, cancellationToken).ConfigureAwait(false);
            }
        }

        var zipName = $"checklists_export_{DateTime.Now:yyyyMMdd_HHmm}.zip";
        return new ChecklistDocumentExportFile(zipName, "application/zip", stream.ToArray());
    }

    private static string BuildWordHtml(ChecklistDocumentExportModel model)
    {
        var startAt = model.Header.StartedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) ?? "—";
        var endAt = model.Header.EndedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) ?? "—";

        var sb = new StringBuilder();
        sb.AppendLine("<html><head><meta charset=\"utf-8\" />");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Calibri,Arial,sans-serif;font-size:11pt;}");
        sb.AppendLine("h1,h2{margin:0 0 8px 0;}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-top:8px;}");
        sb.AppendLine("th,td{border:1px solid #222;padding:4px;vertical-align:top;}");
        sb.AppendLine(".meta td:first-child{width:260px;font-weight:700;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>Контрольный лист №{model.Header.ChecklistId}</h1>");
        sb.AppendLine("<table class=\"meta\">");
        AppendMetaRow(sb, "Организация", model.Header.OrganizationName);
        AppendMetaRow(sb, "Объект", model.Header.FacilityName);
        AppendMetaRow(sb, "Тип оборудования", model.Header.EquipmentTypeName);
        AppendMetaRow(sb, "Установка", model.Header.InstallationLabel);
        AppendMetaRow(sb, "Вид ТО", model.Header.MaintenanceTypeName);
        AppendMetaRow(sb, "Статус", model.Header.StatusCode);
        AppendMetaRow(sb, "Дата начала", startAt);
        AppendMetaRow(sb, "Дата окончания", endAt);
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Ответы по контрольному листу</h2>");
        sb.AppendLine("<table>");
        sb.AppendLine("<tr><th>№</th><th>Код поля</th><th>Вопрос</th><th>Тип</th><th>Значение</th></tr>");
        foreach (var row in model.Answers)
        {
            var value = string.IsNullOrWhiteSpace(row.ValueDisplay) ? row.ValueRaw : row.ValueDisplay;
            sb.Append("<tr>")
                .Append("<td>").Append(row.SortOrder).Append("</td>")
                .Append("<td>").Append(Html(row.FieldCode)).Append("</td>")
                .Append("<td>").Append(Html(row.QuestionText)).Append("</td>")
                .Append("<td>").Append(Html(row.FieldTypeName)).Append("</td>")
                .Append("<td>").Append(Html(value)).Append("</td>")
                .AppendLine("</tr>");
        }

        sb.AppendLine("</table>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void AppendMetaRow(StringBuilder sb, string key, string value)
    {
        sb.Append("<tr><td>")
            .Append(Html(key))
            .Append("</td><td>")
            .Append(Html(value))
            .AppendLine("</td></tr>");
    }

    private static string Html(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "&nbsp;";

        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("\r\n", "<br/>", StringComparison.Ordinal)
            .Replace("\n", "<br/>", StringComparison.Ordinal);
    }

    private static string SanitizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "unit";

        var cleaned = token.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            cleaned = cleaned.Replace(c, '_');
        return cleaned.Replace(' ', '_');
    }

    private static async Task<ChecklistDocumentHeader> LoadHeaderAsync(
        SqliteConnection connection,
        int checklistId,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                c.id,
                c.start_at,
                c.end_at,
                c.status,
                COALESCE(o.short_name, o.full_name) AS organization_name,
                f.name AS facility_name,
                et.type_name AS equipment_type_name,
                COALESCE(NULLIF(TRIM(i.custom_name), ''), CAST(i.id AS TEXT)) AS installation_label,
                mt.type_name AS maintenance_type_name
            FROM checklists c
            INNER JOIN installations i ON i.id = c.installation_id
            INNER JOIN equipment_types et ON et.id = i.equipment_type_id
            INNER JOIN facility_systems fs ON fs.id = i.system_id
            INNER JOIN facilities f ON f.id = fs.facility_id
            INNER JOIN organizations o ON o.id = f.organization_id
            INNER JOIN maintenance_types mt ON mt.id = c.maintenance_type_id
            WHERE c.id = $cid AND c.is_active = 1;
            """;
        cmd.Parameters.AddWithValue("$cid", checklistId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Контрольный лист не найден.");

        DateTimeOffset? startedAt = null;
        DateTimeOffset? endedAt = null;
        if (!reader.IsDBNull(1) && DateTimeOffset.TryParse(reader.GetString(1), out var s))
            startedAt = s;
        if (!reader.IsDBNull(2) && DateTimeOffset.TryParse(reader.GetString(2), out var e))
            endedAt = e;

        return new ChecklistDocumentHeader(
            reader.GetInt32(0),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.GetString(8),
            startedAt,
            endedAt,
            reader.GetString(3));
    }

    private static async Task<int> ResolveTemplateIdAsync(
        SqliteConnection connection,
        int checklistId,
        CancellationToken cancellationToken)
    {
        using var direct = connection.CreateCommand();
        direct.CommandText = """
            SELECT c.checklist_template_id
            FROM checklists c
            WHERE c.id = $cid;
            """;
        direct.Parameters.AddWithValue("$cid", checklistId);
        var scalar = await direct.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (scalar is long l)
            return (int)l;
        if (scalar is int i)
            return i;

        using var resolve = connection.CreateCommand();
        resolve.CommandText = """
            SELECT ct.id
            FROM checklists c
            INNER JOIN installations i ON i.id = c.installation_id
            INNER JOIN checklist_templates ct
                ON ct.equipment_type_id = i.equipment_type_id
               AND ct.maintenance_type_id = c.maintenance_type_id
               AND ct.is_active = 1
            WHERE c.id = $cid
            ORDER BY ct.version DESC
            LIMIT 1;
            """;
        resolve.Parameters.AddWithValue("$cid", checklistId);
        var resolved = await resolve.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (resolved is long rl)
            return (int)rl;
        if (resolved is int ri)
            return ri;

        throw new InvalidOperationException("Не удалось определить шаблон для выгрузки контрольного листа.");
    }

    private static async Task<IReadOnlyList<ChecklistDocumentAnswer>> LoadAnswersAsync(
        SqliteConnection connection,
        int checklistId,
        int templateId,
        ChecklistDocumentHeader header,
        CancellationToken cancellationToken)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                cti.id,
                cti.sort_order,
                cti.field_code,
                cti.question_text,
                ft.type_name,
                cr.boolean_response,
                cr.text_response,
                cr.numeric_response,
                cr.selected_option_id,
                sel.option_label AS selected_option_label,
                (
                    SELECT GROUP_CONCAT(mo.checklist_template_item_option_id, ',')
                    FROM checklist_response_multi_options mo
                    WHERE mo.checklist_response_id = cr.id
                ) AS multi_option_ids,
                (
                    SELECT GROUP_CONCAT(opt.option_label, ' | ')
                    FROM checklist_response_multi_options mo
                    INNER JOIN checklist_template_item_options opt ON opt.id = mo.checklist_template_item_option_id
                    WHERE mo.checklist_response_id = cr.id
                ) AS multi_option_labels
            FROM checklist_template_items cti
            INNER JOIN field_types ft ON ft.id = cti.field_type_id
            LEFT JOIN checklist_responses cr
                ON cr.checklist_template_item_id = cti.id
               AND cr.checklist_id = $cid
            LEFT JOIN checklist_template_item_options sel ON sel.id = cr.selected_option_id
            WHERE cti.checklist_template_id = $tid
            ORDER BY cti.sort_order;
            """;
        cmd.Parameters.AddWithValue("$cid", checklistId);
        cmd.Parameters.AddWithValue("$tid", templateId);

        var rows = new List<ChecklistDocumentAnswer>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var templateItemId = reader.GetInt32(0);
            var sortOrder = reader.GetInt32(1);
            var fieldCode = reader.IsDBNull(2) ? null : reader.GetString(2);
            var questionText = reader.GetString(3);
            var fieldType = reader.GetString(4);
            int? boolRaw = reader.IsDBNull(5) ? null : reader.GetInt32(5);
            var textRaw = reader.IsDBNull(6) ? null : reader.GetString(6);
            double? numRaw = reader.IsDBNull(7) ? null : reader.GetDouble(7);
            int? selectedOptionId = reader.IsDBNull(8) ? null : reader.GetInt32(8);
            var selectedOptionLabel = reader.IsDBNull(9) ? null : reader.GetString(9);
            var multiOptionIds = reader.IsDBNull(10) ? null : reader.GetString(10);
            var multiOptionLabels = reader.IsDBNull(11) ? null : reader.GetString(11);

            var (valueRaw, valueDisplay) = BuildValuePair(
                fieldCode,
                fieldType,
                boolRaw,
                textRaw,
                numRaw,
                selectedOptionId,
                selectedOptionLabel,
                multiOptionIds,
                multiOptionLabels,
                header);

            rows.Add(new ChecklistDocumentAnswer(
                templateItemId,
                sortOrder,
                fieldCode,
                questionText,
                fieldType,
                valueRaw,
                valueDisplay));
        }

        return rows;
    }

    private static (string Raw, string Display) BuildValuePair(
        string? fieldCode,
        string fieldType,
        int? boolRaw,
        string? textRaw,
        double? numRaw,
        int? selectedOptionId,
        string? selectedOptionLabel,
        string? multiOptionIds,
        string? multiOptionLabels,
        ChecklistDocumentHeader header)
    {
        if (fieldCode is not null)
        {
            if (fieldCode.Equals("start_date", StringComparison.OrdinalIgnoreCase))
            {
                var raw = header.StartedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
                var display = header.StartedAt?.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
                return (raw, display);
            }

            if (fieldCode.Equals("end_date", StringComparison.OrdinalIgnoreCase))
            {
                var raw = header.EndedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
                var display = header.EndedAt?.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
                return (raw, display);
            }

            if (fieldCode.Equals("start_time", StringComparison.OrdinalIgnoreCase))
            {
                var raw = header.StartedAt?.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
                return (raw, raw);
            }

            if (fieldCode.Equals("end_time", StringComparison.OrdinalIgnoreCase))
            {
                var raw = header.EndedAt?.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
                return (raw, raw);
            }
        }

        if (string.Equals(fieldType, "boolean", StringComparison.OrdinalIgnoreCase))
        {
            if (!boolRaw.HasValue)
                return (string.Empty, string.Empty);
            return boolRaw.Value == 1 ? ("true", "Да") : ("false", "Нет");
        }

        if (string.Equals(fieldType, "number", StringComparison.OrdinalIgnoreCase))
        {
            var raw = numRaw?.ToString("G", CultureInfo.InvariantCulture) ?? string.Empty;
            return (raw, raw);
        }

        if (string.Equals(fieldType, "radio", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldType, "dropdown", StringComparison.OrdinalIgnoreCase))
        {
            if (selectedOptionId.HasValue)
            {
                var raw = selectedOptionId.Value.ToString(CultureInfo.InvariantCulture);
                var display = string.IsNullOrWhiteSpace(selectedOptionLabel) ? raw : selectedOptionLabel;
                return (raw, display);
            }

            var fallback = textRaw ?? string.Empty;
            return (fallback, fallback);
        }

        if (string.Equals(fieldType, "dropdown_multiple", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fieldType, "checkbox", StringComparison.OrdinalIgnoreCase))
        {
            var raw = multiOptionIds ?? string.Empty;
            var display = multiOptionLabels ?? string.Empty;
            return (raw, display);
        }

        var text = textRaw ?? string.Empty;
        return (text, text);
    }
}
