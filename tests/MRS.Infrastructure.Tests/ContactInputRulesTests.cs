using MRS.Application.Facilities;

namespace MRS.Infrastructure.Tests;

public class ContactInputRulesTests
{
	[Theory]
	[InlineData("89991234567", "+7 (999) 123-45-67")]
	[InlineData("79991234567", "+7 (999) 123-45-67")]
	[InlineData("9991234567", "+7 (999) 123-45-67")]
	[InlineData("+7 999 123-45-67", "+7 (999) 123-45-67")]
	public void FormatPhoneInput_normalizes_ru_mobile(string raw, string expected) =>
		Assert.Equal(expected, ContactInputRules.FormatPhoneInput(raw));

	[Theory]
	[InlineData(null, true)]
	[InlineData("", true)]
	[InlineData("+7 (999) 123-45-67", true)]
	[InlineData("89991234567", true)]
	[InlineData("123", false)]
	[InlineData("+7 (999) 123", false)]
	public void IsPhoneValidOrEmpty(string? phone, bool expected) =>
		Assert.Equal(expected, ContactInputRules.IsPhoneValidOrEmpty(
			string.IsNullOrWhiteSpace(phone) ? phone : ContactInputRules.FormatPhoneInput(phone)));

	[Theory]
	[InlineData(null, true)]
	[InlineData("", true)]
	[InlineData("a@yandex.ru", true)]
	[InlineData("name@mail.ru", true)]
	[InlineData("user.name+tag@gmail.com", true)]
	[InlineData("bad@", false)]
	[InlineData("bad@domain", false)]
	[InlineData("no-at", false)]
	public void IsEmailValidOrEmpty(string? email, bool expected) =>
		Assert.Equal(expected, ContactInputRules.IsEmailValidOrEmpty(email));
}
