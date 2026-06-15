using System.Text;

namespace MRS.Application.Checklists;

public static class ChecklistEditConflictReport
{
	public static string FormatForAdmin(
		ChecklistEditInfo info,
		string engineerName,
		IReadOnlyList<ChecklistUpdateDryRunField> savedFields,
		IReadOnlyList<ChecklistUpdateDryRunField> blockedFields)
	{
		var sb = new StringBuilder();
		sb.AppendLine("Конфликт при редактировании контрольного листа");
		sb.AppendLine();
		sb.AppendLine($"Инженер: {engineerName}");
		sb.AppendLine($"КЛ №{info.ChecklistId} • {info.MaintenanceTypeName}");
		sb.AppendLine($"Заказчик: {info.OrganizationName}");
		sb.AppendLine($"Объект: {info.FacilityName}");
		sb.AppendLine($"Оборудование: {info.EquipmentTypeName}, установка {info.InstallationLabel}");
		sb.AppendLine();

		if (blockedFields.Count > 0)
		{
			sb.AppendLine("Не удалось сохранить:");
			foreach (var f in blockedFields)
				AppendFieldLine(sb, f);
			sb.AppendLine();
		}

		if (savedFields.Count > 0)
		{
			sb.AppendLine("Сохранено успешно:");
			foreach (var f in savedFields)
				AppendFieldLine(sb, f);
		}

		return sb.ToString().TrimEnd();
	}

	private static void AppendFieldLine(StringBuilder sb, ChecklistUpdateDryRunField field)
	{
		var code = string.IsNullOrWhiteSpace(field.FieldCode) ? "—" : field.FieldCode;
		sb.Append("- ").Append(field.QuestionText);
		sb.Append(" [").Append(code).Append(']');
		if (!string.IsNullOrWhiteSpace(field.AttemptedValue))
			sb.Append(": «").Append(field.AttemptedValue).Append('»');
		if (!string.IsNullOrWhiteSpace(field.Reason))
			sb.Append(" — ").Append(field.Reason);
		sb.AppendLine();
	}
}
