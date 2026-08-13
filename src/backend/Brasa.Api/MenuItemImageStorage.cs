using Brasa.Shared.Primitives;

namespace Brasa.Api;

/// <summary>
/// Local-disk storage for menu item photos (CAT-02). Not cloud storage — no
/// S3/Azure Blob credentials exist for this environment (the same "skip
/// what needs real infra" call OPS-11 already makes), so this is an
/// honest dev/demo-scoped placeholder: a flat, gitignored directory under
/// the API's own content root, served back by <c>UseStaticFiles</c>
/// (<c>Program.cs</c>). A real multi-tenant production deployment would
/// need either per-tenant isolation or a signed-URL cloud store instead of
/// one shared local folder any tenant's guessed-or-leaked URL could read —
/// a named gap, not an oversight.
/// </summary>
public sealed class MenuItemImageStorage
{
    /// <summary>Rejects anything larger than this outright rather than accepting and truncating.</summary>
    public const long MaxSizeBytes = 5 * 1024 * 1024;

    // Keyed by the declared Content-Type, not sniffed from the file's own
    // magic bytes — a reasonable bar for a dev-only local store nothing
    // untrusted reaches yet, not a production-grade upload pipeline.
    private static readonly Dictionary<string, string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    /// <summary>The directory files are written to and served from — also what <c>Program.cs</c> points <c>UseStaticFiles</c> at.</summary>
    public string RootPath { get; }

    /// <summary>Creates the storage directory if it doesn't already exist.</summary>
    public MenuItemImageStorage(IWebHostEnvironment environment)
    {
        RootPath = Path.Combine(environment.ContentRootPath, "uploads", "menu-items");
        Directory.CreateDirectory(RootPath);
    }

    /// <summary>Checks an uploaded file's presence, size and declared content type before it's saved.</summary>
    public static Result Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return Result.Failure(Error.Validation("catalog.image_required", "No image file was uploaded."));
        }

        if (file.Length > MaxSizeBytes)
        {
            return Result.Failure(Error.Validation(
                "catalog.image_too_large", $"Image must be {MaxSizeBytes / 1024 / 1024}MB or smaller."));
        }

        if (!AllowedContentTypes.ContainsKey(file.ContentType))
        {
            return Result.Failure(Error.Validation(
                "catalog.invalid_image_type", "Image must be JPEG, PNG or WebP."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Saves an already-<see cref="Validate"/>d file under a fresh, unguessable
    /// name (never the caller's own filename — avoids both collisions and
    /// path-injection from a hostile one) and returns the URL it's served
    /// back at.
    /// </summary>
    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var extension = AllowedContentTypes[file.ContentType];
        var fileName = $"{Guid.CreateVersion7()}{extension}";
        var path = Path.Combine(RootPath, fileName);

        await using (var stream = File.Create(path))
        {
            await file.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        }

        return $"/uploads/menu-items/{fileName}";
    }

    /// <summary>
    /// Deletes the file a previously-issued URL points at, if any — safe to
    /// call with null or a URL whose file is already gone.
    /// <see cref="Path.GetFileName(string)"/> strips any directory
    /// component first, so even a maliciously-crafted <paramref name="imageUrl"/>
    /// can only ever resolve to a file directly under <see cref="RootPath"/>,
    /// never escape it.
    /// </summary>
    public void Delete(string? imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            return;
        }

        var path = Path.Combine(RootPath, Path.GetFileName(imageUrl));
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
