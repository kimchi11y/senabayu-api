using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Homestay.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingNights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_RoomId_CheckIn_CheckOut",
                table: "Bookings");

            migrationBuilder.CreateTable(
                name: "BookingNights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BookingId = table.Column<int>(type: "integer", nullable: false),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    NightDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingNights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingNights_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingNights_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RoomId",
                table: "Bookings",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingNights_BookingId",
                table: "BookingNights",
                column: "BookingId");

            // Bookings made before this table existed need their nights too, before the unique index goes on.
            migrationBuilder.Sql("""
                INSERT INTO "BookingNights" ("BookingId", "RoomId", "NightDate", "Status")
                SELECT b."Id", b."RoomId", night::date,
                       CASE WHEN b."Status" = 'Cancelled' THEN 'Cancelled' ELSE 'Active' END
                FROM "Bookings" b
                CROSS JOIN LATERAL generate_series(
                    b."CheckIn"::timestamp, (b."CheckOut" - 1)::timestamp, interval '1 day') AS night;
                """);

            migrationBuilder.CreateIndex(
                name: "UX_BookingNights_Room_Night_Active",
                table: "BookingNights",
                columns: new[] { "RoomId", "NightDate" },
                unique: true,
                filter: "\"Status\" <> 'Cancelled'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingNights");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_RoomId",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_RoomId_CheckIn_CheckOut",
                table: "Bookings",
                columns: new[] { "RoomId", "CheckIn", "CheckOut" });
        }
    }
}
