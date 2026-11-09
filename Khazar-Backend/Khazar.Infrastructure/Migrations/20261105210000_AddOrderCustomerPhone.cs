using Khazar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Khazar.Infrastructure.Migrations
{
    /// <summary>
    /// Keeps the shopper's phone on the order so it can be sent to NexusERP. Nullable: orders placed before it was kept have none.
    /// </summary>
    [DbContext(typeof(KhazarDbContext))]
    [Migration("20261105210000_AddOrderCustomerPhone")]
    public partial class AddOrderCustomerPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerPhone",
                table: "Orders");
        }
    }
}
