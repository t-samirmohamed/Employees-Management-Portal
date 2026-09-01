using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpoloyeeManagment.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitClientLocationForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Visits_ClientId",
                table: "Visits",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Visits_LocationId",
                table: "Visits",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Visits_Clients_ClientId",
                table: "Visits",
                column: "ClientId",
                principalTable: "Clients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Visits_Locations_LocationId",
                table: "Visits",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Visits_Clients_ClientId",
                table: "Visits");

            migrationBuilder.DropForeignKey(
                name: "FK_Visits_Locations_LocationId",
                table: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_Visits_ClientId",
                table: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_Visits_LocationId",
                table: "Visits");
        }
    }
}
