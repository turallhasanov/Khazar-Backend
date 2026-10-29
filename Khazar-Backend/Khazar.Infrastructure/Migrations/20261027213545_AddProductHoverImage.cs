using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Khazar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductHoverImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HoverImage",
                table: "Products",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            // The seeder only runs on an empty catalog, so give the already-seeded products the same hover photos
            // KhazarDbInitializer assigns. Products an admin created or already gave a hover photo are left alone.
            foreach (var (slug, photoId) in SeedHoverPhotos)
            {
                migrationBuilder.Sql(
                    $"UPDATE [Products] SET [HoverImage] = N'https://images.unsplash.com/photo-{photoId}?auto=format&fit=crop&w=900&q=80' " +
                    $"WHERE [Slug] = N'{slug}' AND [HoverImage] IS NULL;");
            }
        }

        private static readonly (string Slug, string PhotoId)[] SeedHoverPhotos =
        [
            ("zoho-oversize-sweatshirt", "1620799140408-edc6dcb6d633"),
            ("european-cotton-t-shirt", "1581655353564-df123a1eb820"),
            ("minimalist-cargo-pants", "1473966968600-fa801b869a1a"),
            ("heavyweight-hoodie", "1556821840-3a63f95609a7"),
            ("merino-longsleeve", "1516762689617-e1cffcef479d"),
            ("tailored-wool-coat", "1551232864-3f0890e580d9"),
            ("vintage-wash-sweatshirt", "1572495641004-28421ae52e52"),
            ("essential-oversize-t-shirt", "1574180566232-aaad1b5b8450"),
            ("pleated-wide-pants", "1490481651871-ab68de25d43d"),
            ("double-face-coat", "1485462537746-965f33f7f6a7"),
        ];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HoverImage",
                table: "Products");
        }
    }
}
