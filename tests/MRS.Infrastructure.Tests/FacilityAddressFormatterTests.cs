using MRS.Application.Facilities;

namespace MRS.Infrastructure.Tests;

public class FacilityAddressFormatterTests
{
	[Fact]
	public void Format_joins_city_street_and_house_with_commas() =>
		Assert.Equal("Курск, ул. Демо, 2", FacilityAddressFormatter.Format("Курск", "ул. Демо", "2", null, null));

	[Fact]
	public void Format_appends_structure_and_block_to_house() =>
		Assert.Equal("Москва, Ленина, 1 стр. 2 корп. А",
			FacilityAddressFormatter.Format("Москва", "Ленина", "1", "2", "А"));

	[Fact]
	public void FormatActObject_returns_full_address_without_city_duplication() =>
		Assert.Equal("Курск, ул. Демо, 2",
			FacilityAddressFormatter.FormatActObject("Курск", "ул. Демо", "2", null, null));
}
