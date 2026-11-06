using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobScrape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    permission_keys = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    email = table.Column<string>(
                        type: "TEXT",
                        nullable: false,
                        collation: "NOCASE"
                    ),
                    password_hash = table.Column<string>(type: "TEXT", nullable: false),
                    must_change_password = table.Column<bool>(type: "INTEGER", nullable: false),
                    failed_attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    locked_until_utc = table.Column<long>(type: "INTEGER", nullable: true),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    role_ids = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                table: "roles",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "roles");

            migrationBuilder.DropTable(name: "users");
        }
    }
}
