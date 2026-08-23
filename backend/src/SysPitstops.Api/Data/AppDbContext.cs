using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<ServiceOrderItem> ServiceOrderItems => Set<ServiceOrderItem>();
    public DbSet<ServiceOrderStatusHistory> ServiceOrderStatusHistory => Set<ServiceOrderStatusHistory>();
    public DbSet<ServiceOrderMedia> ServiceOrderMedia => Set<ServiceOrderMedia>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // The Postgres enum types themselves are declared through MapEnum in
        // Program.cs, which also maps the CLR properties onto them.
        b.HasPostgresExtension("pgcrypto");

        b.HasSequence<int>("service_order_number_seq").StartsAt(1);

        b.Entity<Workshop>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        });

        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.WorkshopId, x.Email }).IsUnique().HasDatabaseName("uq_users_email");
            e.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Customer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.WorkshopId, x.Phone }).HasDatabaseName("ix_customers_phone");
            e.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Vehicle>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.WorkshopId, x.Plate }).IsUnique().HasDatabaseName("uq_vehicles_plate");
            e.HasIndex(x => x.OwnerId).HasDatabaseName("ix_vehicles_owner");
            e.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Owner).WithMany(c => c.Vehicles).HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Part>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Unit).HasDefaultValue("UN");
            e.Property(x => x.SalePrice).HasPrecision(12, 2);
            e.Property(x => x.CostPrice).HasPrecision(12, 2).HasDefaultValue(0m);
            e.Property(x => x.QuantityOnHand).HasPrecision(10, 3).HasDefaultValue(0m);
            e.Property(x => x.MinQuantity).HasPrecision(10, 3).HasDefaultValue(0m);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.WorkshopId, x.Sku }).IsUnique().HasDatabaseName("uq_parts_sku");
            e.HasIndex(x => x.WorkshopId).HasDatabaseName("ix_parts_low_stock").HasFilter("quantity_on_hand <= min_quantity");
            e.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_parts_sale_price", "sale_price >= 0");
                t.HasCheckConstraint("ck_parts_cost_price", "cost_price >= 0");
            });
        });

        b.Entity<ServiceOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Number).HasDefaultValueSql("nextval('service_order_number_seq')");
            e.Property(x => x.Status).HasDefaultValue(ServiceOrderStatus.Requested);
            e.Property(x => x.DiscountAmount).HasPrecision(12, 2).HasDefaultValue(0m);
            e.Property(x => x.OpenedAt).HasDefaultValueSql("now()");
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.WorkshopId, x.Number }).IsUnique().HasDatabaseName("uq_service_orders_number");
            e.HasIndex(x => new { x.WorkshopId, x.Status }).HasDatabaseName("ix_so_status");
            e.HasIndex(x => new { x.VehicleId, x.OpenedAt }).IsDescending(false, true).HasDatabaseName("ix_so_vehicle");
            e.HasIndex(x => new { x.CustomerId, x.OpenedAt }).IsDescending(false, true).HasDatabaseName("ix_so_customer");
            e.HasIndex(x => new { x.MechanicId, x.Status }).HasDatabaseName("ix_so_mechanic");
            e.HasIndex(x => new { x.WorkshopId, x.OpenedAt }).HasDatabaseName("ix_so_opened_at");
            e.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Mechanic).WithMany().HasForeignKey(x => x.MechanicId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_so_discount", "discount_amount >= 0"));
        });

        b.Entity<ServiceOrderItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Quantity).HasPrecision(10, 3);
            e.Property(x => x.UnitPrice).HasPrecision(12, 2);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.ServiceOrderId).HasDatabaseName("ix_soi_order");
            e.HasOne(x => x.ServiceOrder).WithMany(o => o.Items).HasForeignKey(x => x.ServiceOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Part).WithMany().HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_item_part_required",
                    "(item_type = 'PART' AND part_id IS NOT NULL) OR (item_type = 'SERVICE' AND part_id IS NULL)");
                t.HasCheckConstraint("ck_item_quantity", "quantity > 0");
                t.HasCheckConstraint("ck_item_unit_price", "unit_price >= 0");
            });
        });

        b.Entity<ServiceOrderStatusHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.ChangedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.ServiceOrderId, x.ChangedAt }).HasDatabaseName("ix_sosh_order");
            e.HasOne(x => x.ServiceOrder).WithMany(o => o.StatusHistory).HasForeignKey(x => x.ServiceOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedBy).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ServiceOrderMedia>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UploadedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.ServiceOrderId).HasDatabaseName("ix_som_order");
            e.HasOne(x => x.ServiceOrder).WithMany(o => o.Media).HasForeignKey(x => x.ServiceOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Quote>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Status).HasDefaultValue(QuoteStatus.Sent);
            e.Property(x => x.ItemsSnapshot).HasColumnType("jsonb");
            e.Property(x => x.TotalAmount).HasPrecision(12, 2);
            e.Property(x => x.SentAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.PublicToken).IsUnique().HasDatabaseName("uq_quotes_token");
            e.HasIndex(x => new { x.ServiceOrderId, x.SentAt }).IsDescending(false, true).HasDatabaseName("ix_quotes_order");
            e.HasOne(x => x.ServiceOrder).WithMany(o => o.Quotes).HasForeignKey(x => x.ServiceOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.SentByUser).WithMany().HasForeignKey(x => x.SentBy).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_quotes_total", "total_amount >= 0"));
        });

        b.Entity<StockMovement>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Quantity).HasPrecision(10, 3);
            e.Property(x => x.UnitCost).HasPrecision(12, 2);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.PartId, x.CreatedAt }).IsDescending(false, true).HasDatabaseName("ix_sm_part");
            e.HasIndex(x => x.ServiceOrderId).HasDatabaseName("ix_sm_order");
            e.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Part).WithMany().HasForeignKey(x => x.PartId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ServiceOrder).WithMany().HasForeignKey(x => x.ServiceOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_sm_quantity", "quantity > 0"));
        });
    }
}
