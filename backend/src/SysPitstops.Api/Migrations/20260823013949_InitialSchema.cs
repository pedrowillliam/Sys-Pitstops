using System;
using Microsoft.EntityFrameworkCore.Migrations;
using SysPitstops.Api.Domain;

#nullable disable

namespace SysPitstops.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:item_type", "PART,SERVICE")
                .Annotation("Npgsql:Enum:movement_type", "ADJUSTMENT,IN,OUT")
                .Annotation("Npgsql:Enum:quote_status", "APPROVED,EXPIRED,REJECTED,SENT")
                .Annotation("Npgsql:Enum:service_order_status", "AWAITING_APPROVAL,CANCELED,CONFIRMED,DELIVERED,IN_PROGRESS,IN_YARD,READY,REQUESTED")
                .Annotation("Npgsql:Enum:user_role", "ADMIN,ATTENDANT,MECHANIC")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateSequence<int>(
                name: "service_order_number_seq");

            migrationBuilder.CreateTable(
                name: "workshops",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    document = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "text", nullable: true),
                    address = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workshops", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    workshop_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    phone = table.Column<string>(type: "text", nullable: false),
                    document = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                    table.ForeignKey(
                        name: "fk_customers_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalTable: "workshops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    workshop_id = table.Column<int>(type: "integer", nullable: false),
                    sku = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    unit = table.Column<string>(type: "text", nullable: false, defaultValue: "UN"),
                    sale_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    cost_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    quantity_on_hand = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false, defaultValue: 0m),
                    min_quantity = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false, defaultValue: 0m),
                    location = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parts", x => x.id);
                    table.CheckConstraint("ck_parts_cost_price", "cost_price >= 0");
                    table.CheckConstraint("ck_parts_sale_price", "sale_price >= 0");
                    table.ForeignKey(
                        name: "fk_parts_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalTable: "workshops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    workshop_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<UserRole>(type: "user_role", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalTable: "workshops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    workshop_id = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate = table.Column<string>(type: "text", nullable: false),
                    brand = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    model_year = table.Column<int>(type: "integer", nullable: true),
                    color = table.Column<string>(type: "text", nullable: true),
                    vin = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicles", x => x.id);
                    table.ForeignKey(
                        name: "fk_vehicles_customers_owner_id",
                        column: x => x.owner_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vehicles_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalTable: "workshops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    workshop_id = table.Column<int>(type: "integer", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false, defaultValueSql: "nextval('service_order_number_seq')"),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mechanic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<ServiceOrderStatus>(type: "service_order_status", nullable: false, defaultValue: ServiceOrderStatus.Requested),
                    mileage = table.Column<int>(type: "integer", nullable: true),
                    reported_issue = table.Column<string>(type: "text", nullable: true),
                    diagnosis = table.Column<string>(type: "text", nullable: true),
                    discount_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                    approval_waived_note = table.Column<string>(type: "text", nullable: true),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_orders", x => x.id);
                    table.CheckConstraint("ck_so_discount", "discount_amount >= 0");
                    table.ForeignKey(
                        name: "fk_service_orders_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_orders_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_orders_users_mechanic_id",
                        column: x => x.mechanic_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_orders_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_orders_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalTable: "workshops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    service_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_token = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<QuoteStatus>(type: "quote_status", nullable: false, defaultValue: QuoteStatus.Sent),
                    items_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    sent_by = table.Column<Guid>(type: "uuid", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotes", x => x.id);
                    table.CheckConstraint("ck_quotes_total", "total_amount >= 0");
                    table.ForeignKey(
                        name: "fk_quotes_service_orders_service_order_id",
                        column: x => x.service_order_id,
                        principalTable: "service_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_quotes_users_sent_by",
                        column: x => x.sent_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    service_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_type = table.Column<ItemType>(type: "item_type", nullable: false),
                    part_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_order_items", x => x.id);
                    table.CheckConstraint("ck_item_part_required", "(item_type = 'PART' AND part_id IS NOT NULL) OR (item_type = 'SERVICE' AND part_id IS NULL)");
                    table.CheckConstraint("ck_item_quantity", "quantity > 0");
                    table.CheckConstraint("ck_item_unit_price", "unit_price >= 0");
                    table.ForeignKey(
                        name: "fk_service_order_items_parts_part_id",
                        column: x => x.part_id,
                        principalTable: "parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_order_items_service_orders_service_order_id",
                        column: x => x.service_order_id,
                        principalTable: "service_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_service_order_items_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_order_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    service_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    caption = table.Column<string>(type: "text", nullable: true),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_order_media", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_order_media_service_orders_service_order_id",
                        column: x => x.service_order_id,
                        principalTable: "service_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_service_order_media_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_order_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    service_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<ServiceOrderStatus>(type: "service_order_status", nullable: true),
                    to_status = table.Column<ServiceOrderStatus>(type: "service_order_status", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_order_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_order_status_history_service_orders_service_order_id",
                        column: x => x.service_order_id,
                        principalTable: "service_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_service_order_status_history_users_changed_by",
                        column: x => x.changed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    workshop_id = table.Column<int>(type: "integer", nullable: false),
                    part_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_type = table.Column<MovementType>(type: "movement_type", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    service_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                    table.CheckConstraint("ck_sm_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_stock_movements_parts_part_id",
                        column: x => x.part_id,
                        principalTable: "parts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_service_orders_service_order_id",
                        column: x => x.service_order_id,
                        principalTable: "service_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalTable: "workshops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_customers_phone",
                table: "customers",
                columns: new[] { "workshop_id", "phone" });

            migrationBuilder.CreateIndex(
                name: "ix_parts_low_stock",
                table: "parts",
                column: "workshop_id",
                filter: "quantity_on_hand <= min_quantity");

            migrationBuilder.CreateIndex(
                name: "uq_parts_sku",
                table: "parts",
                columns: new[] { "workshop_id", "sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quotes_order",
                table: "quotes",
                columns: new[] { "service_order_id", "sent_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_quotes_sent_by",
                table: "quotes",
                column: "sent_by");

            migrationBuilder.CreateIndex(
                name: "uq_quotes_token",
                table: "quotes",
                column: "public_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_order_items_created_by",
                table: "service_order_items",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_service_order_items_part_id",
                table: "service_order_items",
                column: "part_id");

            migrationBuilder.CreateIndex(
                name: "ix_soi_order",
                table: "service_order_items",
                column: "service_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_order_media_uploaded_by",
                table: "service_order_media",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ix_som_order",
                table: "service_order_media",
                column: "service_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_order_status_history_changed_by",
                table: "service_order_status_history",
                column: "changed_by");

            migrationBuilder.CreateIndex(
                name: "ix_sosh_order",
                table: "service_order_status_history",
                columns: new[] { "service_order_id", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_service_orders_created_by",
                table: "service_orders",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_so_customer",
                table: "service_orders",
                columns: new[] { "customer_id", "opened_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_so_mechanic",
                table: "service_orders",
                columns: new[] { "mechanic_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_so_opened_at",
                table: "service_orders",
                columns: new[] { "workshop_id", "opened_at" });

            migrationBuilder.CreateIndex(
                name: "ix_so_status",
                table: "service_orders",
                columns: new[] { "workshop_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_so_vehicle",
                table: "service_orders",
                columns: new[] { "vehicle_id", "opened_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uq_service_orders_number",
                table: "service_orders",
                columns: new[] { "workshop_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sm_order",
                table: "stock_movements",
                column: "service_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_sm_part",
                table: "stock_movements",
                columns: new[] { "part_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_user_id",
                table: "stock_movements",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_workshop_id",
                table: "stock_movements",
                column: "workshop_id");

            migrationBuilder.CreateIndex(
                name: "uq_users_email",
                table: "users",
                columns: new[] { "workshop_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_owner",
                table: "vehicles",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "uq_vehicles_plate",
                table: "vehicles",
                columns: new[] { "workshop_id", "plate" },
                unique: true);

            // Case-insensitive lookup indexes. EF Core cannot express
            // functional indexes, so they match docs/schema.sql here.
            migrationBuilder.Sql(
                "CREATE INDEX ix_customers_name ON customers (workshop_id, lower(name));");
            migrationBuilder.Sql(
                "CREATE INDEX ix_parts_name ON parts (workshop_id, lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quotes");

            migrationBuilder.DropTable(
                name: "service_order_items");

            migrationBuilder.DropTable(
                name: "service_order_media");

            migrationBuilder.DropTable(
                name: "service_order_status_history");

            migrationBuilder.DropTable(
                name: "stock_movements");

            migrationBuilder.DropTable(
                name: "parts");

            migrationBuilder.DropTable(
                name: "service_orders");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropTable(
                name: "workshops");

            migrationBuilder.DropSequence(
                name: "service_order_number_seq");
        }
    }
}
