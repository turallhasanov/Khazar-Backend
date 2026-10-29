using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Khazar.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            // One category per distinct value already on products. The seeded ones get the labels, Header/Footer slugs
            // and order KhazarDbInitializer uses; any other value keeps its name as label and goes last.
            migrationBuilder.Sql(
                """
                INSERT INTO [Categories] ([Id], [Name], [Label], [Slug], [SortOrder], [CreatedAt])
                SELECT NEWID(), d.[Category], COALESCE(k.[Label], d.[Category]),
                       COALESCE(k.[Slug], LOWER(REPLACE(d.[Category], N' ', N'-'))), COALESCE(k.[SortOrder], 1000),
                       SYSUTCDATETIME()
                FROM (SELECT DISTINCT [Category] FROM [Products]) AS d
                LEFT JOIN (VALUES
                    (N'Hoodie', N'Hoodie', N'hoodie', 10),
                    (N'T-Shirt', N'T-Shirt', N't-shirt', 20),
                    (N'Pants', N'Şalvar', N'salvar', 30),
                    (N'Sweatshirt', N'Sweatshirt', N'sweatshirt', 40),
                    (N'Longsleeve', N'Longsleeve', N'longsleeve', 50),
                    (N'Coat', N'Palto', N'palto', 60)
                ) AS k ([Name], [Label], [Slug], [SortOrder]) ON k.[Name] = d.[Category];
                """);

            // Nullable until every product points at its category; only then is the old text column dropped.
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE p SET p.[CategoryId] = c.[Id]
                FROM [Products] AS p
                INNER JOIN [Categories] AS c ON c.[Name] = p.[Category];
                """);

            migrationBuilder.DropIndex(
                name: "IX_Products_Category",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Products");

            migrationBuilder.AlterColumn<Guid>(
                name: "CategoryId",
                table: "Products",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_CategoryId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_CategoryId",
                table: "Products");

            // Restore the text column from each product's category Name before the table goes away.
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE p SET p.[Category] = c.[Name]
                FROM [Products] AS p
                INNER JOIN [Categories] AS c ON c.[Id] = p.[CategoryId];
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Category",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Category",
                table: "Products",
                column: "Category");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
