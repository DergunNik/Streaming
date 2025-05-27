using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VoDService.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Videos",
                columns: table => new
                {
                    PublicId = table.Column<string>(type: "text", maxLength: 0, nullable: false),
                    LikeCount = table.Column<int>(type: "integer", nullable: false),
                    DislikeCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Videos", x => x.PublicId);
                });

            migrationBuilder.CreateTable(
                name: "Reactions",
                columns: table => new
                {
                    PublicId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    IsLike = table.Column<bool>(type: "boolean", nullable: false),
                    VideoInfoPublicId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reactions", x => new { x.PublicId, x.UserId });
                    table.ForeignKey(
                        name: "FK_Reactions_Videos_PublicId",
                        column: x => x.PublicId,
                        principalTable: "Videos",
                        principalColumn: "PublicId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reactions_Videos_VideoInfoPublicId",
                        column: x => x.VideoInfoPublicId,
                        principalTable: "Videos",
                        principalColumn: "PublicId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_VideoInfoPublicId",
                table: "Reactions",
                column: "VideoInfoPublicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reactions");

            migrationBuilder.DropTable(
                name: "Videos");
        }
    }
}
