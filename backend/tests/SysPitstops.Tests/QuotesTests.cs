using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using Xunit;

namespace SysPitstops.Tests;

public class QuoteSendingTests
{
    [Fact]
    public async Task TheQuoteFreezesTheItemsAndTheTotal()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard, discount: 50m);
        db.AddItem(order, "Troca de pastilhas", 1m, 300m);
        db.AddItem(order, "Alinhamento", 1m, 150m);

        var quote = TestApi.Body(await Quotes(db).Send(order.Id, default));

        Assert.Equal(QuoteStatus.Sent, quote.Status);
        Assert.Equal(400m, quote.TotalAmount);   
        Assert.Equal(2, quote.Items.Count);
        Assert.Equal("Troca de pastilhas", quote.Items[0].Description);
    }

    [Fact]
    public async Task ChangingTheOrderDoesNotRewriteAQuoteAlreadySent()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);
        var item = db.AddItem(order, "Troca de óleo", 1m, 200m);
        var quotes = Quotes(db);

        var sent = TestApi.Body(await quotes.Send(order.Id, default));

        item.UnitPrice = 999m;
        item.Description = "Outro serviço";
        await db.SaveChangesAsync();

        var reread = Assert.Single(TestApi.Body(await quotes.List(order.Id, default)));

        Assert.Equal(200m, reread.TotalAmount);
        Assert.Equal("Troca de óleo", Assert.Single(reread.Items).Description);
        Assert.Equal(sent.Id, reread.Id);
    }

    [Fact]
    public async Task TheTokenIsLongAndNeverRepeats()
    {
        var db = TestApi.NewDatabase();
        var quotes = Quotes(db);

        var first = TestApi.Body(await quotes.Send(WithItem(db).Id, default));
        var second = TestApi.Body(await quotes.Send(WithItem(db).Id, default));

        Assert.NotEqual(first.PublicToken, second.PublicToken);
        Assert.True(first.PublicToken.Length >= 43, first.PublicToken);
        Assert.DoesNotContain('+', first.PublicToken);
        Assert.DoesNotContain('/', first.PublicToken);
    }

    [Fact]
    public async Task SendingAgainClosesThePreviousLink()
    {
        var db = TestApi.NewDatabase();
        var order = WithItem(db);
        var quotes = Quotes(db);

        await quotes.Send(order.Id, default);
        await quotes.Send(order.Id, default);

        var all = TestApi.Body(await quotes.List(order.Id, default));

        Assert.Equal(2, all.Count);
        Assert.Single(all, q => q.Status == QuoteStatus.Sent);
        Assert.Single(all, q => q.Status == QuoteStatus.Expired);
    }

    [Fact]
    public async Task AnOrderWithoutItemsCannotBeQuoted()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);

        var result = await Quotes(db).Send(order.Id, default);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.Quotes);
    }

    [Fact]
    public async Task AFinishedOrderNoLongerReceivesQuotes()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.Delivered);
        db.AddItem(order, "Serviço", 1m, 100m);

        var result = await Quotes(db).Send(order.Id, default);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    internal static ServiceOrder WithItem(AppDbContext db, decimal price = 100m)
    {
        var order = db.AddServiceOrder(status: ServiceOrderStatus.AwaitingApproval);
        db.AddItem(order, "Serviço", 1m, price);
        return order;
    }

    internal static QuotesController Quotes(AppDbContext db) =>
        new QuotesController(db).AsUser(db.AddUser());
}

public class PublicQuoteTests
{
    [Fact]
    public async Task TheCustomerSeesTheQuoteWithoutSigningIn()
    {
        var db = TestApi.NewDatabase();
        var token = await Send(db);

        var seen = TestApi.Body(await Public(db).Get(token, default));

        Assert.Equal(QuoteStatus.Sent, seen.Status);
        Assert.Equal(100m, seen.TotalAmount);
        Assert.Single(seen.Items);
        Assert.False(string.IsNullOrWhiteSpace(seen.VehiclePlate));
    }

    [Fact]
    public async Task ApprovingStartsTheWorkAndRecordsWhoDecided()
    {
        var db = TestApi.NewDatabase();
        var token = await Send(db);

        var answered = TestApi.Body(await Public(db).Approve(token, default));

        Assert.Equal(QuoteStatus.Approved, answered.Status);

        var order = db.ServiceOrders.Single();
        Assert.Equal(ServiceOrderStatus.InProgress, order.Status);

        var entry = db.ServiceOrderStatusHistory.Single(h => h.ToStatus == ServiceOrderStatus.InProgress);
        Assert.Equal(ServiceOrderStatus.AwaitingApproval, entry.FromStatus);
        Assert.Contains("cliente", entry.Note);
    }

    [Fact]
    public async Task RejectingKeepsTheOrderWhereItIs()
    {
        var db = TestApi.NewDatabase();
        var token = await Send(db);

        var answered = TestApi.Body(await Public(db).Reject(
            token, new RejectQuoteRequest { Reason = "  Muito caro.  " }, default));

        Assert.Equal(QuoteStatus.Rejected, answered.Status);
        Assert.Equal("Muito caro.", db.Quotes.Single().RejectionReason);
        Assert.Equal(ServiceOrderStatus.AwaitingApproval, db.ServiceOrders.Single().Status);
    }

    [Fact]
    public async Task AnswerHappensOnlyOnce()
    {
        var db = TestApi.NewDatabase();
        var token = await Send(db);
        var api = Public(db);

        await api.Approve(token, default);
        var again = await api.Approve(token, default);

        Assert.IsType<ConflictObjectResult>(again.Result);
    }

    [Fact]
    public async Task AnExpiredLinkCannotBeApproved()
    {
        var db = TestApi.NewDatabase();
        var token = await Send(db);

        var quote = db.Quotes.Single();
        quote.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var result = await Public(db).Approve(token, default);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(QuoteStatus.Expired, db.Quotes.Single().Status);
        Assert.Equal(ServiceOrderStatus.AwaitingApproval, db.ServiceOrders.Single().Status);
    }

    [Fact]
    public async Task AnUnknownTokenIsJustNotFound()
    {
        var db = TestApi.NewDatabase();

        var result = await Public(db).Get("token-que-nao-existe", default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    private static async Task<string> Send(AppDbContext db)
    {
        var order = QuoteSendingTests.WithItem(db);
        var quote = TestApi.Body(await QuoteSendingTests.Quotes(db).Send(order.Id, default));
        return quote.PublicToken;
    }

    private static PublicQuotesController Public(AppDbContext db) =>
        new PublicQuotesController(db).AsUser();
}

public class QuoteExpiryTests
{
    [Fact]
    public void OnlyAQuoteAwaitingAnAnswerExpires()
    {
        var past = DateTimeOffset.UtcNow.AddDays(-1);

        Assert.True(ServiceOrderWorkflowQuoteHelper(QuoteStatus.Sent, past));
        Assert.False(ServiceOrderWorkflowQuoteHelper(QuoteStatus.Approved, past));
        Assert.False(ServiceOrderWorkflowQuoteHelper(QuoteStatus.Rejected, past));
    }

    [Fact]
    public void AQuoteStillWithinItsWeekDoesNotExpire()
    {
        Assert.False(ServiceOrderWorkflowQuoteHelper(
            QuoteStatus.Sent, DateTimeOffset.UtcNow.AddDays(1)));
    }

    private static bool ServiceOrderWorkflowQuoteHelper(
        QuoteStatus status, DateTimeOffset expiresAt) =>
        QuoteLifecycle.ApplyExpiry(new Quote
        {
            Status = status,
            ExpiresAt = expiresAt,
            ItemsSnapshot = "[]"
        });
}

public class QuoteListTests
{
    [Fact]
    public async Task TheListNamesTheOrderTheCustomerTheVehicleAndWhoSent()
    {
        var db = TestApi.NewDatabase();
        var sender = db.AddUser("Ana Atendente", UserRole.Attendant);
        var order = Ordered(db, number: 42);

        await new QuotesController(db).AsUser(sender).Send(order.Id, default);

        var row = Assert.Single(TestApi.Body(
            await new QuotesController(db).AsUser(sender).ListAll(null, null, null, default)).Items);

        Assert.Equal(42, row.ServiceOrderNumber);
        Assert.Equal("Ana Atendente", row.SentByName);
        Assert.Equal("Cliente", row.CustomerName);
        Assert.Equal("Fiat Uno", row.VehicleDescription);
        Assert.Equal(ServiceOrderStatus.AwaitingApproval, row.ServiceOrderStatus);
        Assert.NotEmpty(row.CustomerPhone);
        Assert.NotEmpty(row.PublicToken);
    }

    /// <summary>The per-order list reads the same navigations; before the fix it
    /// answered order 0 sent by nobody.</summary>
    [Fact]
    public async Task ThePerOrderListAlsoNamesWhoSentTheQuote()
    {
        var db = TestApi.NewDatabase();
        var sender = db.AddUser("Bia Atendente", UserRole.Attendant);
        var order = Ordered(db, number: 7);
        var quotes = new QuotesController(db).AsUser(sender);

        var sent = TestApi.Body(await quotes.Send(order.Id, default));
        var listed = Assert.Single(TestApi.Body(await quotes.List(order.Id, default)));

        Assert.Equal("Bia Atendente", sent.SentByName);
        Assert.Equal(7, sent.ServiceOrderNumber);
        Assert.Equal("Bia Atendente", listed.SentByName);
        Assert.Equal(7, listed.ServiceOrderNumber);
    }

    [Fact]
    public async Task AQuotePastItsDateIsListedAsExpired()
    {
        var db = TestApi.NewDatabase();
        await Expire(db);

        var row = Assert.Single(TestApi.Body(await Quotes(db).ListAll(null, null, null, default)).Items);

        Assert.Equal(QuoteStatus.Expired, row.Status);
    }

    [Fact]
    public async Task FilteringByAwaitingAnAnswerSkipsTheOnesAlreadyPastTheDate()
    {
        var db = TestApi.NewDatabase();
        await Expire(db);
        await Quotes(db).Send(Ordered(db).Id, default);

        var awaiting = TestApi.Body(
            await Quotes(db).ListAll(QuoteStatus.Sent, null, null, default));

        Assert.Equal(1, awaiting.Total);
        Assert.True(Assert.Single(awaiting.Items).ExpiresAt > DateTimeOffset.UtcNow);
    }

    /// <summary>Expiry is decided on read (D-14), so the expired ones are still
    /// SENT in the table: the filter has to find them anyway.</summary>
    [Fact]
    public async Task FilteringByExpiredFindsTheOnesStillStoredAsSent()
    {
        var db = TestApi.NewDatabase();
        await Expire(db);
        await Quotes(db).Send(Ordered(db).Id, default);

        var expired = TestApi.Body(
            await Quotes(db).ListAll(QuoteStatus.Expired, null, null, default));

        Assert.Equal(1, expired.Total);
        Assert.Equal(QuoteStatus.Expired, Assert.Single(expired.Items).Status);
    }

    [Fact]
    public async Task TheListPagesAndCountsTheWholeWorkshop()
    {
        var db = TestApi.NewDatabase();

        for (var i = 0; i < 3; i++)
        {
            await Quotes(db).Send(Ordered(db).Id, default);
        }

        var page = TestApi.Body(await Quotes(db).ListAll(null, 2, 2, default));

        Assert.Equal(3, page.Total);
        Assert.Equal(2, page.Page);
        Assert.Single(page.Items);
    }

    private static ServiceOrder Ordered(AppDbContext db, int number = 1)
    {
        var order = db.AddServiceOrder(status: ServiceOrderStatus.AwaitingApproval);
        order.Number = number;
        db.SaveChanges();
        db.AddItem(order, "Serviço", 1m, 100m);
        return order;
    }

    private static async Task Expire(AppDbContext db)
    {
        var sent = TestApi.Body(await Quotes(db).Send(Ordered(db).Id, default));
        var stored = await db.Quotes.SingleAsync(q => q.Id == sent.Id);
        stored.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();
    }

    private static QuotesController Quotes(AppDbContext db) =>
        new QuotesController(db).AsUser(db.AddUser());
}

