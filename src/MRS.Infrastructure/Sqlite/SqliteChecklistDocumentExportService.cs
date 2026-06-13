using System.Globalization;
using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

/// <summary>
/// Главная реализация экспорта:
/// 1) читает данные листа и шаблона из SQLite;
/// 2) нормализует ответы в ChecklistDocumentExportModel;
/// 3) рендерит Word-openable HTML и сохраняет как .doc;
/// 4) при необходимости упаковывает несколько .doc в ZIP.

/// </summary>
public sealed class SqliteChecklistDocumentExportService : IChecklistDocumentExportService
{
    private const string BrandRed = "#E31E24";
    private const string LogoResourceName = "MRS.Infrastructure.Resources.brand-schutz-logo.png";

    // Профиль документа выбирается по типу оборудования.
    // От него зависят подписи и логика отметок во втором блоке.
    private enum ActExportProfile
    {
        Compressor,
        Dryer,
        Installation
    }

    private sealed record EquipmentStateFlags(bool Working, bool UnderLoad, bool Off, bool NotWorking);

    private static readonly Lock LogoLock = new();
    private static string? _logoDataUri;

    private readonly ILocalDatabasePath _paths;
    private readonly ILocalDatabaseBootstrapper _bootstrapper;

    public SqliteChecklistDocumentExportService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
    {
        _paths = paths;
        _bootstrapper = bootstrapper;
    }

    public async Task<ChecklistDocumentExportModel> GetDocumentModelAsync(int checklistId, CancellationToken cancellationToken = default)
    {
        // Открываем локальную SQLite БД (с bootstrap-проверкой схемы).
        await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken)
            .ConfigureAwait(false);

        // 1) шапка листа (организация/оборудование/даты)
        var header = await LoadHeaderAsync(connection, checklistId, cancellationToken).ConfigureAwait(false);
        // 2) шаблон (либо прямой checklist_template_id, либо fallback-резолв)
        var templateId = await ResolveTemplateIdAsync(connection, checklistId, cancellationToken).ConfigureAwait(false);
        // 3) ответы всех template items с нормализацией типов
        var answers = await LoadAnswersAsync(connection, checklistId, templateId, header, cancellationToken).ConfigureAwait(false);
        return new ChecklistDocumentExportModel(header, answers);
    }

    public async Task<ChecklistDocumentExportFile> ExportDocAsync(int checklistId, CancellationToken cancellationToken = default)
    {
        // Сначала строим модель, затем рендерим HTML.
        var model = await GetDocumentModelAsync(checklistId, cancellationToken).ConfigureAwait(false);
        var html = BuildWordHtml(model);

        // Имя файла стараемся делать человекочитаемым и уникальным.
        var stamp = (model.Header.StartedAt ?? DateTimeOffset.Now).ToLocalTime().ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture);
        var fileName = $"checklist_{model.Header.ChecklistId}_{SanitizeToken(model.Header.InstallationLabel)}_{stamp}.doc";
        // BOM + UTF-8 помогают Word корректно открыть русский текст.
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
                // На каждый checklistId создается отдельный .doc внутри архива.
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
        
        var answers = model.Answers;
        var customer = model.Header.OrganizationName;
        var equipmentType = model.Header.EquipmentTypeName;
        var serial = GetAnswerDisplay(answers, "comp_serial", "serial_number", "compressor_serial", "serial") ?? "___________";
        var workDates = FormatWorkDateRange(model.Header);
        var modelName = GetAnswerDisplay(answers, "comp_model", "model") ?? "___________";
        var unitNumber = GetAnswerDisplay(answers, "unit_number", "unit_no") ?? model.Header.InstallationLabel;
        if (string.IsNullOrWhiteSpace(unitNumber))
            unitNumber = "___________";
        var workKind = model.Header.MaintenanceTypeName;
        var hours = GetAnswerDisplay(answers, "operating_hours", "hours", "runtime_hours") ?? "___________";

        var logoUri = GetLogoDataUri();
        var profile = DetectProfile(equipmentType);
        var compStateDisplay = GetAnswerDisplay(answers, "comp_state", "equipment_state", "state");
        var stateFlags = ParseEquipmentState(compStateDisplay, profile);

        var sb = new StringBuilder();
        sb.AppendLine("<html xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:w=\"urn:schemas-microsoft-com:office:word\"><head><meta charset=\"utf-8\" />");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Calibri,Arial,sans-serif;font-size:11pt;margin:12pt;}");
        sb.AppendLine(".header-top{width:100%;border-collapse:collapse;mso-table-lspace:0;mso-table-rspace:0;}");
        sb.AppendLine(".header-top td{border:none !important;padding:0;vertical-align:top;}");
        sb.AppendLine(".logo-cell{width:3.8cm;}");
        sb.AppendLine(".logo-cell img{width:3.8cm;height:3.8cm;display:block;object-fit:contain;}");
        sb.AppendLine(".gap-cell{width:7cm;}");
        sb.AppendLine(".contact-cell{padding:0;}");
        sb.AppendLine(".contact-wrap{width:100%;border-collapse:collapse;mso-table-lspace:0;mso-table-rspace:0;}");
        sb.AppendLine(".contact-wrap td{border:none !important;padding:0;vertical-align:top;}");
        sb.AppendLine(".contact-vbar{width:1.5pt;background-color:").Append(BrandRed).Append(";font-size:1pt;line-height:1pt;mso-line-height-rule:exactly;}");
        sb.AppendLine(".contact-text{padding-left:0.5cm;padding-top:0;padding-bottom:0;vertical-align:top;}");
        sb.AppendLine(".contact-line{color:").Append(BrandRed).Append(";font-size:10pt;line-height:1.2;margin:0;padding:0;}");
        sb.AppendLine(".doc-title{text-align:center;font-weight:bold;font-size:14pt;margin:14pt 0 10pt 0;}");
        sb.AppendLine(".doc-title-sub{font-size:13pt;margin-top:4pt;}");
        sb.AppendLine(".meta-grid{width:100%;border-collapse:collapse;mso-table-lspace:0;mso-table-rspace:0;}");
        sb.AppendLine(".meta-grid td{border:1px solid #FFFFFF !important;padding:4pt 6pt 6pt 0;vertical-align:bottom;width:50%;}");
        sb.AppendLine(".lbl{font-weight:normal;}");
        sb.AppendLine(".val-line{border-bottom:1pt solid #000000;display:block;min-height:14pt;margin-top:2pt;padding-bottom:1pt;}");
        sb.AppendLine(".full-row{width:100%;border-collapse:collapse;}");
        sb.AppendLine(".full-row td{border:1px solid #FFFFFF !important;padding:6pt 0;vertical-align:bottom;}");
        sb.AppendLine(".block2{margin-top:16pt;font-size:11pt;font-family:Calibri,Arial,sans-serif;}");
        sb.AppendLine(".eq-title{font-weight:bold;margin:0 0 6pt 0;}");
        sb.AppendLine(".eq-table{width:100%;border-collapse:collapse;mso-table-lspace:0;mso-table-rspace:0;}");
        sb.AppendLine(".eq-table td,.eq-table th{border:1px solid #000000;padding:4pt 5pt;vertical-align:middle;font-size:11pt;}");
        sb.AppendLine(".eq-mid{width:100%;border-collapse:collapse;mso-table-lspace:0;mso-table-rspace:0;}");
        sb.AppendLine(".eq-mid td{border:none !important;padding:2pt 4pt 2pt 0;vertical-align:middle;font-size:11pt;width:50%;}");
        sb.AppendLine(".works-title{font-weight:bold;margin:14pt 0 6pt 0;}");
        sb.AppendLine(".works-table{width:100%;border-collapse:collapse;mso-table-lspace:0;mso-table-rspace:0;}");
        sb.AppendLine(".works-table td,.works-table th{border:1px solid #000000;padding:4pt 5pt;vertical-align:middle;font-size:11pt;}");
        sb.AppendLine(".works-mark{width:22pt;text-align:center;}");
        sb.AppendLine(".works-legend td{border:1px solid #000000;padding:4pt 5pt;font-size:11pt;}");
        sb.AppendLine(".bottom-const{margin-top:22pt;font-size:11pt;}");
        sb.AppendLine(".bottom-section-title{font-weight:bold;text-transform:uppercase;margin:10pt 0 4pt 0;}");
        sb.AppendLine(".lined-table{width:100%;border-collapse:collapse;mso-table-lspace:0;mso-table-rspace:0;}");
        sb.AppendLine(".lined-table td{border-bottom:1px solid #7A7A7A;height:16pt;vertical-align:bottom;padding:0 0 1pt 0;font-size:11pt;}");
        sb.AppendLine(".signature-grid{width:100%;border-collapse:collapse;margin-top:16pt;mso-table-lspace:0;mso-table-rspace:0;}");
        sb.AppendLine(".signature-grid td{vertical-align:top;border:none !important;padding:0;}");
        sb.AppendLine(".signature-title{font-weight:bold;margin:0 0 12pt 0;line-height:1.2;}");
        sb.AppendLine(".signature-fields{width:100%;border-collapse:collapse;mso-table-lspace:0;mso-table-rspace:0;}");
        sb.AppendLine(".signature-fields td{border:none !important;padding:2pt 0;font-size:11pt;}");
        sb.AppendLine(".signature-label{width:90pt;white-space:nowrap;padding-right:6pt !important;}");
        sb.AppendLine(".signature-line{border-bottom:1px solid #7A7A7A;min-height:14pt;display:block;}");
        sb.AppendLine(".footer-brand{margin-top:26pt;}");
        sb.AppendLine(".footer-rule{border-top:1.5pt solid ").Append(BrandRed).Append(";font-size:1pt;line-height:1pt;mso-line-height-rule:exactly;}");
        sb.AppendLine(".footer-meta{margin-top:8pt;width:100%;border-collapse:collapse;mso-table-lspace:0;mso-table-rspace:0;}");
        sb.AppendLine(".footer-meta td{border:none !important;color:").Append(BrandRed).Append(";font-size:11pt;}");
        sb.AppendLine(".footer-meta .right{text-align:right;}");
        sb.AppendLine("</style></head><body>");

        // --- Верхний блок (единый для всех): лого + отступ 7 см + вертикальная красная плашка ---
        sb.AppendLine("<table class=\"header-top\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
        sb.AppendLine("<td class=\"logo-cell\">");
        if (!string.IsNullOrEmpty(logoUri))
            sb.Append("<img src=\"").Append(logoUri).Append("\" alt=\"Brand Schutz\" />");
        else
            sb.Append("&nbsp;");
        sb.AppendLine("</td>");
        sb.AppendLine("<td class=\"gap-cell\">&nbsp;</td>");
        sb.AppendLine("<td class=\"contact-cell\">");
        sb.AppendLine("<table class=\"contact-wrap\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
        sb.AppendLine("<td class=\"contact-vbar\" style=\"min-height:3cm;\">&nbsp;</td>");
        sb.AppendLine("<td class=\"contact-text\">");
        AppendContactLine(sb, "ООО «Бранд Шутц»");
        AppendContactLine(sb, "+7(495) 363-8916");
        AppendContactLine(sb, "117648, г. Москва, вн.тер.г.");
        AppendContactLine(sb, "Муниципальный Округ Чертаново");
        AppendContactLine(sb, "Северное, мкр. Северное Чертаново,");
        AppendContactLine(sb, "д. 4, к. 402, пом. 6/2Т");
        sb.AppendLine("</td>");
        sb.AppendLine("</tr></table>");
        sb.AppendLine("</td>");
        sb.AppendLine("</tr></table>");

        // --- Заголовок акта (подзаголовок зависит от типа оборудования; номера — заглушки до отдельного поля акта) ---
        sb.AppendLine("<div class=\"doc-title\">");
        sb.AppendLine("АКТ _____ /_____<br/>");
        sb.Append("<span class=\"doc-title-sub\">").Append(Html(GetActSubtitle(profile))).AppendLine("</span>");
        sb.AppendLine("</div>");

        // --- Двухколоночный блок реквизитов (границы таблицы белые) ---
        sb.AppendLine("<table class=\"meta-grid\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
        sb.AppendLine("<td>");
        sb.AppendLine("<span class=\"lbl\">Заказчик:</span><span class=\"val-line\">").Append(Html(customer)).Append("</span></td>");
        sb.AppendLine("<td>");
        sb.AppendLine("<span class=\"lbl\">Дата проведения работ:</span><span class=\"val-line\">").Append(Html(workDates)).Append("</span></td>");
        sb.AppendLine("</tr><tr>");
        sb.AppendLine("<td>");
        sb.AppendLine("<span class=\"lbl\">Тип оборудования:</span><span class=\"val-line\">").Append(Html(equipmentType)).Append("</span></td>");
        sb.AppendLine("<td>");
        sb.AppendLine("<span class=\"lbl\">Модель:</span><span class=\"val-line\">").Append(Html(modelName)).Append("</span></td>");
        sb.AppendLine("</tr><tr>");
        sb.AppendLine("<td>");
        sb.AppendLine("<span class=\"lbl\">Серийный номер</span><span class=\"val-line\">").Append(Html(serial)).Append("</span></td>");
        sb.AppendLine("<td>");
        sb.AppendLine("<span class=\"lbl\">Номер установки:</span><span class=\"val-line\">").Append(Html(unitNumber)).Append("</span></td>");
        sb.AppendLine("</tr></table>");

        sb.AppendLine("<table class=\"full-row\" cellspacing=\"0\" cellpadding=\"0\"><tr><td>");
        sb.AppendLine("<span class=\"lbl\">Вид работ:</span><span class=\"val-line\">").Append(Html(workKind)).Append("</span>");
        sb.AppendLine("</td></tr><tr><td>");
        sb.AppendLine("<span class=\"lbl\">Часы эксплуатации:</span><span class=\"val-line\">").Append(Html(hours)).Append("</span>");
        sb.AppendLine("</td></tr></table>");

        // Второй блок отличается по профилю оборудования.
        AppendSecondBlock(sb, model, profile, stateFlags);
        AppendBottomConstantBlock(sb);

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static void AppendContactLine(StringBuilder sb, string text)
    {
        sb.Append("<p class=\"contact-line\">").Append(Html(text)).AppendLine("</p>");
    }

    private static ActExportProfile DetectProfile(string? equipmentTypeName)
    {
        // Эвристика по тексту типа оборудования.
        // Если появятся новые типы актов — добавить условия здесь.
        var et = equipmentTypeName ?? string.Empty;
        if (et.Contains("осушит", StringComparison.OrdinalIgnoreCase))
            return ActExportProfile.Dryer;
        if (et.Contains("компресс", StringComparison.OrdinalIgnoreCase))
            return ActExportProfile.Compressor;
        return ActExportProfile.Installation;
    }

    private static string GetActSubtitle(ActExportProfile profile) =>
        profile switch
        {
            ActExportProfile.Dryer => "Технического обслуживания осушителя",
            ActExportProfile.Installation => "Технического обслуживания компонентов установки",
            _ => "Технического обслуживания компрессора"
        };

    private static string NormalizeStateString(string? display)
    {
        if (string.IsNullOrWhiteSpace(display))
            return string.Empty;
        return display.Trim().ToLowerInvariant();
    }

    private static EquipmentStateFlags ParseEquipmentState(string? display, ActExportProfile profile)
    {
        // Текущее ограничение данных:
        // в БД хранится одно состояние (обычно comp_state), поэтому оно копируется
        // одновременно в "прибытие" и "убытие" (до появления отдельных полей).
        var s = NormalizeStateString(display);
        if (string.IsNullOrEmpty(s))
            return new EquipmentStateFlags(false, false, false, false);

        var notWorking = s.Contains("не рабоч", StringComparison.OrdinalIgnoreCase)
            || s.Contains("не работ", StringComparison.OrdinalIgnoreCase);
        if (notWorking)
            return new EquipmentStateFlags(false, false, false, true);

        var off = s.Contains("выключ", StringComparison.OrdinalIgnoreCase);
        var underLoad = profile == ActExportProfile.Dryer
            ? s.Contains("в работе", StringComparison.OrdinalIgnoreCase)
              || (s.Contains("работ", StringComparison.OrdinalIgnoreCase) && !off)
            : s.Contains("нагруз", StringComparison.OrdinalIgnoreCase);

        var hasWorkingKeyword = s.Contains("рабочее", StringComparison.OrdinalIgnoreCase);

        if (hasWorkingKeyword && !underLoad && !off)
            return new EquipmentStateFlags(true, false, false, false);
        if (underLoad && !off)
            return new EquipmentStateFlags(true, true, false, false);
        if (off && !underLoad)
            return new EquipmentStateFlags(true, false, true, false);
        if (underLoad && off)
            return new EquipmentStateFlags(true, true, true, false);
        if (hasWorkingKeyword)
            return new EquipmentStateFlags(true, underLoad, off, false);

        return new EquipmentStateFlags(false, false, false, false);
    }

    private static string Box(bool on) => on ? "☑" : "☐";

    private static void AppendSecondBlock(
        StringBuilder sb,
        ChecklistDocumentExportModel model,
        ActExportProfile profile,
        EquipmentStateFlags state)
    {
        // Здесь вызываются секции, которые можно менять независимо:
        // 1) состояние оборудования;
        // 2) перечень работ.
        sb.AppendLine("<div class=\"block2\">");
        AppendEquipmentStateSection(sb, profile, state);
        AppendWorksSection(sb, model, profile);
        sb.AppendLine("</div>");
    }

    /// <summary>
    /// Нижний постоянный блок (общий для всех актов):
    /// - "Дополнительные работы"
    /// - "Замечания и рекомендации"
    /// - подписи исполнителя/заказчика
    /// - нижняя красная линия + сайт/ИНН.
    /// </summary>
    private static void AppendBottomConstantBlock(StringBuilder sb)
    {
        sb.AppendLine("<div class=\"bottom-const\">");

        sb.AppendLine("<p class=\"bottom-section-title\">ДОПОЛНИТЕЛЬНЫЕ РАБОТЫ:</p>");
        AppendLinedRows(sb, rowCount: 4, firstLineText: string.Empty);

        sb.AppendLine("<p class=\"bottom-section-title\">ЗАМЕЧАНИЯ И РЕКОМЕНДАЦИИ:</p>");
        AppendLinedRows(sb, rowCount: 4, firstLineText: string.Empty);

        sb.AppendLine("<table class=\"signature-grid\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
        sb.AppendLine("<td style=\"width:48%;padding-right:18pt;\">");
        sb.AppendLine("<p class=\"signature-title\">Представитель<br/>ИСПОЛНИТЕЛЯ<br/>Работу выполнил.</p>");
        AppendSignatureFields(sb);
        sb.AppendLine("</td>");
        sb.AppendLine("<td style=\"width:4%;\">&nbsp;</td>");
        sb.AppendLine("<td style=\"width:48%;padding-left:18pt;\">");
        sb.AppendLine("<p class=\"signature-title\">Представитель<br/>ЗАКАЗЧИКА:<br/>Выполнение работ подтверждаю.</p>");
        AppendSignatureFields(sb);
        sb.AppendLine("</td>");
        sb.AppendLine("</tr></table>");

        sb.AppendLine("<div class=\"footer-brand\">");
        sb.AppendLine("<div class=\"footer-rule\">&nbsp;</div>");
        sb.AppendLine("<table class=\"footer-meta\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
        sb.AppendLine("<td>www. brandschutz.ru</td>");
        sb.AppendLine("<td class=\"right\">ИНН 7726589854</td>");
        sb.AppendLine("</tr></table>");
        sb.AppendLine("</div>");

        sb.AppendLine("</div>");
    }

    /// <summary>
    /// Рисует несколько горизонтальных строк.
    /// Текст можно печатать прямо поверх линии (line не пропадает, остается границей строки).
    /// </summary>
    private static void AppendLinedRows(StringBuilder sb, int rowCount, string? firstLineText)
    {
        sb.AppendLine("<table class=\"lined-table\" cellspacing=\"0\" cellpadding=\"0\">");
        for (var i = 0; i < rowCount; i++)
        {
            var text = i == 0 ? firstLineText : string.Empty;
            sb.Append("<tr><td>").Append(Html(text)).AppendLine("</td></tr>");
        }

        sb.AppendLine("</table>");
    }

    private static void AppendSignatureFields(StringBuilder sb)
    {
        sb.AppendLine("<table class=\"signature-fields\" cellspacing=\"0\" cellpadding=\"0\">");
        AppendSignatureRow(sb, "Должность:");
        AppendSignatureRow(sb, "ФИО:");
        AppendSignatureRow(sb, "Подпись:");
        sb.AppendLine("</table>");
    }

    private static void AppendSignatureRow(StringBuilder sb, string label)
    {
        sb.AppendLine("<tr>");
        sb.Append("<td class=\"signature-label\">").Append(Html(label)).AppendLine("</td>");
        sb.AppendLine("<td><span class=\"signature-line\">&nbsp;</span></td>");
        sb.AppendLine("</tr>");
    }

    private static void AppendEquipmentStateSection(StringBuilder sb, ActExportProfile profile, EquipmentStateFlags st)
    {
        // Подписи средней строки зависят от профиля (установка / компрессор / осушитель).
        var (midLeft, midRight) = profile switch
        {
            ActExportProfile.Dryer => ("Осушитель в работе", "Осушитель выключен"),
            ActExportProfile.Installation => ("Установка под нагрузкой", "Установка выключен"),
            _ => ("Компрессор под нагрузкой", "Компрессор выключен")
        };

        void AppendHalf(StringBuilder b, bool arrival, EquipmentStateFlags f)
        {
            // Универсальный рендер одной половины таблицы: прибытие/убытие.
            var d = arrival ? "прибытия" : "убытия";
            b.Append("<tr><td>").Append(Box(f.Working)).Append(" Рабочее на дату ").Append(d).AppendLine("</td></tr>");
            b.AppendLine("<tr><td>");
            b.AppendLine("<table class=\"eq-mid\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
            b.Append("<td>").Append(Box(f.UnderLoad)).Append(' ').Append(Html(midLeft)).AppendLine("</td>");
            b.Append("<td>").Append(Box(f.Off)).Append(' ').Append(Html(midRight)).AppendLine("</td>");
            b.AppendLine("</tr></table>");
            b.AppendLine("</td></tr>");
            b.Append("<tr><td>").Append(Box(f.NotWorking)).Append(" Не рабочее на дату ").Append(d).AppendLine("</td></tr>");
        }

        sb.AppendLine("<p class=\"eq-title\">Состояние оборудования</p>");
        sb.AppendLine("<table class=\"eq-table\" cellspacing=\"0\" cellpadding=\"0\"><tr>");
        sb.AppendLine("<th style=\"width:50%;text-align:center;\">на дату прибытия</th>");
        sb.AppendLine("<th style=\"width:50%;text-align:center;\">на дату убытия</th>");
        sb.AppendLine("</tr><tr>");
        sb.AppendLine("<td style=\"vertical-align:top;padding:0;\">");
        sb.AppendLine("<table class=\"eq-table\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border:none;\">");
        AppendHalf(sb, arrival: true, st);
        sb.AppendLine("</table></td>");
        sb.AppendLine("<td style=\"vertical-align:top;padding:0;\">");
        sb.AppendLine("<table class=\"eq-table\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border:none;\">");
        AppendHalf(sb, arrival: false, st);
        sb.AppendLine("</table></td>");
        sb.AppendLine("</tr></table>");
    }

    private static void AppendWorksSection(StringBuilder sb, ChecklistDocumentExportModel model, ActExportProfile profile)
    {
        // Отбираем только "рабочие" строки перечня, исключая шапочные field_code.
        var workItems = model.Answers.Where(IsWorkListRow).OrderBy(a => a.SortOrder).ToList();
        var mid = (workItems.Count + 1) / 2;
        var left = workItems.Take(mid).ToList();
        var right = workItems.Skip(mid).ToList();
        var rows = Math.Max(left.Count, right.Count);

        sb.AppendLine("<p class=\"works-title\">Перечень выполненных работ:</p>");
        sb.AppendLine("<table class=\"works-table\" cellspacing=\"0\" cellpadding=\"0\">");
        for (var i = 0; i < rows; i++)
        {
            sb.AppendLine("<tr>");
            if (i < left.Count)
            {
                sb.Append("<td>").Append(Html(left[i].QuestionText)).AppendLine("</td>");
                sb.Append("<td class=\"works-mark\">").Append(FormatWorkMark(left[i], profile)).AppendLine("</td>");
            }
            else
            {
                sb.AppendLine("<td>&nbsp;</td><td class=\"works-mark\">&nbsp;</td>");
            }

            if (i < right.Count)
            {
                sb.Append("<td>").Append(Html(right[i].QuestionText)).AppendLine("</td>");
                sb.Append("<td class=\"works-mark\">").Append(FormatWorkMark(right[i], profile)).AppendLine("</td>");
            }
            else
            {
                sb.AppendLine("<td>&nbsp;</td><td class=\"works-mark\">&nbsp;</td>");
            }

            sb.AppendLine("</tr>");
        }

        if (profile == ActExportProfile.Compressor)
        {
            // Для компрессора внизу печатаем легенду кодов К/Ч/З/П/—.
            sb.AppendLine("<tr class=\"works-legend\"><td colspan=\"4\">");
            sb.Append("К — контроль; Ч — чистка; З — замена; П — параметры; — — не выполн.");
            sb.AppendLine("</td></tr>");
        }

        sb.AppendLine("</table>");
    }

    private static bool IsWorkListRow(ChecklistDocumentAnswer a)
    {
        var code = a.FieldCode ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
            return false;

        if (code.StartsWith("extra_", StringComparison.OrdinalIgnoreCase))
            return false;
        if (code.StartsWith("remarks_", StringComparison.OrdinalIgnoreCase))
            return false;

        return !HeaderFieldCodes.Contains(code);
    }

    private static readonly HashSet<string> HeaderFieldCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "start_date",
        "start_time",
        "end_date",
        "end_time",
        "workers",
        "unit_number",
        "equipment_pick",
        "comp_model",
        "comp_type",
        "comp_serial",
        "serial_number",
        "compressor_serial",
        "serial",
        "comp_state",
        "equipment_state",
        "state",
        "operating_hours",
        "hours",
        "runtime_hours",
        "motor_hours_note",
        "oht_hours",
        "fridge_hours",
        "model"
    };

    private static string FormatWorkMark(ChecklistDocumentAnswer a, ActExportProfile profile)
    {
        // Центральная развилка правил отметок по профилю.
        if (profile == ActExportProfile.Compressor)
            return FormatCompressorWorkMark(a);

        if (profile == ActExportProfile.Dryer)
            return FormatDryerOrInstallationMark(a, preferDashWhenEmpty: true);

        return FormatDryerOrInstallationMark(a, preferDashWhenEmpty: false);
    }

    private static string FormatCompressorWorkMark(ChecklistDocumentAnswer a)
    {
        // Правила для компрессора:
        // - буквенные коды К/Ч/З/П;
        // - "—" для не выполнено;
        // - для bool "Да" используем "К" как безопасный дефолт.
        var disp = string.IsNullOrWhiteSpace(a.ValueDisplay) ? a.ValueRaw : a.ValueDisplay;
        disp = disp.Trim();
        if (string.IsNullOrEmpty(disp))
            return "\u00A0";

        if (disp.StartsWith("-", StringComparison.Ordinal)
            || disp.StartsWith("—", StringComparison.Ordinal)
            || disp.Equals("нет", StringComparison.OrdinalIgnoreCase)
            || disp.Equals("н/п", StringComparison.OrdinalIgnoreCase))
            return "—";

        if (string.Equals(a.FieldTypeName, "boolean", StringComparison.OrdinalIgnoreCase))
        {
            if (disp.Equals("Да", StringComparison.OrdinalIgnoreCase) || string.Equals(a.ValueRaw, "true", StringComparison.OrdinalIgnoreCase))
                return "К";
            return "—";
        }

        var c0 = disp[0];
        var u = char.ToUpperInvariant(c0);
        return u switch
        {
            'K' => "К",
            'C' => "Ч",
            'Z' => "З",
            'P' => "П",
            'К' => "К",
            'Ч' => "Ч",
            'З' => "З",
            'П' => "П",
            _ => Html(disp.Substring(0, 1))
        };
    }

    private static string FormatDryerOrInstallationMark(ChecklistDocumentAnswer a, bool preferDashWhenEmpty)
    {
        // Для осушителя/установки:
        // - используем ✓ / ☐ / — в зависимости от профиля и пустоты значения.
        // Все символы — обычный текст, поэтому в Word пользователь может их править вручную.
        var disp = string.IsNullOrWhiteSpace(a.ValueDisplay) ? a.ValueRaw : a.ValueDisplay;
        disp = disp.Trim();
        if (string.IsNullOrEmpty(disp))
            return preferDashWhenEmpty ? "—" : "☐";

        if (disp.StartsWith("-", StringComparison.Ordinal) || disp.StartsWith("—", StringComparison.Ordinal))
            return "—";

        if (string.Equals(a.FieldTypeName, "boolean", StringComparison.OrdinalIgnoreCase))
        {
            if (disp.Equals("Да", StringComparison.OrdinalIgnoreCase) || string.Equals(a.ValueRaw, "true", StringComparison.OrdinalIgnoreCase))
                return "✓";
            return preferDashWhenEmpty ? "—" : "☐";
        }

        if (disp.Equals("Нет", StringComparison.OrdinalIgnoreCase) || string.Equals(a.ValueRaw, "false", StringComparison.OrdinalIgnoreCase))
            return preferDashWhenEmpty ? "—" : "☐";

        return "✓";
    }

    private static string? GetAnswerDisplay(IReadOnlyList<ChecklistDocumentAnswer> answers, params string[] fieldCodes)
    {
        // Берем первое непустое значение по списку кодов-синонимов.
        // Это защищает от различий в названиях field_code между шаблонами.
        foreach (var code in fieldCodes)
        {
            var row = answers.FirstOrDefault(a =>
                !string.IsNullOrWhiteSpace(a.FieldCode) &&
                string.Equals(a.FieldCode, code, StringComparison.OrdinalIgnoreCase));
            if (row is null)
                continue;
            var v = string.IsNullOrWhiteSpace(row.ValueDisplay) ? row.ValueRaw : row.ValueDisplay;
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return null;
    }

    private static string FormatWorkDateRange(ChecklistDocumentHeader header)
    {
        var s = header.StartedAt?.ToLocalTime();
        var e = header.EndedAt?.ToLocalTime();
        if (s is null && e is null)
            return "___________";
        if (s is not null && e is not null)
        {
            var ds = s.Value.ToString("dd.MM.yy", CultureInfo.InvariantCulture);
            var de = e.Value.ToString("dd.MM.yy", CultureInfo.InvariantCulture);
            return $"{ds} — {de}";
        }

        var one = (s ?? e)!.Value.ToString("dd.MM.yy", CultureInfo.InvariantCulture);
        return one;
    }

    private static string GetLogoDataUri()
    {
        // Лого читается из EmbeddedResource и кэшируется на время жизни процесса.
        lock (LogoLock)
        {
            if (_logoDataUri is not null)
                return _logoDataUri;

            var assembly = typeof(SqliteChecklistDocumentExportService).Assembly;
            using var stream = assembly.GetManifestResourceStream(LogoResourceName);
            if (stream is null)
            {
                _logoDataUri = string.Empty;
                return _logoDataUri;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var b64 = Convert.ToBase64String(ms.ToArray());
            _logoDataUri = "data:image/png;base64," + b64;
            return _logoDataUri;
        }
    }

    private static string Html(string? value)
    {
        // Базовая HTML-экранизация + переносы строк для безопасного рендера в Word HTML.
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
        // Нормализация фрагмента имени файла под ограничения ОС.
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
        // SQL шапки листа: организация, объект, тип оборудования, установка, тип ТО, даты.
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
        // Сначала пытаемся взять template_id напрямую из checklists.
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

        // Если там null/пусто — fallback по связке equipment_type + maintenance_type.
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
        // Берем все template items и подтягиваем ответы текущего checklist (LEFT JOIN),
        // чтобы в документ попадали также незаполненные строки.
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
        // Нормализация значений к единому виду:
        // - для start/end date-time берем значения из header;
        // - bool -> Да/Нет;
        // - radio/dropdown -> label выбранной опции;
        // - multi -> join labels через " | ".
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
