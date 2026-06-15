using MRS.Application.Facilities;

namespace MRS.Infrastructure.Tests;

public class OrganizationLegalFormTests
{
	[Theory]
	[InlineData("OOO", "Рога и копыта", "ООО Рога и копыта")]
	[InlineData("IP", "Иванов", "ИП Иванов")]
	[InlineData("AO", "Газпром", "АО Газпром")]
	public void FormatListName_includes_short_legal_form(string code, string company, string expected) =>
		Assert.Equal(expected, OrganizationLegalForm.FormatListName(code, company, null));

	[Theory]
	[InlineData("OOO", "Рога и копыта", "Общество с ограниченной ответственностью Рога и копыта")]
	[InlineData("ZAO", "Техно", "Закрытое акционерное общество Техно")]
	[InlineData("SELF_EMPLOYED", "Петров", "Самозанятый Петров")]
	public void FormatActName_uses_full_legal_form_without_quotes(string code, string company, string expected) =>
		Assert.Equal(expected, OrganizationLegalForm.FormatActName(code, company, null));

	[Fact]
	public void Legacy_organization_without_legal_form_uses_short_name() =>
		Assert.Equal("Мосархив", OrganizationLegalForm.FormatActName(null, "Полное имя", "Мосархив"));
}
