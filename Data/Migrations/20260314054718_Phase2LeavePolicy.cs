using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2LeavePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfDeath",
                table: "LeaveRequests",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeceasedName",
                table: "LeaveRequests",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeceasedRelationship",
                table: "LeaveRequests",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupportingDocumentRequired",
                table: "LeaveRequests",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "AspNetUsers",
                type: "longtext",
                nullable: false);

            migrationBuilder.CreateTable(
                name: "CompensatoryOffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeId = table.Column<string>(type: "varchar(255)", nullable: false),
                    DateEarned = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsUsed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Reason = table.Column<string>(type: "longtext", nullable: false),
                    ApprovedById = table.Column<string>(type: "varchar(255)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompensatoryOffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompensatoryOffs_AspNetUsers_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CompensatoryOffs_AspNetUsers_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CompensatoryOffs_ApprovedById",
                table: "CompensatoryOffs",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_CompensatoryOffs_EmployeeId",
                table: "CompensatoryOffs",
                column: "EmployeeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompensatoryOffs");

            migrationBuilder.DropColumn(
                name: "DateOfDeath",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "DeceasedName",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "DeceasedRelationship",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "SupportingDocumentRequired",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "AspNetUsers");
        }
    }
}
