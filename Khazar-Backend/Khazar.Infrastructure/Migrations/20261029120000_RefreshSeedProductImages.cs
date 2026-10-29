using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Khazar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefreshSeedProductImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Swaps seed photos that showed the wrong garment or an off-palette color for the ones KhazarDbInitializer
            // now assigns. A row is only touched while it still holds the old seed photo, so an image an admin set is kept.
            foreach (var (slug, column, oldPhotoId, newPhotoId) in ReplacedPhotos)
                migrationBuilder.Sql(ReplaceSql(slug, column, oldPhotoId, newPhotoId));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (slug, column, oldPhotoId, newPhotoId) in ReplacedPhotos)
                migrationBuilder.Sql(ReplaceSql(slug, column, newPhotoId, oldPhotoId));
        }

        private static string ReplaceSql(string slug, string column, string fromPhotoId, string toPhotoId) =>
            $"UPDATE [Products] SET [{column}] = N'{UnsplashImage(toPhotoId)}' " +
            $"WHERE [Slug] = N'{slug}' AND [{column}] = N'{UnsplashImage(fromPhotoId)}';";

        private static string UnsplashImage(string photoId) =>
            $"https://images.unsplash.com/photo-{photoId}?auto=format&fit=crop&w=900&q=80";

        private static readonly (string Slug, string Column, string OldPhotoId, string NewPhotoId)[] ReplacedPhotos =
        [
            ("zoho-oversize-sweatshirt", "HoverImage", "1620799140408-edc6dcb6d633", "1630254688956-40da9f30216a"),
            ("minimalist-cargo-pants", "HoverImage", "1473966968600-fa801b869a1a", "1548883354-7622d03aca27"),
            ("merino-longsleeve", "HoverImage", "1516762689617-e1cffcef479d", "1536992266094-82847e1fd431"),
            ("tailored-wool-coat", "HoverImage", "1551232864-3f0890e580d9", "1539533018447-63fcce2678e3"),
            ("tailored-wool-coat", "CoverImage", "1539533018447-63fcce2678e3", "1619603364937-8d7af41ef206"),
            ("vintage-wash-sweatshirt", "HoverImage", "1572495641004-28421ae52e52", "1697425603592-23863028592b"),
            ("pleated-wide-pants", "HoverImage", "1490481651871-ab68de25d43d", "1770909792040-ccfac330491d"),
            ("double-face-coat", "CoverImage", "1648066380643-675d30c3cd17", "1770644935626-f61d233f4827"),
            ("double-face-coat", "HoverImage", "1485462537746-965f33f7f6a7", "1701707243530-6d5c5629f150"),
        ];
    }
}
