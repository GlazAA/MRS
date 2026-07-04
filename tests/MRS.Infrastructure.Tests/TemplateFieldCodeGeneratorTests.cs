using MRS.Application.Checklists;

namespace MRS.Infrastructure.Tests;

public class TemplateFieldCodeGeneratorTests
{
	[Fact]
	public void SuggestFromQuestion_uses_known_codes()
	{
		var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		Assert.Equal("start_date", TemplateFieldCodeGenerator.SuggestFromQuestion("Дата начала", used));
	}

	[Fact]
	public void SuggestFromQuestion_avoids_duplicates()
	{
		var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "davlenie" };
		Assert.Equal("davlenie_2", TemplateFieldCodeGenerator.SuggestFromQuestion("Давление", used));
	}

	[Fact]
	public void SuggestMaintenanceTypeCode_builds_int_prefix()
	{
		Assert.Equal("INT-POLUGODOVOE", TemplateFieldCodeGenerator.SuggestMaintenanceTypeCode("Полугодовое"));
	}
}
