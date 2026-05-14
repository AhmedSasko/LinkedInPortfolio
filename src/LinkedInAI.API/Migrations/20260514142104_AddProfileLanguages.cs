using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkedInAI.API.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileLanguages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProfileCertifications_Profiles_ProfileId1",
                table: "ProfileCertifications");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileEducations_Profiles_ProfileId1",
                table: "ProfileEducations");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileExperiences_Profiles_ProfileId1",
                table: "ProfileExperiences");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileProjects_Profiles_ProfileId1",
                table: "ProfileProjects");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileSkills_Profiles_ProfileId1",
                table: "ProfileSkills");

            migrationBuilder.DropIndex(
                name: "IX_ProfileSkills_ProfileId1",
                table: "ProfileSkills");

            migrationBuilder.DropIndex(
                name: "IX_ProfileProjects_ProfileId1",
                table: "ProfileProjects");

            migrationBuilder.DropIndex(
                name: "IX_ProfileExperiences_ProfileId1",
                table: "ProfileExperiences");

            migrationBuilder.DropIndex(
                name: "IX_ProfileEducations_ProfileId1",
                table: "ProfileEducations");

            migrationBuilder.DropIndex(
                name: "IX_ProfileCertifications_ProfileId1",
                table: "ProfileCertifications");

            migrationBuilder.DropColumn(
                name: "ProfileId1",
                table: "ProfileSkills");

            migrationBuilder.DropColumn(
                name: "ProfileId1",
                table: "ProfileProjects");

            migrationBuilder.DropColumn(
                name: "ProfileId1",
                table: "ProfileExperiences");

            migrationBuilder.DropColumn(
                name: "ProfileId1",
                table: "ProfileEducations");

            migrationBuilder.DropColumn(
                name: "ProfileId1",
                table: "ProfileCertifications");

            migrationBuilder.CreateTable(
                name: "ProfileLanguages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Proficiency = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileLanguages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProfileLanguages_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileLanguages_ProfileId",
                table: "ProfileLanguages",
                column: "ProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProfileLanguages");

            migrationBuilder.AddColumn<int>(
                name: "ProfileId1",
                table: "ProfileSkills",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId1",
                table: "ProfileProjects",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId1",
                table: "ProfileExperiences",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId1",
                table: "ProfileEducations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId1",
                table: "ProfileCertifications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProfileSkills_ProfileId1",
                table: "ProfileSkills",
                column: "ProfileId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileProjects_ProfileId1",
                table: "ProfileProjects",
                column: "ProfileId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileExperiences_ProfileId1",
                table: "ProfileExperiences",
                column: "ProfileId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileEducations_ProfileId1",
                table: "ProfileEducations",
                column: "ProfileId1");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileCertifications_ProfileId1",
                table: "ProfileCertifications",
                column: "ProfileId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileCertifications_Profiles_ProfileId1",
                table: "ProfileCertifications",
                column: "ProfileId1",
                principalTable: "Profiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileEducations_Profiles_ProfileId1",
                table: "ProfileEducations",
                column: "ProfileId1",
                principalTable: "Profiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileExperiences_Profiles_ProfileId1",
                table: "ProfileExperiences",
                column: "ProfileId1",
                principalTable: "Profiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileProjects_Profiles_ProfileId1",
                table: "ProfileProjects",
                column: "ProfileId1",
                principalTable: "Profiles",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileSkills_Profiles_ProfileId1",
                table: "ProfileSkills",
                column: "ProfileId1",
                principalTable: "Profiles",
                principalColumn: "Id");
        }
    }
}
