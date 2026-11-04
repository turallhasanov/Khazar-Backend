using Khazar.Domain.Entities;
using Khazar.Domain.Exceptions;
using Khazar.Domain.ValueObjects;

namespace Khazar.WebAPI.Tests.Domain;

/// <summary>Variant stock never goes negative, and variants stay identifiable by SKU and by color + size.</summary>
public sealed class VariantStockTests
{
    private static Product NewProduct() =>
        new("Yun palto", "yun-palto", "Təsvir", 149.90m, "100% yun", new Category("Coat", "Palto", "palto", 0), "/images/coat.jpg");

    [Fact]
    public void Decreasing_below_zero_throws_InsufficientStock_and_keeps_the_balance()
    {
        var variant = NewProduct().AddVariant("M", "Qara", "khz-coat-blk-m", 2);

        var act = () => variant.DecreaseStock(3);

        act.Should().Throw<InsufficientStockException>().Which.Sku.Should().Be("KHZ-COAT-BLK-M");
        variant.StockQuantity.Should().Be(2);
    }

    [Fact]
    public void Decreasing_exactly_the_balance_leaves_zero_and_out_of_stock()
    {
        var variant = NewProduct().AddVariant("M", "Qara", "KHZ-1", 2);

        variant.DecreaseStock(2);

        variant.StockQuantity.Should().Be(0);
        variant.IsInStock.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Stock_moves_by_positive_amounts_only(int quantity)
    {
        var variant = NewProduct().AddVariant("M", "Qara", "KHZ-1", 5);

        variant.Invoking(v => v.DecreaseStock(quantity)).Should().Throw<DomainException>();
        variant.Invoking(v => v.IncreaseStock(quantity)).Should().Throw<DomainException>();
        variant.StockQuantity.Should().Be(5);
    }

    [Fact]
    public void A_negative_stock_cannot_be_set_or_created()
    {
        var product = NewProduct();
        var variant = product.AddVariant("M", "Qara", "KHZ-1", 5);

        variant.Invoking(v => v.SetStock(-1)).Should().Throw<DomainException>().WithMessage("StockQuantity cannot be negative.");
        product.Invoking(p => p.AddVariant("L", "Qara", "KHZ-2", -3)).Should().Throw<DomainException>();
        variant.StockQuantity.Should().Be(5);
    }

    [Fact]
    public void Skus_are_stored_upper_case_and_trimmed()
    {
        var variant = NewProduct().AddVariant(" M ", " Qara ", "  khz-coat-blk-m ", 1);

        variant.SKU.Should().Be("KHZ-COAT-BLK-M");
        variant.Size.Should().Be("M");
        variant.Color.Should().Be("Qara");
    }

    [Fact]
    public void A_sku_is_unique_within_the_product_whatever_its_case()
    {
        var product = NewProduct();
        product.AddVariant("M", "Qara", "KHZ-1", 1);

        product.Invoking(p => p.AddVariant("L", "Bej", "khz-1", 1))
            .Should().Throw<DomainException>().WithMessage("SKU 'KHZ-1' already exists on this product.");
    }

    [Fact]
    public void Color_and_size_together_identify_a_variant_case_insensitively()
    {
        var product = NewProduct();
        product.AddVariant("M", "Qara", "KHZ-1", 1);

        product.Invoking(p => p.AddVariant("m", " qara ", "KHZ-2", 1)).Should().Throw<DomainException>();
        product.AddVariant("L", "Qara", "KHZ-3", 1).SortOrder.Should().Be(1);
    }

    [Fact]
    public void An_order_line_snapshots_the_variant_and_merges_repeat_adds()
    {
        var product = NewProduct();
        var variant = product.AddVariant("M", "Qara", "KHZ-1", 10);
        var order = new Order("Leyla Həsənova", "Leyla@Example.az", new Address("Nizami 10", "Bakı", "AZ1000", "Azərbaycan"));

        order.AddItem(variant, product.Name, 149.90m, 1);
        order.AddItem(variant, product.Name, 149.90m, 2);

        order.Items.Should().ContainSingle().Which.Quantity.Should().Be(3);
        order.TotalAmount.Should().Be(449.70m);
        order.CustomerEmail.Should().Be("leyla@example.az");
        order.OrderNumber.Should().MatchRegex("^KHZ-[A-HJ-NP-Z2-9]{5}$");
    }
}
