using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SysPitstops.Api.Auth;
using SysPitstops.Api.Contracts;
using SysPitstops.Api.Controllers;
using SysPitstops.Api.Data;
using SysPitstops.Api.Domain;
using SysPitstops.Api.Storage;
using Xunit;

namespace SysPitstops.Tests;

public class MediaRulesTests
{
    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    public void ThePhotoFormatsAreAccepted(string contentType) =>
        Assert.True(MediaRules.IsAllowedType(contentType));

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/gif")]
    [InlineData("text/html")]
    [InlineData(null)]
    public void EverythingElseIsRefused(string? contentType) =>
        Assert.False(MediaRules.IsAllowedType(contentType));

    /// <summary>Browsers append parameters, and they are not part of the type.</summary>
    [Fact]
    public void TheCharsetDoesNotChangeTheType()
    {
        Assert.True(MediaRules.IsAllowedType("image/jpeg; charset=binary"));
        Assert.Equal("image/jpeg", MediaRules.Normalize("image/jpeg; charset=binary"));
    }

    [Fact]
    public void TheExtensionFollowsTheType()
    {
        Assert.Equal(".jpg", MediaRules.ExtensionFor("image/jpeg"));
        Assert.Equal(".png", MediaRules.ExtensionFor("IMAGE/PNG"));
    }
}

public class ServiceOrderMediaControllerTests
{
    [Fact]
    public async Task UploadingKeepsTheFileAndTheRowTogether()
    {
        var (db, storage, controller) = Build();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);

        var created = TestApi.Body(
            await controller.Upload(order.Id, Photo("avaria.jpg"), default));

        Assert.Equal("image/jpeg", created.ContentType);
        Assert.Equal(order.Id, created.ServiceOrderId);
        var stored = Assert.Single(db.ServiceOrderMedia);
        Assert.True(storage.Has(stored.StorageKey));
    }

    /// <summary>The key is logical (D-25): it must not read like a path chosen
    /// by whoever uploaded the file.</summary>
    [Fact]
    public async Task TheStoredKeyCarriesTheExtensionAndNotTheSentName()
    {
        var (db, _, controller) = Build();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);

        await controller.Upload(order.Id, Photo("foto do cliente.jpg"), default);

        var stored = Assert.Single(db.ServiceOrderMedia);
        Assert.EndsWith(".jpg", stored.StorageKey);
        Assert.DoesNotContain("foto do cliente", stored.StorageKey);
    }

    [Fact]
    public async Task AnEmptyFileIsRefused()
    {
        var (db, storage, controller) = Build();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);

        var result = await controller.Upload(
            order.Id, Photo("vazia.jpg", content: ""), default);

        result.Result.AssertRejects("File");
        Assert.Empty(db.ServiceOrderMedia);
        Assert.Equal(0, storage.Count);
    }

    [Fact]
    public async Task AFormatThatIsNotAPhotoIsRefused()
    {
        var (db, storage, controller) = Build();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);

        var result = await controller.Upload(
            order.Id, Photo("laudo.pdf", contentType: "application/pdf"), default);

        result.Result.AssertRejects("File");
        Assert.Empty(db.ServiceOrderMedia);
        Assert.Equal(0, storage.Count);
    }

    [Fact]
    public async Task AFileOverTheLimitIsRefused()
    {
        var (db, storage, controller) = Build();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);

        var result = await controller.Upload(
            order.Id, Photo("grande.jpg", size: MediaRules.MaxBytes + 1), default);

        result.Result.AssertRejects("File");
        Assert.Empty(db.ServiceOrderMedia);
        Assert.Equal(0, storage.Count);
    }

    /// <summary>The photo is part of the laudo, so it follows the same rule as
    /// the diagnosis: a mechanic writes only into an order that is theirs.</summary>
    [Fact]
    public async Task AMechanicWhoIsNotResponsibleCannotUpload()
    {
        var db = TestApi.NewDatabase();
        var order = db.AddServiceOrder(
            status: ServiceOrderStatus.InYard, mechanic: db.AddUser("Roberto", UserRole.Mechanic));
        var other = db.AddUser("Outro", UserRole.Mechanic);
        var controller = new ServiceOrderMediaController(db, new FakeMediaStorage()).AsUser(other);

        var result = await controller.Upload(order.Id, Photo("avaria.jpg"), default);

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Empty(db.ServiceOrderMedia);
    }

    [Fact]
    public async Task TheResponsibleMechanicUploads()
    {
        var db = TestApi.NewDatabase();
        var mechanic = db.AddUser("Roberto", UserRole.Mechanic);
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard, mechanic: mechanic);
        var controller = new ServiceOrderMediaController(db, new FakeMediaStorage()).AsUser(mechanic);

        var created = TestApi.Body(await controller.Upload(order.Id, Photo("avaria.jpg"), default));

        Assert.Equal("Roberto", created.UploadedByName);
    }

    [Fact]
    public async Task AClosedOrderDoesNotReceiveMorePhotos()
    {
        var (db, _, controller) = Build();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.Delivered);

        var result = await controller.Upload(order.Id, Photo("tarde.jpg"), default);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Empty(db.ServiceOrderMedia);
    }

    [Fact]
    public async Task TheContentComesBackWithTheStoredType()
    {
        var (db, _, controller) = Build();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);
        var created = TestApi.Body(
            await controller.Upload(order.Id, Photo("avaria.jpg", content: "bytes"), default));

        var result = Assert.IsType<FileStreamResult>(
            await controller.Content(order.Id, created.Id, default));

        Assert.Equal("image/jpeg", result.ContentType);
        using var reader = new StreamReader(result.FileStream);
        Assert.Equal("bytes", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task RemovingTakesTheRowAndTheFile()
    {
        var (db, storage, controller) = Build();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);
        var created = TestApi.Body(await controller.Upload(order.Id, Photo("avaria.jpg"), default));
        var key = db.ServiceOrderMedia.Single().StorageKey;

        var result = await controller.Remove(order.Id, created.Id, default);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.ServiceOrderMedia);
        Assert.False(storage.Has(key));
    }

    [Fact]
    public async Task TheListAnswersInTheOrderTheyWereTaken()
    {
        var (db, _, controller) = Build();
        var order = db.AddServiceOrder(status: ServiceOrderStatus.InYard);

        await controller.Upload(order.Id, Photo("1.jpg", caption: "Antes"), default);
        await controller.Upload(order.Id, Photo("2.jpg", caption: "Depois"), default);

        var listed = TestApi.Body(await controller.List(order.Id, default));

        Assert.Equal(["Antes", "Depois"], listed.Select(m => m.Caption));
    }

    [Fact]
    public async Task AnUnknownOrderIsNotFound()
    {
        var (_, _, controller) = Build();

        var result = await controller.Upload(Guid.NewGuid(), Photo("avaria.jpg"), default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    /// <summary>The controller signs in as a real user because UploadedByUser is
    /// a required navigation: the list joins on it, and a random id would answer
    /// an empty page. The foreign key guarantees this in production.</summary>
    private static (AppDbContext Db, FakeMediaStorage Storage, ServiceOrderMediaController Controller)
        Build()
    {
        var db = TestApi.NewDatabase();
        var storage = new FakeMediaStorage();
        var controller = new ServiceOrderMediaController(db, storage).AsUser(db.AddUser());

        return (db, storage, controller);
    }

    private static UploadMediaRequest Photo(
        string name,
        string contentType = "image/jpeg",
        string content = "conteudo",
        long? size = null,
        string? caption = null) =>
        new() { File = new FakeFormFile(name, contentType, content, size), Caption = caption };
}

/// <summary>Keeps the bytes in memory so the tests can assert that the file and
/// the row live and die together, without touching the disk.</summary>
internal sealed class FakeMediaStorage : IMediaStorage
{
    private readonly Dictionary<string, byte[]> _files = [];

    public int Count => _files.Count;

    public bool Has(string key) => _files.ContainsKey(key);

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        var key = $"2026/09/{Guid.NewGuid():N}{MediaRules.ExtensionFor(contentType)}";
        _files[key] = buffer.ToArray();

        return key;
    }

    public Task<Stream?> OpenAsync(string storageKey, CancellationToken ct) =>
        Task.FromResult<Stream?>(
            _files.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        _files.Remove(storageKey);
        return Task.CompletedTask;
    }
}

/// <summary>A size larger than the content lets the limit be tested without
/// allocating megabytes of nothing.</summary>
internal sealed class FakeFormFile(string name, string contentType, string content, long? size)
    : IFormFile
{
    private readonly byte[] _bytes = Encoding.UTF8.GetBytes(content);

    public string ContentType { get; set; } = contentType;
    public string ContentDisposition { get; set; } = string.Empty;
    public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
    public long Length => size ?? _bytes.Length;
    public string Name => "file";
    public string FileName => name;

    public void CopyTo(Stream target) => target.Write(_bytes);

    public Task CopyToAsync(Stream target, CancellationToken ct = default) =>
        target.WriteAsync(_bytes, ct).AsTask();

    public Stream OpenReadStream() => new MemoryStream(_bytes);
}
