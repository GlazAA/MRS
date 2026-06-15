using Microsoft.Data.Sqlite;
using MRS.Application.Facilities;

namespace MRS.Infrastructure.Sqlite;

internal static class SqliteOrganizationName
{
	public static string ReadListName(SqliteDataReader reader, int fullNameOrdinal, int shortNameOrdinal, int legalFormOrdinal)
	{
		var fullName = reader.GetString(fullNameOrdinal);
		var shortName = reader.IsDBNull(shortNameOrdinal) ? null : reader.GetString(shortNameOrdinal);
		var legalForm = reader.IsDBNull(legalFormOrdinal) ? null : reader.GetString(legalFormOrdinal);
		return OrganizationLegalForm.FormatListName(legalForm, fullName, shortName);
	}

	public static string ReadActName(SqliteDataReader reader, int fullNameOrdinal, int shortNameOrdinal, int legalFormOrdinal)
	{
		var fullName = reader.GetString(fullNameOrdinal);
		var shortName = reader.IsDBNull(shortNameOrdinal) ? null : reader.GetString(shortNameOrdinal);
		var legalForm = reader.IsDBNull(legalFormOrdinal) ? null : reader.GetString(legalFormOrdinal);
		return OrganizationLegalForm.FormatActName(legalForm, fullName, shortName);
	}
}
