using Khazar.Application.Common;
using Khazar.Domain.Entities;
using Khazar.Domain.Exceptions;

namespace Khazar.WebAPI.Tests.NexusErp;

/// <summary>NexusERP names variants "Name (Color / Size)"; the storefront keeps the name clean and the details as attributes.</summary>
public sealed class ProductNameParserTests
{
    [Theory]
    [InlineData("Usaq Hoodie (S)", "Usaq Hoodie", "S", null)]
    [InlineData("Usaq Hoodie (xl)", "Usaq Hoodie", "XL", null)]
    [InlineData("Yun palto (Qara / M)", "Yun palto", "M", "Qara")]
    [InlineData("Yun palto (Tünd yaşıl / XXL)", "Yun palto", "XXL", "Tünd yaşıl")]
    [InlineData("Palto (Qara)", "Palto", null, "Qara")]
    [InlineData("Ayaqqabı (42)", "Ayaqqabı", "42", null)]
    public void Splits_the_trailing_variant_details_from_the_name(string name, string baseName, string? size, string? color)
    {
        ProductNameParser.Parse(name).Should().Be(new ParsedProductName(baseName, size, color));
    }

    [Theory]
    [InlineData("Usaq Hoodie")]
    [InlineData("Palto (2-3 yaş)")]       // does not read like a size or a color
    [InlineData("(S)")]                   // nothing would be left of the name
    [InlineData("Palto (A / B / C)")]     // more than color / size
    [InlineData("Palto (S) yeni")]        // not a trailing group
    [InlineData("")]
    public void Leaves_a_name_without_variant_details_alone(string name)
    {
        var parsed = ProductNameParser.Parse(name);

        parsed.BaseName.Should().Be(name.Trim());
        parsed.HasVariantDetails.Should().BeFalse();
    }

    [Fact]
    public void A_variant_can_be_renamed_but_never_into_a_pair_another_variant_has()
    {
        var product = new Product("Hoodie", "hoodie", "Təsvir", 20m, "pambıq", new Category("Top", "Üst", "ust", 0), "/images/h.jpg");
        var standard = product.AddVariant("Standart", "Standart", "KHZ-1", 1);
        product.AddVariant("M", "Qara", "KHZ-2", 1);

        product.ChangeVariantAttributes(standard.Id, "S", "Qara");

        (standard.Size, standard.Color, standard.SKU).Should().Be(("S", "Qara", "KHZ-1"));
        var clash = () => product.ChangeVariantAttributes(standard.Id, "M", "Qara");
        clash.Should().Throw<DomainException>();
    }
}
