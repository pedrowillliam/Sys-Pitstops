using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;

namespace SysPitstops.Api.Seeding;

/// <summary>
/// Fills an empty database with a workshop that looks like it has been running
/// for six months. Without it the deployed system opens with nothing in it: the
/// board has no cards, the stock is empty, and the revenue chart of the
/// dashboard draws six columns of zero, which is indistinguishable from broken.
/// <para>
/// Runs only when SEED_DEMO is set, and only against a database with no
/// customers — migrations run on every boot (D-28), and re-running this would
/// pile a second workshop on top of the first.
/// </para>
/// </summary>
public static class DemoSeeder
{
    /// <summary>Fixed so that two runs produce the same workshop. A demo that
    /// changes shape every deploy is one nobody can rehearse against.</summary>
    private const int Seed = 20260914;

    private const int Months = 6;

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(DemoSeeder));

        if (!config.GetValue("SEED_DEMO", false))
        {
            return;
        }

        if (await db.Customers.AnyAsync(ct))
        {
            logger.LogInformation("Demo data skipped: the database already has customers.");
            return;
        }

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin, ct);

        if (admin is null)
        {
            logger.LogWarning("Demo data skipped: no admin exists to own the records.");
            return;
        }

        var random = new Random(Seed);
        var now = DateTimeOffset.UtcNow;

        var staff = await SeedStaff(db, hasher, config, logger, ct);
        var vehicles = await SeedCustomersAndVehicles(db, random, ct);
        var parts = await SeedParts(db, admin, now, ct);

        await SeedServiceOrders(db, random, now, admin, staff, vehicles, parts, ct);

        logger.LogInformation(
            "Demo data created: {Customers} customers, {Parts} parts, {Orders} service orders.",
            await db.Customers.CountAsync(ct),
            await db.Parts.CountAsync(ct),
            await db.ServiceOrders.CountAsync(ct));
    }

    private static async Task<List<User>> SeedStaff(
        AppDbContext db,
        IPasswordHasher hasher,
        IConfiguration config,
        ILogger logger,
        CancellationToken ct)
    {
        var password = config["SEED_DEMO_PASSWORD"];

        // Without a password the accounts still have to exist — an order needs a
        // mechanic to be assigned to. They just get a hash of something nobody
        // knows, instead of a shared secret invented here.
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "SEED_DEMO_PASSWORD is not set: the demo staff is created without a usable login.");
            password = Guid.NewGuid().ToString("N");
        }

        var hash = hasher.Hash(password);
        List<User> staff = [];

        foreach (var name in DemoData.Mechanics)
        {
            staff.Add(NewUser(name, UserRole.Mechanic, hash));
        }

        staff.Add(NewUser(DemoData.Attendant, UserRole.Attendant, hash));

        db.Users.AddRange(staff);
        await db.SaveChangesAsync(ct);

        return staff;
    }

    private static User NewUser(string name, UserRole role, string hash) => new()
    {
        WorkshopId = AdminSeeder.MvpWorkshopId,
        Name = name,
        Email = Login(name),
        PasswordHash = hash,
        Role = role
    };

    /// <summary>"Roberto Silva" becomes "roberto.silva@syspitstops.local", which
    /// is what somebody demonstrating the system would guess.</summary>
    private static string Login(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var handle = $"{parts[0]}.{parts[^1]}".ToLowerInvariant();

        var ascii = string.Concat(handle
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => char.GetUnicodeCategory(c)
                != System.Globalization.UnicodeCategory.NonSpacingMark));

        return $"{ascii}@syspitstops.local";
    }

    private static async Task<List<Vehicle>> SeedCustomersAndVehicles(
        AppDbContext db, Random random, CancellationToken ct)
    {
        List<Customer> customers = [];

        foreach (var (name, phone) in DemoData.Customers)
        {
            customers.Add(new Customer
            {
                WorkshopId = AdminSeeder.MvpWorkshopId,
                Name = name,
                Phone = phone
            });
        }

        db.Customers.AddRange(customers);
        await db.SaveChangesAsync(ct);

        List<Vehicle> vehicles = [];

        // More vehicles than customers, so some of them own two — which is what
        // makes the "customer is frozen on the order" rule visible in the data.
        for (var i = 0; i < DemoData.Vehicles.Length; i++)
        {
            var (plate, brand, model, year) = DemoData.Vehicles[i];

            vehicles.Add(new Vehicle
            {
                WorkshopId = AdminSeeder.MvpWorkshopId,
                Owner = customers[i % customers.Count],
                Plate = plate,
                Brand = brand,
                Model = model,
                ModelYear = year,
                Color = null
            });
        }

        db.Vehicles.AddRange(vehicles);
        await db.SaveChangesAsync(ct);

        return vehicles;
    }

    private static async Task<List<Part>> SeedParts(
        AppDbContext db, User admin, DateTimeOffset now, CancellationToken ct)
    {
        List<Part> parts = [];
        List<StockMovement> movements = [];

        foreach (var (sku, name, sale, cost) in DemoData.Parts)
        {
            // Enough to survive six months of orders and still show a balance.
            // The minimum is deliberately close to it on some rows, so the low
            // stock filter of the inventory screen has something to find.
            var quantity = 40m + (sku.Length % 5) * 12m;

            // A few parts carry a minimum the workshop actually wants kept, so
            // the low stock filter and the KPI of section 6 have rows to find.
            var minimum = parts.Count % 5 == 0 ? 45m : 8m;

            var part = new Part
            {
                WorkshopId = AdminSeeder.MvpWorkshopId,
                Sku = sku,
                Name = name,
                Unit = "UN",
                SalePrice = sale,
                CostPrice = cost,
                QuantityOnHand = quantity,
                MinQuantity = minimum
            };

            parts.Add(part);

            // Section 4 of data-model.md: the balance never moves without the
            // row that explains it, not even when the application writes both.
            movements.Add(new StockMovement
            {
                WorkshopId = AdminSeeder.MvpWorkshopId,
                Part = part,
                MovementType = MovementType.In,
                Quantity = quantity,
                UnitCost = cost,
                UserId = admin.Id,
                Note = "Carga inicial do estoque.",
                CreatedAt = now.AddMonths(-Months).AddDays(-3)
            });
        }

        db.Parts.AddRange(parts);
        db.StockMovements.AddRange(movements);
        await db.SaveChangesAsync(ct);

        return parts;
    }

    private static async Task SeedServiceOrders(
        AppDbContext db,
        Random random,
        DateTimeOffset now,
        User admin,
        List<User> staff,
        List<Vehicle> vehicles,
        List<Part> parts,
        CancellationToken ct)
    {
        var mechanics = staff.Where(u => u.Role == UserRole.Mechanic).ToList();
        var attendant = staff.First(u => u.Role == UserRole.Attendant);

        // The board states, cycled instead of drawn at random: with ten orders a
        // random pick leaves columns empty, and an empty column reads as a bug.
        ServiceOrderStatus[] openStates =
        [
            ServiceOrderStatus.Requested,
            ServiceOrderStatus.Confirmed,
            ServiceOrderStatus.InYard,
            ServiceOrderStatus.AwaitingApproval,
            ServiceOrderStatus.InProgress
        ];

        var openIndex = 0;

        for (var monthsAgo = Months - 1; monthsAgo >= 0; monthsAgo--)
        {
            var current = monthsAgo == 0;

            // The current month is still happening, so it carries the open work.
            // It closes some orders too — a month with nothing delivered would
            // draw the last column of the revenue chart at zero.
            // Two full turns of the board states, plus the closed and the
            // canceled ones: every column ends with the same weight.
            var count = current ? 20 : random.Next(13, 17);

            for (var i = 0; i < count; i++)
            {
                var closed = !current || i < 8;
                var canceled = current && i >= 18;

                var openedAt = now
                    .AddMonths(-monthsAgo)
                    .AddDays(random.Next(0, 26) - now.Day + 1)
                    .AddHours(random.Next(8, 17) - now.Hour)
                    .AddMinutes(random.Next(0, 60) - now.Minute);

                var vehicle = vehicles[random.Next(vehicles.Count)];
                var mechanic = mechanics[random.Next(mechanics.Count)];

                var order = new ServiceOrder
                {
                    WorkshopId = AdminSeeder.MvpWorkshopId,
                    Vehicle = vehicle,
                    // D-08: the customer is copied from the owner at opening and
                    // frozen, never resolved through the vehicle later.
                    CustomerId = vehicle.OwnerId,
                    Mechanic = mechanic,
                    CreatedBy = attendant.Id,
                    Status = ServiceOrderStatus.Requested,
                    // Lida na entrada do veículo, e por isso vive na OS, não no
                    // cadastro: é o valor daquele atendimento.
                    Mileage = random.Next(20_000, 160_000),
                    ReportedIssue = Pick(random, DemoData.Complaints),
                    OpenedAt = openedAt,
                    CreatedAt = openedAt,
                    UpdatedAt = openedAt
                };

                db.ServiceOrders.Add(order);

                var items = BuildItems(db, random, order, parts, attendant.Id, openedAt);
                var total = items.Sum(i => i.Quantity * i.UnitPrice);

                if (canceled)
                {
                    Cancel(db, random, order, items, total, openedAt, attendant, admin);
                }
                else if (closed)
                {
                    Close(db, random, order, items, total, openedAt, attendant, mechanic, parts);
                }
                else
                {
                    // The round decides which of the two quotes awaiting an
                    // answer is already past its date, instead of a draw that
                    // can leave the screen with none of either.
                    var round = openIndex / openStates.Length;
                    Open(db, random, order, items, total, openedAt, attendant, mechanic,
                        openStates[openIndex++ % openStates.Length], stale: round > 0);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static List<ServiceOrderItem> BuildItems(
        AppDbContext db,
        Random random,
        ServiceOrder order,
        List<Part> parts,
        Guid author,
        DateTimeOffset at)
    {
        List<ServiceOrderItem> items = [];

        foreach (var (description, price) in Take(random, DemoData.Services, random.Next(1, 3)))
        {
            items.Add(new ServiceOrderItem
            {
                ServiceOrder = order,
                ItemType = ItemType.Service,
                Description = description,
                Quantity = 1m,
                // D-07: the price is frozen here. Reading it back from the table
                // of services later would rewrite past revenue.
                UnitPrice = price,
                CreatedBy = author,
                CreatedAt = at
            });
        }

        foreach (var part in Take(random, parts, random.Next(0, 4)))
        {
            items.Add(new ServiceOrderItem
            {
                ServiceOrder = order,
                ItemType = ItemType.Part,
                Part = part,
                Description = part.Name,
                Quantity = random.Next(1, 3),
                UnitPrice = part.SalePrice,
                CreatedBy = author,
                CreatedAt = at
            });
        }

        db.ServiceOrderItems.AddRange(items);

        return items;
    }

    /// <summary>Walks the order through the whole flow, writing the history the
    /// real transitions would have written. The average execution time KPI reads
    /// exactly these rows, so skipping them would make it unanswerable.</summary>
    private static void Close(
        AppDbContext db,
        Random random,
        ServiceOrder order,
        List<ServiceOrderItem> items,
        decimal total,
        DateTimeOffset openedAt,
        User attendant,
        User mechanic,
        List<Part> parts)
    {
        var at = openedAt;

        at = Move(db, order, null, ServiceOrderStatus.Requested, attendant, at, 0);
        at = Move(db, order, ServiceOrderStatus.Requested, ServiceOrderStatus.Confirmed, attendant, at, random.Next(1, 6));
        at = Move(db, order, ServiceOrderStatus.Confirmed, ServiceOrderStatus.InYard, attendant, at, random.Next(2, 20));
        at = Move(db, order, ServiceOrderStatus.InYard, ServiceOrderStatus.AwaitingApproval, mechanic, at, random.Next(1, 8));

        var sentAt = at;
        at = Move(db, order, ServiceOrderStatus.AwaitingApproval, ServiceOrderStatus.InProgress, attendant, at, random.Next(1, 30));

        db.Quotes.Add(NewQuote(order, items, total, attendant.Id, sentAt, at, QuoteStatus.Approved));

        var readyAt = at.AddHours(random.Next(3, 28));
        Move(db, order, ServiceOrderStatus.InProgress, ServiceOrderStatus.Ready, mechanic, at, random.Next(3, 28));
        WriteStockOff(db, order, items, parts, readyAt, mechanic.Id);

        var deliveredAt = readyAt.AddHours(random.Next(2, 40));
        Move(db, order, ServiceOrderStatus.Ready, ServiceOrderStatus.Delivered, attendant, readyAt, random.Next(2, 40));

        order.Diagnosis = Pick(random, DemoData.Diagnoses);
        order.Status = ServiceOrderStatus.Delivered;
        order.ClosedAt = readyAt;
        order.UpdatedAt = deliveredAt;
    }

    /// <summary>The current month, spread across the board so every column has
    /// something and the yard tiles are not all zero.</summary>
    private static void Open(
        AppDbContext db,
        Random random,
        ServiceOrder order,
        List<ServiceOrderItem> items,
        decimal total,
        DateTimeOffset openedAt,
        User attendant,
        User mechanic,
        ServiceOrderStatus target,
        bool stale)
    {
        var at = Move(db, order, null, ServiceOrderStatus.Requested, attendant, openedAt, 0);

        if (target == ServiceOrderStatus.Requested)
        {
            order.Status = ServiceOrderStatus.Requested;
            order.ScheduledAt = openedAt.AddDays(random.Next(1, 8));
            return;
        }

        at = Move(db, order, ServiceOrderStatus.Requested, ServiceOrderStatus.Confirmed, attendant, at, random.Next(1, 6));
        order.Status = ServiceOrderStatus.Confirmed;

        if (target == ServiceOrderStatus.Confirmed)
        {
            return;
        }

        at = Move(db, order, ServiceOrderStatus.Confirmed, ServiceOrderStatus.InYard, attendant, at, random.Next(2, 20));
        order.Status = ServiceOrderStatus.InYard;

        if (target == ServiceOrderStatus.InYard)
        {
            return;
        }

        var sentAt = at;
        at = Move(db, order, ServiceOrderStatus.InYard, ServiceOrderStatus.AwaitingApproval, mechanic, at, random.Next(1, 8));
        order.Status = ServiceOrderStatus.AwaitingApproval;

        if (target == ServiceOrderStatus.AwaitingApproval)
        {
            // Waiting for an answer, which is what the quote screen exists for.
            // One of them is sent far enough back to be already past its date:
            // expiry is decided on read (D-14), so the screen has to show it.
            var quote = NewQuote(order, items, total, attendant.Id, sentAt, null, QuoteStatus.Sent);

            if (stale)
            {
                quote.SentAt = sentAt.AddDays(-45);
                quote.ExpiresAt = sentAt.AddDays(-38);
            }

            db.Quotes.Add(quote);
            return;
        }

        at = Move(db, order, ServiceOrderStatus.AwaitingApproval, ServiceOrderStatus.InProgress, attendant, at, random.Next(1, 30));
        order.Status = ServiceOrderStatus.InProgress;
        order.Diagnosis = Pick(random, DemoData.Diagnoses);

        db.Quotes.Add(NewQuote(order, items, total, attendant.Id, sentAt, at, QuoteStatus.Approved));
    }

    /// <summary>The customer said no. Only the admin cancels (D-13), and the
    /// refused quote is what the rejected filter of the quote screen exists to
    /// find — without one it would always answer an empty list.</summary>
    private static void Cancel(
        AppDbContext db,
        Random random,
        ServiceOrder order,
        List<ServiceOrderItem> items,
        decimal total,
        DateTimeOffset openedAt,
        User attendant,
        User admin)
    {
        var at = Move(db, order, null, ServiceOrderStatus.Requested, attendant, openedAt, 0);
        at = Move(db, order, ServiceOrderStatus.Requested, ServiceOrderStatus.Confirmed, attendant, at, random.Next(1, 6));
        at = Move(db, order, ServiceOrderStatus.Confirmed, ServiceOrderStatus.InYard, attendant, at, random.Next(2, 20));

        var sentAt = at;
        at = Move(db, order, ServiceOrderStatus.InYard, ServiceOrderStatus.AwaitingApproval, attendant, at, random.Next(1, 8));

        var answeredAt = at.AddHours(random.Next(2, 48));
        var quote = NewQuote(order, items, total, attendant.Id, sentAt, answeredAt, QuoteStatus.Rejected);
        quote.RejectionReason = "Valor acima do que eu esperava. Vou pesquisar em outro lugar.";
        db.Quotes.Add(quote);

        Move(db, order, ServiceOrderStatus.AwaitingApproval, ServiceOrderStatus.Canceled, admin, answeredAt, 1);

        order.Status = ServiceOrderStatus.Canceled;
        order.UpdatedAt = answeredAt.AddHours(1);
    }

    private static DateTimeOffset Move(
        AppDbContext db,
        ServiceOrder order,
        ServiceOrderStatus? from,
        ServiceOrderStatus to,
        User by,
        DateTimeOffset at,
        int hours)
    {
        var changedAt = at.AddHours(hours);

        db.ServiceOrderStatusHistory.Add(new ServiceOrderStatusHistory
        {
            ServiceOrder = order,
            FromStatus = from,
            ToStatus = to,
            ChangedBy = by.Id,
            ChangedAt = changedAt
        });

        return changedAt;
    }

    private static Quote NewQuote(
        ServiceOrder order,
        List<ServiceOrderItem> items,
        decimal total,
        Guid sentBy,
        DateTimeOffset sentAt,
        DateTimeOffset? respondedAt,
        QuoteStatus status)
    {
        // D-10: the quote freezes what the customer saw. An empty snapshot would
        // be a quote nobody could open, so the items are copied even here.
        var snapshot = items
            .Select(i => new QuoteItemSnapshot(
                i.ItemType, i.Description, i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice))
            .ToList();

        return new Quote
        {
            ServiceOrder = order,
            PublicToken = PublicToken.Create(),
            Status = status,
            ItemsSnapshot = JsonSerializer.Serialize(snapshot, QuoteLifecycle.Json),
            TotalAmount = total,
            SentBy = sentBy,
            SentAt = sentAt,
            // Long enough that a quote seeded today is still open at the
            // presentation, instead of expiring between the deploy and the demo.
            ExpiresAt = sentAt.AddDays(30),
            RespondedAt = respondedAt
        };
    }

    /// <summary>D-11: the parts leave stock when the work is finished. The seeder
    /// writes the movement and the balance together, the same way the transition
    /// does — a demo whose stock does not reconcile teaches the wrong thing.</summary>
    private static void WriteStockOff(
        AppDbContext db,
        ServiceOrder order,
        List<ServiceOrderItem> items,
        List<Part> parts,
        DateTimeOffset at,
        Guid userId)
    {
        var used = items
            .Where(i => i.ItemType == ItemType.Part && i.Part is not null)
            .GroupBy(i => i.Part!)
            .Select(g => new { Part = g.Key, Quantity = g.Sum(i => i.Quantity) });

        foreach (var use in used)
        {
            use.Part.QuantityOnHand -= use.Quantity;
            use.Part.UpdatedAt = at;

            db.StockMovements.Add(new StockMovement
            {
                WorkshopId = AdminSeeder.MvpWorkshopId,
                Part = use.Part,
                MovementType = MovementType.Out,
                Quantity = use.Quantity,
                UnitCost = use.Part.CostPrice,
                ServiceOrder = order,
                UserId = userId,
                Note = "Baixa automática da OS.",
                CreatedAt = at
            });
        }
    }

    private static T Pick<T>(Random random, IReadOnlyList<T> options) =>
        options[random.Next(options.Count)];

    private static List<T> Take<T>(Random random, IReadOnlyList<T> options, int count) =>
        options.OrderBy(_ => random.Next()).Take(count).ToList();
}
