using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ServeFlow.Application.Management;
using SkiaSharp;

namespace ServeFlow.Infrastructure.Management;

public sealed class ProductImageOptions
{
    public string? RootPath { get; set; }
}

public sealed partial class ProductImageStore : IProductImageStore, IDisposable
{
    public const int MaxUploadBytes = 5 * 1024 * 1024;
    private const int MaxStoredBytes = 20 * 1024 * 1024;
    private readonly string _root;
    private readonly SemaphoreSlim _processing = new(2, 2);

    public ProductImageStore(IOptions<ProductImageOptions> options, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() && !Path.IsPathFullyQualified(options.Value.RootPath ?? ""))
            throw new InvalidOperationException("Configure an absolute Uploads:RootPath for production product images.");
        _root = Path.GetFullPath(options.Value.RootPath ?? Path.Combine(environment.ContentRootPath, "App_Data", "product-images"));
        var webRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));
        if (IsWithin(_root, webRoot)) throw new InvalidOperationException("Product images must be stored outside wwwroot.");
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(_root);
        else Directory.CreateDirectory(_root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    public async Task<ImageUploadResult> SaveAsync(Stream input, string fileName, string contentType, CancellationToken token)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var jpeg = extension is ".jpg" or ".jpeg";
        var png = extension == ".png";
        if ((!jpeg && !png) || (jpeg && contentType != "image/jpeg") || (png && contentType != "image/png"))
            return Failure("Upload a PNG or JPEG image with a matching file type.");
        await _processing.WaitAsync(token);
        try
        {
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(), token)) > 0)
            {
                if (memory.Length + read > MaxUploadBytes) return Failure("Images must be no larger than 5 MB.");
                await memory.WriteAsync(buffer.AsMemory(0, read), token);
            }
            var bytes = memory.ToArray();
            if (bytes.Length < 8 || (jpeg && !(bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff)) ||
                (png && !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })))
                return Failure("The image signature does not match its file type.");
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            if (codec is null || (codec.EncodedFormat != SKEncodedImageFormat.Jpeg && codec.EncodedFormat != SKEncodedImageFormat.Png))
                return Failure("The image could not be decoded.");
            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || info.Width > 4096 || info.Height > 4096 || (long)info.Width * info.Height > 8000000 || codec.FrameCount > 1)
                return Failure("Use a non-animated image no larger than 4096 pixels per side and 8 megapixels.");
            using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
                return Failure("The image is incomplete or corrupt.");
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            if (encoded is null || encoded.Size > MaxStoredBytes) return Failure("The processed image is too large.");
            var key = Guid.NewGuid().ToString("N") + ".png";
            var fileOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
                Share = FileShare.None, Options = FileOptions.Asynchronous };
            if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            var path = Path.Combine(_root, key);
            var created = false;
            try
            {
                await using var output = new FileStream(path, fileOptions);
                created = true;
                await output.WriteAsync(encoded.ToArray(), token);
            }
            catch
            {
                if (created) File.Delete(path); // Only this new, unreferenced file can be removed on a write failure.
                throw;
            }
            return new ImageUploadResult(true, key, null);
        }
        finally { _processing.Release(); }
    }

    public Stream? OpenRead(string key)
    {
        if (!StorageKey().IsMatch(key)) return null;
        var path = Path.Combine(_root, key);
        try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public void Dispose() => _processing.Dispose();

    private static bool IsWithin(string path, string directory) => path.Equals(directory,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
        || path.StartsWith(directory + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static ImageUploadResult Failure(string error) => new(false, null, error);
    [GeneratedRegex("\\A[a-f0-9]{32}\\.png\\z", RegexOptions.CultureInvariant)]
    private static partial Regex StorageKey();
}
