using Microsoft.Data.Sqlite;
using MRS.Application.Checklists;
using MRS.Application.Storage;

namespace MRS.Infrastructure.Sqlite;

public sealed class SqliteChecklistTemplateOptionService : IChecklistTemplateOptionService
{
	private readonly ILocalDatabasePath _paths;
	private readonly ILocalDatabaseBootstrapper _bootstrapper;

	public SqliteChecklistTemplateOptionService(ILocalDatabasePath paths, ILocalDatabaseBootstrapper bootstrapper)
	{
		_paths = paths;
		_bootstrapper = bootstrapper;
	}

	public async Task<TemplateFieldOption> EnsureOptionAsync(
		int templateItemId,
		string label,
		CancellationToken cancellationToken = default)
	{
		var trimmed = (label ?? string.Empty).Trim();
		if (trimmed.Length == 0)
			throw new InvalidOperationException("Укажите название варианта.");

		await using var connection = await SqliteLocalDatabase.OpenReadyAsync(_paths, _bootstrapper, cancellationToken).ConfigureAwait(false);

		using (var find = connection.CreateCommand())
		{
			find.CommandText = """
				SELECT id, option_label, sort_order
				FROM checklist_template_item_options
				WHERE checklist_template_item_id = $item
				  AND lower(option_label) = lower($label)
				LIMIT 1;
				""";
			find.Parameters.AddWithValue("$item", templateItemId);
			find.Parameters.AddWithValue("$label", trimmed);
			await using var reader = await find.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				return new TemplateFieldOption(
					reader.GetInt32(0),
					reader.GetString(1),
					reader.GetInt32(2));
			}
		}

		int nextSort;
		using (var max = connection.CreateCommand())
		{
			max.CommandText = """
				SELECT COALESCE(MAX(sort_order), 0) + 1
				FROM checklist_template_item_options
				WHERE checklist_template_item_id = $item;
				""";
			max.Parameters.AddWithValue("$item", templateItemId);
			var obj = await max.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			nextSort = Convert.ToInt32(obj);
		}

		using var insert = connection.CreateCommand();
		insert.CommandText = """
			INSERT INTO checklist_template_item_options (checklist_template_item_id, sort_order, option_label)
			VALUES ($item, $sort, $label);
			SELECT last_insert_rowid();
			""";
		insert.Parameters.AddWithValue("$item", templateItemId);
		insert.Parameters.AddWithValue("$sort", nextSort);
		insert.Parameters.AddWithValue("$label", trimmed);
		var idObj = await insert.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		var id = Convert.ToInt32(idObj);
		return new TemplateFieldOption(id, trimmed, nextSort);
	}
}
