using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServeFlow.Infrastructure.Management;
using SkiaSharp;

namespace ServeFlow.UnitTests;

public sealed class ProductImageStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ServeFlowImageTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ValidImageIsReencodedWithAnUnpredictableKeyAndNoAppendedPayload()
    {
        using var store = Store();
        var original = ImageBytes(8, 8);
        var payload = Encoding.UTF8.GetBytes("<script>unexpected-payload</script>");
        using var input = new MemoryStream(original.Concat(payload).ToArray());
        var result = await store.SaveAsync(input, "../../unsafe.png", "image/png", TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded, result.Error);
        Assert.Matches("^[a-f0-9]{32}\\.png$", result.StorageKey!);
        using var output = store.OpenRead(result.StorageKey!);
        Assert.NotNull(output);
        using var memory = new MemoryStream();
        await output.CopyToAsync(memory, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("unexpected-payload", Encoding.UTF8.GetString(memory.ToArray()));
        using var decoded = SKBitmap.Decode(memory.ToArray());
        Assert.Equal(8, decoded.Width);
        Assert.Equal(8, decoded.Height);
        Assert.Equal(SKColors.ForestGreen, decoded.GetPixel(0, 0));
        Assert.Single(Directory.GetFiles(Path.Combine(_folder, "images")));
    }

    [Theory]
    [InlineData("image.svg", "image/svg+xml")]
    [InlineData("image.png", "text/html")]
    [InlineData("image.exe", "image/png")]
    public async Task UnsupportedFormatsAndMismatchedMimeTypesAreRejected(string name, string contentType)
    {
        using var store = Store();
        using var input = new MemoryStream(ImageBytes(8, 8));
        var result = await store.SaveAsync(input, name, contentType, TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Empty(Directory.GetFiles(Path.Combine(_folder, "images")));
    }

    [Fact]
    public async Task SpoofedPngSignatureIsRejected()
    {
        using var store = Store();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("<html>not an image</html>"));
        Assert.False((await store.SaveAsync(input, "fake.png", "image/png", TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task TruncatedImageIsRejected()
    {
        using var store = Store();
        using var input = new MemoryStream(ImageBytes(8, 8).Take(24).ToArray());
        Assert.False((await store.SaveAsync(input, "broken.png", "image/png", TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task InputOverFiveMegabytesIsRejectedBeforeDecoding()
    {
        using var store = Store();
        using var input = new MemoryStream(new byte[ProductImageStore.MaxUploadBytes + 1]);
        var result = await store.SaveAsync(input, "large.png", "image/png", TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Contains("5 MB", result.Error!);
    }

    [Fact]
    public async Task ExcessiveDimensionsAreRejectedBeforePixelAllocation()
    {
        using var store = Store();
        using var input = new MemoryStream(ImageBytes(5000, 1));
        var result = await store.SaveAsync(input, "wide.png", "image/png", TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Contains("4096", result.Error!);
    }

    [Theory]
    [InlineData("../../private.png")]
    [InlineData("/etc/passwd")]
    [InlineData("not-a-key.png")]
    public void ReadingInvalidKeysCannotTraverseDirectories(string key)
    {
        using var store = Store();
        Assert.Null(store.OpenRead(key));
    }

    [Fact]
    public void StorageInsideWwwrootIsRejected()
    {
        var environment = new TestEnvironment { ContentRootPath = _folder };
        Assert.Throws<InvalidOperationException>(() => new ProductImageStore(
            Options.Create(new ProductImageOptions { RootPath = Path.Combine(_folder, "wwwroot", "images") }), environment));
    }

    private ProductImageStore Store() => new(Options.Create(new ProductImageOptions { RootPath = Path.Combine(_folder, "images") }),
        new TestEnvironment { ContentRootPath = _folder });
    private static byte[] ImageBytes(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.ForestGreen);
        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
    public void Dispose() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ServeFlow.Tests";
        public string ContentRootPath { get; set; } = "/tmp";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
