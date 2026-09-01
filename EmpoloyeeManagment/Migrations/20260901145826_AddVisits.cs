using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpoloyeeManagment.Migrations
{
    /// <inheritdoc />
    public partial class AddVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClientId = table.Column<int>(type: "int", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_RelatedVisitId",
                table: "Tasks",
                column: "RelatedVisitId",
                unique: true,
                filter: "[RelatedVisitId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Visits_RelatedVisitId",
                table: "Tasks",
                column: "RelatedVisitId",
                principalTable: "Visits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Visits_RelatedVisitId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_RelatedVisitId",
                table: "Tasks");
        }
    }
}
