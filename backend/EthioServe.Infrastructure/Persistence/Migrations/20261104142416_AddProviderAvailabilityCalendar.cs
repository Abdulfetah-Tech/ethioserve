using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EthioServe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderAvailabilityCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "availability_schedule",
                table: "provider_profiles",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "availability_schedule",
                table: "provider_profiles");
        }
    }
}
