using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HouseholdFarm.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_season_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_season_summaries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_usages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    feature = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    usage_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_usages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_claims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                });

            migrationBuilder.CreateTable(
                name: "asp_net_user_tokens",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                });

            migrationBuilder.CreateTable(
                name: "asp_net_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: true),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    can_manage_stock = table.Column<bool>(type: "boolean", nullable: false),
                    can_manage_finance = table.Column<bool>(type: "boolean", nullable: false),
                    can_close_season = table.Column<bool>(type: "boolean", nullable: false),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_users", x => x.id);
                    table.CheckConstraint("ck_asp_net_users_extra_permissions_member_only", "role = 'Member' OR NOT (can_manage_stock OR can_manage_finance OR can_close_season)");
                    table.CheckConstraint("ck_asp_net_users_role_household", "(role = 'SystemAdmin') = (household_id IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "households",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    production_address = table.Column<string>(type: "text", nullable: false),
                    contact_info = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    allow_negative_stock = table.Column<bool>(type: "boolean", nullable: false),
                    allow_overlapping_seasons = table.Column<bool>(type: "boolean", nullable: false),
                    sao_area_m2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_households", x => x.id);
                    table.ForeignKey(
                        name: "fk_households_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    replaced_by_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_refresh_tokens_replaced_by_id",
                        column: x => x.replaced_by_id,
                        principalTable: "refresh_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_asp_net_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_audit_logs_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "crops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    group = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    group_other = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    yield_unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reference_info = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_crops", x => x.id);
                    table.ForeignKey(
                        name: "fk_crops_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_crops_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_crops_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                    table.ForeignKey(
                        name: "fk_customers_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customers_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_customers_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    area_m2 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    location_description = table.Column<string>(type: "text", nullable: true),
                    soil_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    soil_type_other = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    water_source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    water_source_other = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plots", x => x.id);
                    table.CheckConstraint("ck_plots_area_m2", "area_m2 > 0");
                    table.ForeignKey(
                        name: "fk_plots_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plots_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_plots_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_exports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: true),
                    to_date = table.Column<DateOnly>(type: "date", nullable: true),
                    filters = table.Column<string>(type: "jsonb", nullable: false),
                    exported_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    exported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_exports", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_exports_asp_net_users_exported_by_id",
                        column: x => x.exported_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_report_exports_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "supplies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    group = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    min_stock_level = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    reference_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    average_unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplies", x => x.id);
                    table.CheckConstraint("ck_supplies_average_unit_cost", "average_unit_cost >= 0");
                    table.CheckConstraint("ck_supplies_min_stock_level", "min_stock_level >= 0");
                    table.ForeignKey(
                        name: "fk_supplies_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplies_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_supplies_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    crop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_harvest_date = table.Column<DateOnly>(type: "date", nullable: false),
                    target_yield = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancel_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seasons", x => x.id);
                    table.CheckConstraint("ck_seasons_expected_harvest_date", "expected_harvest_date >= start_date");
                    table.CheckConstraint("ck_seasons_target_yield", "target_yield > 0");
                    table.ForeignKey(
                        name: "fk_seasons_asp_net_users_cancelled_by_id",
                        column: x => x.cancelled_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_seasons_asp_net_users_closed_by_id",
                        column: x => x.closed_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_seasons_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_seasons_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_seasons_crops_crop_id",
                        column: x => x.crop_id,
                        principalTable: "crops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_seasons_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_seasons_plots_plot_id",
                        column: x => x.plot_id,
                        principalTable: "plots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category_other = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    expense_date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    voided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_expenses", x => x.id);
                    table.CheckConstraint("ck_expenses_amount", "amount > 0");
                    table.ForeignKey(
                        name: "fk_expenses_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expenses_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expenses_asp_net_users_voided_by_id",
                        column: x => x.voided_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expenses_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_expenses_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "farm_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type_other = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    performed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                    abnormal_note = table.Column<string>(type: "text", nullable: true),
                    field_note = table.Column<string>(type: "text", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recurrence_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_farm_tasks", x => x.id);
                    table.CheckConstraint("ck_farm_tasks_actual_duration_minutes", "actual_duration_minutes IS NULL OR actual_duration_minutes > 0");
                    table.CheckConstraint("ck_farm_tasks_planned_duration_minutes", "planned_duration_minutes IS NULL OR planned_duration_minutes > 0");
                    table.ForeignKey(
                        name: "fk_farm_tasks_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_tasks_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_tasks_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_tasks_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_tasks_users_assignee_id",
                        column: x => x.assignee_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "harvest_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    harvest_date = table.Column<DateOnly>(type: "date", nullable: false),
                    quality_grade = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    voided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_harvest_records", x => x.id);
                    table.CheckConstraint("ck_harvest_records_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_harvest_records_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_harvest_records_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_harvest_records_asp_net_users_voided_by_id",
                        column: x => x.voided_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_harvest_records_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_harvest_records_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "produce_stock_adjustments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quality_grade = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    adjustment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    reason_other = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    voided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produce_stock_adjustments", x => x.id);
                    table.CheckConstraint("ck_produce_stock_adjustments_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_produce_stock_adjustments_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_produce_stock_adjustments_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_produce_stock_adjustments_asp_net_users_voided_by_id",
                        column: x => x.voided_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_produce_stock_adjustments_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_produce_stock_adjustments_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    channel_other = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    quality_grade = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    sale_date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    voided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales", x => x.id);
                    table.CheckConstraint("ck_sales_customer_or_channel", "customer_id IS NOT NULL OR channel IS NOT NULL");
                    table.CheckConstraint("ck_sales_quantity", "quantity > 0");
                    table.CheckConstraint("ck_sales_unit_price", "unit_price >= 0");
                    table.ForeignKey(
                        name: "fk_sales_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_asp_net_users_voided_by_id",
                        column: x => x.voided_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "season_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_season_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_season_assignments_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_season_assignments_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_season_assignments_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_season_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "season_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_season_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_season_notes_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_season_notes_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_season_notes_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_season_notes_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "season_supply_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supply_id = table.Column<Guid>(type: "uuid", nullable: false),
                    planned_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_season_supply_plans", x => x.id);
                    table.CheckConstraint("ck_season_supply_plans_planned_quantity", "planned_quantity > 0");
                    table.ForeignKey(
                        name: "fk_season_supply_plans_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_season_supply_plans_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_season_supply_plans_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_season_supply_plans_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_season_supply_plans_supplies_supply_id",
                        column: x => x.supply_id,
                        principalTable: "supplies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supply_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    average_unit_cost_before = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    total_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    voided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_transactions", x => x.id);
                    table.CheckConstraint("ck_stock_transactions_adjustment", "type <> 'Adjustment' OR (quantity <> 0 AND season_id IS NULL AND reason IS NOT NULL)");
                    table.CheckConstraint("ck_stock_transactions_in", "type <> 'In' OR (quantity > 0 AND season_id IS NULL)");
                    table.CheckConstraint("ck_stock_transactions_out", "type <> 'Out' OR (quantity < 0 AND season_id IS NOT NULL)");
                    table.CheckConstraint("ck_stock_transactions_total_cost", "total_cost >= 0");
                    table.CheckConstraint("ck_stock_transactions_unit_cost", "unit_cost >= 0");
                    table.ForeignKey(
                        name: "fk_stock_transactions_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transactions_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transactions_asp_net_users_voided_by_id",
                        column: x => x.voided_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transactions_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transactions_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transactions_supplies_supply_id",
                        column: x => x.supply_id,
                        principalTable: "supplies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    voided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    voided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    void_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.CheckConstraint("ck_payments_amount", "amount > 0");
                    table.ForeignKey(
                        name: "fk_payments_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_asp_net_users_voided_by_id",
                        column: x => x.voided_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_payments_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_harvest_links",
                columns: table => new
                {
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    harvest_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_harvest_links", x => new { x.sale_id, x.harvest_record_id });
                    table.ForeignKey(
                        name: "fk_sale_harvest_links_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_harvest_links_harvest_records_harvest_record_id",
                        column: x => x.harvest_record_id,
                        principalTable: "harvest_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_harvest_links_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_harvest_links_sales_sale_id",
                        column: x => x.sale_id,
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "farm_task_supply_usages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    farm_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supply_id = table.Column<Guid>(type: "uuid", nullable: false),
                    planned_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    actual_quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                    stock_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_farm_task_supply_usages", x => x.id);
                    table.CheckConstraint("ck_farm_task_supply_usages_actual_quantity", "actual_quantity IS NULL OR actual_quantity > 0");
                    table.CheckConstraint("ck_farm_task_supply_usages_planned_quantity", "planned_quantity IS NULL OR planned_quantity > 0");
                    table.ForeignKey(
                        name: "fk_farm_task_supply_usages_asp_net_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_task_supply_usages_asp_net_users_updated_by_id",
                        column: x => x.updated_by_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_task_supply_usages_farm_tasks_farm_task_id",
                        column: x => x.farm_task_id,
                        principalTable: "farm_tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_task_supply_usages_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_task_supply_usages_stock_transactions_stock_transactio",
                        column: x => x.stock_transaction_id,
                        principalTable: "stock_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_farm_task_supply_usages_supplies_supply_id",
                        column: x => x.supply_id,
                        principalTable: "supplies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_season_summaries_created_by_id",
                table: "ai_season_summaries",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_season_summaries_household_id",
                table: "ai_season_summaries",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_season_summaries_season_id",
                table: "ai_season_summaries",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usages_household_id_usage_date",
                table: "ai_usages",
                columns: new[] { "household_id", "usage_date" });

            migrationBuilder.CreateIndex(
                name: "ix_ai_usages_user_id",
                table: "ai_usages",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "asp_net_user_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "asp_net_user_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "asp_net_users",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_household_id",
                table: "asp_net_users",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "asp_net_users",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_id",
                table: "audit_logs",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_type_entity_id",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_household_id_occurred_at",
                table: "audit_logs",
                columns: new[] { "household_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_crops_created_by_id",
                table: "crops",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_crops_household_id_name",
                table: "crops",
                columns: new[] { "household_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_crops_updated_by_id",
                table: "crops",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_created_by_id",
                table: "customers",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_household_id",
                table: "customers",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_updated_by_id",
                table: "customers",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_created_by_id",
                table: "expenses",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_household_id_request_id",
                table: "expenses",
                columns: new[] { "household_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_expenses_season_id",
                table: "expenses",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_updated_by_id",
                table: "expenses",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_expenses_voided_by_id",
                table: "expenses",
                column: "voided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_farm_task_supply_usages_created_by_id",
                table: "farm_task_supply_usages",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_farm_task_supply_usages_farm_task_id_supply_id",
                table: "farm_task_supply_usages",
                columns: new[] { "farm_task_id", "supply_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_farm_task_supply_usages_household_id",
                table: "farm_task_supply_usages",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ix_farm_task_supply_usages_stock_transaction_id",
                table: "farm_task_supply_usages",
                column: "stock_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_farm_task_supply_usages_supply_id",
                table: "farm_task_supply_usages",
                column: "supply_id");

            migrationBuilder.CreateIndex(
                name: "ix_farm_task_supply_usages_updated_by_id",
                table: "farm_task_supply_usages",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_farm_tasks_assignee_id",
                table: "farm_tasks",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_farm_tasks_created_by_id",
                table: "farm_tasks",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_farm_tasks_household_id",
                table: "farm_tasks",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ix_farm_tasks_season_id",
                table: "farm_tasks",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_farm_tasks_updated_by_id",
                table: "farm_tasks",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_harvest_records_created_by_id",
                table: "harvest_records",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_harvest_records_household_id_request_id",
                table: "harvest_records",
                columns: new[] { "household_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_harvest_records_season_id",
                table: "harvest_records",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_harvest_records_updated_by_id",
                table: "harvest_records",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_harvest_records_voided_by_id",
                table: "harvest_records",
                column: "voided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_households_code",
                table: "households",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_households_updated_by_id",
                table: "households",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_created_by_id",
                table: "payments",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_household_id_request_id",
                table: "payments",
                columns: new[] { "household_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_sale_id",
                table: "payments",
                column: "sale_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_updated_by_id",
                table: "payments",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_voided_by_id",
                table: "payments",
                column: "voided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_plots_created_by_id",
                table: "plots",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_plots_household_id_code",
                table: "plots",
                columns: new[] { "household_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plots_updated_by_id",
                table: "plots",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_produce_stock_adjustments_created_by_id",
                table: "produce_stock_adjustments",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_produce_stock_adjustments_household_id_request_id",
                table: "produce_stock_adjustments",
                columns: new[] { "household_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_produce_stock_adjustments_season_id",
                table: "produce_stock_adjustments",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_produce_stock_adjustments_updated_by_id",
                table: "produce_stock_adjustments",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_produce_stock_adjustments_voided_by_id",
                table: "produce_stock_adjustments",
                column: "voided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_replaced_by_id",
                table: "refresh_tokens",
                column: "replaced_by_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_exports_exported_by_id",
                table: "report_exports",
                column: "exported_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_exports_household_id",
                table: "report_exports",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_harvest_links_created_by_id",
                table: "sale_harvest_links",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_harvest_links_harvest_record_id",
                table: "sale_harvest_links",
                column: "harvest_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_harvest_links_household_id",
                table: "sale_harvest_links",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_created_by_id",
                table: "sales",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_customer_id",
                table: "sales",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_household_id_request_id",
                table: "sales",
                columns: new[] { "household_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_season_id",
                table: "sales",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_updated_by_id",
                table: "sales",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_voided_by_id",
                table: "sales",
                column: "voided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_season_assignments_created_by_id",
                table: "season_assignments",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_season_assignments_household_id",
                table: "season_assignments",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ix_season_assignments_season_id_user_id",
                table: "season_assignments",
                columns: new[] { "season_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_season_assignments_user_id",
                table: "season_assignments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_season_notes_created_by_id",
                table: "season_notes",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_season_notes_household_id",
                table: "season_notes",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ix_season_notes_season_id",
                table: "season_notes",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_season_notes_updated_by_id",
                table: "season_notes",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_season_supply_plans_created_by_id",
                table: "season_supply_plans",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_season_supply_plans_household_id",
                table: "season_supply_plans",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ix_season_supply_plans_season_id_supply_id",
                table: "season_supply_plans",
                columns: new[] { "season_id", "supply_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_season_supply_plans_supply_id",
                table: "season_supply_plans",
                column: "supply_id");

            migrationBuilder.CreateIndex(
                name: "ix_season_supply_plans_updated_by_id",
                table: "season_supply_plans",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_seasons_cancelled_by_id",
                table: "seasons",
                column: "cancelled_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_seasons_closed_by_id",
                table: "seasons",
                column: "closed_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_seasons_created_by_id",
                table: "seasons",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_seasons_crop_id",
                table: "seasons",
                column: "crop_id");

            migrationBuilder.CreateIndex(
                name: "ix_seasons_household_id",
                table: "seasons",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "ix_seasons_plot_id",
                table: "seasons",
                column: "plot_id");

            migrationBuilder.CreateIndex(
                name: "ix_seasons_updated_by_id",
                table: "seasons",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_created_by_id",
                table: "stock_transactions",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_household_id_request_id",
                table: "stock_transactions",
                columns: new[] { "household_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_season_id",
                table: "stock_transactions",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_supply_id",
                table: "stock_transactions",
                column: "supply_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_updated_by_id",
                table: "stock_transactions",
                column: "updated_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_voided_by_id",
                table: "stock_transactions",
                column: "voided_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplies_created_by_id",
                table: "supplies",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_supplies_household_id_code",
                table: "supplies",
                columns: new[] { "household_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_supplies_updated_by_id",
                table: "supplies",
                column: "updated_by_id");

            migrationBuilder.AddForeignKey(
                name: "fk_ai_season_summaries_asp_net_users_created_by_id",
                table: "ai_season_summaries",
                column: "created_by_id",
                principalTable: "asp_net_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_ai_season_summaries_households_household_id",
                table: "ai_season_summaries",
                column: "household_id",
                principalTable: "households",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_ai_season_summaries_seasons_season_id",
                table: "ai_season_summaries",
                column: "season_id",
                principalTable: "seasons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_ai_usages_households_household_id",
                table: "ai_usages",
                column: "household_id",
                principalTable: "households",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_ai_usages_users_user_id",
                table: "ai_usages",
                column: "user_id",
                principalTable: "asp_net_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_user_claims_asp_net_users_user_id",
                table: "asp_net_user_claims",
                column: "user_id",
                principalTable: "asp_net_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_user_logins_asp_net_users_user_id",
                table: "asp_net_user_logins",
                column: "user_id",
                principalTable: "asp_net_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                table: "asp_net_user_tokens",
                column: "user_id",
                principalTable: "asp_net_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_asp_net_users_households_household_id",
                table: "asp_net_users",
                column: "household_id",
                principalTable: "households",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_households_asp_net_users_updated_by_id",
                table: "households");

            migrationBuilder.DropTable(
                name: "ai_season_summaries");

            migrationBuilder.DropTable(
                name: "ai_usages");

            migrationBuilder.DropTable(
                name: "asp_net_user_claims");

            migrationBuilder.DropTable(
                name: "asp_net_user_logins");

            migrationBuilder.DropTable(
                name: "asp_net_user_tokens");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "farm_task_supply_usages");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "produce_stock_adjustments");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "report_exports");

            migrationBuilder.DropTable(
                name: "sale_harvest_links");

            migrationBuilder.DropTable(
                name: "season_assignments");

            migrationBuilder.DropTable(
                name: "season_notes");

            migrationBuilder.DropTable(
                name: "season_supply_plans");

            migrationBuilder.DropTable(
                name: "farm_tasks");

            migrationBuilder.DropTable(
                name: "stock_transactions");

            migrationBuilder.DropTable(
                name: "harvest_records");

            migrationBuilder.DropTable(
                name: "sales");

            migrationBuilder.DropTable(
                name: "supplies");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "seasons");

            migrationBuilder.DropTable(
                name: "crops");

            migrationBuilder.DropTable(
                name: "plots");

            migrationBuilder.DropTable(
                name: "asp_net_users");

            migrationBuilder.DropTable(
                name: "households");
        }
    }
}
