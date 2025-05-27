using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiveService.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Streams",
                columns: table => new
                {
                    CloudinaryStreamId = table.Column<string>(type: "text", maxLength: 0, nullable: false),
                    Name = table.Column<string>(type: "text", maxLength: 0, nullable: false),
                    ArchivePublicId = table.Column<string>(type: "text", maxLength: 0, nullable: false),
                    AuthorId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Streams", x => x.CloudinaryStreamId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Streams_AuthorId",
                table: "Streams",
                column: "AuthorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Streams");
        }
    }
}
