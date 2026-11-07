using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobScrape.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "browser_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    source_key = table.Column<string>(type: "TEXT", nullable: false),
                    engine = table.Column<string>(type: "TEXT", nullable: false),
                    channel = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    cooldown_until = table.Column<long>(type: "INTEGER", nullable: true),
                    consecutive_challenges = table.Column<int>(type: "INTEGER", nullable: false),
                    last_used_at = table.Column<long>(type: "INTEGER", nullable: true),
                    last_verified_at = table.Column<long>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_browser_profiles", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                columns: table => new
                {
                    owner_key = table.Column<string>(type: "TEXT", nullable: false),
                    endpoint = table.Column<string>(type: "TEXT", nullable: false),
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    request_hash = table.Column<string>(type: "TEXT", nullable: false),
                    status_code = table.Column<int>(type: "INTEGER", nullable: false),
                    content_type = table.Column<string>(type: "TEXT", nullable: false),
                    body = table.Column<byte[]>(type: "BLOB", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_idempotency_keys",
                        x => new
                        {
                            x.owner_key,
                            x.endpoint,
                            x.key,
                        }
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "job_postings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_key = table.Column<string>(type: "TEXT", nullable: false),
                    external_job_id = table.Column<string>(type: "TEXT", nullable: false),
                    canonical_url = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false),
                    company = table.Column<string>(type: "TEXT", nullable: true),
                    company_url = table.Column<string>(type: "TEXT", nullable: true),
                    location = table.Column<string>(type: "TEXT", nullable: true),
                    experience_raw = table.Column<string>(type: "TEXT", nullable: true),
                    experience_min_years = table.Column<int>(type: "INTEGER", nullable: true),
                    experience_max_years = table.Column<int>(type: "INTEGER", nullable: true),
                    salary_raw = table.Column<string>(type: "TEXT", nullable: true),
                    salary = table.Column<string>(type: "TEXT", nullable: true),
                    posted_raw = table.Column<string>(type: "TEXT", nullable: true),
                    posted_at = table.Column<long>(type: "INTEGER", nullable: true),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    snippet = table.Column<string>(type: "TEXT", nullable: true),
                    skills = table.Column<string>(type: "TEXT", nullable: false),
                    first_seen_at = table.Column<long>(type: "INTEGER", nullable: false),
                    last_seen_at = table.Column<long>(type: "INTEGER", nullable: false),
                    content_hash = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    quality_issues = table.Column<int>(type: "INTEGER", nullable: false),
                    apply_url = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_postings", x => x.id);
                }
            );

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
                name: "search_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    source_key = table.Column<string>(type: "TEXT", nullable: false),
                    criteria = table.Column<string>(type: "TEXT", nullable: false),
                    limits = table.Column<string>(type: "TEXT", nullable: false),
                    enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_search_profiles", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "source_circuit_breakers",
                columns: table => new
                {
                    source_key = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    open_until = table.Column<long>(type: "INTEGER", nullable: true),
                    consecutive_failures = table.Column<int>(type: "INTEGER", nullable: false),
                    recent_failures = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_circuit_breakers", x => x.source_key);
                }
            );

            migrationBuilder.CreateTable(
                name: "source_quotas",
                columns: table => new
                {
                    source_key = table.Column<string>(type: "TEXT", nullable: false),
                    quota_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    pages_fetched = table.Column<int>(type: "INTEGER", nullable: false),
                    details_fetched = table.Column<int>(type: "INTEGER", nullable: false),
                    runs_started = table.Column<int>(type: "INTEGER", nullable: false),
                    challenges = table.Column<int>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_quotas", x => new { x.source_key, x.quota_date });
                }
            );

            migrationBuilder.CreateTable(
                name: "usage_quotas",
                columns: table => new
                {
                    scope = table.Column<string>(type: "TEXT", nullable: false),
                    limit = table.Column<int>(type: "INTEGER", nullable: false),
                    window_ticks = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_quotas", x => x.scope);
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
                    token_version = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    role_ids = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "scrape_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    profile_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    source_key = table.Column<string>(type: "TEXT", nullable: false),
                    limits = table.Column<string>(type: "TEXT", nullable: false),
                    companies = table.Column<string>(type: "TEXT", nullable: false),
                    stage = table.Column<string>(type: "TEXT", nullable: false),
                    stats = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<long>(type: "INTEGER", nullable: false),
                    started_at = table.Column<long>(type: "INTEGER", nullable: true),
                    finished_at = table.Column<long>(type: "INTEGER", nullable: true),
                    pause_reason = table.Column<string>(type: "TEXT", nullable: true),
                    retry_at = table.Column<long>(type: "INTEGER", nullable: true),
                    failure_reason = table.Column<string>(type: "TEXT", nullable: true),
                    resume_stage = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scrape_runs", x => x.id);
                    table.ForeignKey(
                        name: "FK_scrape_runs_search_profiles_profile_id",
                        column: x => x.profile_id,
                        principalTable: "search_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "scrape_run_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    pages_completed = table.Column<int>(type: "INTEGER", nullable: false),
                    next_cursor = table.Column<string>(type: "TEXT", nullable: true),
                    seen_external_ids = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<long>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scrape_run_progress", x => x.id);
                    table.ForeignKey(
                        name: "FK_scrape_run_progress_scrape_runs_id",
                        column: x => x.id,
                        principalTable: "scrape_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_browser_profiles_name",
                table: "browser_profiles",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_company",
                table: "job_postings",
                column: "company"
            );

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_last_seen_at",
                table: "job_postings",
                column: "last_seen_at"
            );

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_posted_at",
                table: "job_postings",
                column: "posted_at"
            );

            migrationBuilder.CreateIndex(
                name: "IX_job_postings_source_key_external_job_id",
                table: "job_postings",
                columns: new[] { "source_key", "external_job_id" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_roles_name",
                table: "roles",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_scrape_runs_created_at",
                table: "scrape_runs",
                column: "created_at"
            );

            migrationBuilder.CreateIndex(
                name: "IX_scrape_runs_profile_id",
                table: "scrape_runs",
                column: "profile_id"
            );

            migrationBuilder.CreateIndex(
                name: "IX_search_profiles_name",
                table: "search_profiles",
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
            migrationBuilder.DropTable(name: "browser_profiles");

            migrationBuilder.DropTable(name: "idempotency_keys");

            migrationBuilder.DropTable(name: "job_postings");

            migrationBuilder.DropTable(name: "roles");

            migrationBuilder.DropTable(name: "scrape_run_progress");

            migrationBuilder.DropTable(name: "source_circuit_breakers");

            migrationBuilder.DropTable(name: "source_quotas");

            migrationBuilder.DropTable(name: "usage_quotas");

            migrationBuilder.DropTable(name: "users");

            migrationBuilder.DropTable(name: "scrape_runs");

            migrationBuilder.DropTable(name: "search_profiles");
        }
    }
}
