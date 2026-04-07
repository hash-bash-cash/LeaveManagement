using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateHolidayFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Holidays",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeId",
                table: "Holidays",
                type: "varchar(255)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_DepartmentId",
                table: "Holidays",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_EmployeeId",
                table: "Holidays",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Holidays_AspNetUsers_EmployeeId",
                table: "Holidays",
                column: "EmployeeId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Holidays_Departments_DepartmentId",
                table: "Holidays",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Holidays_AspNetUsers_EmployeeId",
                table: "Holidays");

            migrationBuilder.DropForeignKey(
                name: "FK_Holidays_Departments_DepartmentId",
                table: "Holidays");

            migrationBuilder.DropIndex(
                name: "IX_Holidays_DepartmentId",
                table: "Holidays");

            migrationBuilder.DropIndex(
                name: "IX_Holidays_EmployeeId",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Holidays");
        }
    }
}
