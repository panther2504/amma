using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Amma.Infrastructure.Storage;

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, CancellationToken ct);
    Task<Stream> OpenAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

public class StorageOptions
{
    public string RootPath { get; set; } = "data/files";
    /// <summary>"Local" (disk, default) or "Database" (PostgreSQL).</summary>
    public string Provider { get; set; } = "Local";
    /// <summary>Base64 encoded 32-byte key. When set, files are encrypted at rest with AES-GCM.</summary>
    public string? EncryptionKey { get; set; }
}

/// <summary>AES-GCM encryption at rest when Storage:EncryptionKey is set.</summary>
public class FileCrypto
{
    private const int NonceSize = 12, TagSize = 16;
    private readonly byte[]? _key;

    public FileCrypto(IOptions<StorageOptions> options, ILogger<FileCrypto> log)
    {
        if (!string.IsNullOrWhiteSpace(options.Value.EncryptionKey))
        {
            // A base64 32-byte key is used as is; any other secret (min. 16 chars) is turned into one with SHA-256.
            var raw = options.Value.EncryptionKey.Trim();
            byte[]? decoded = null;
            try { decoded = Convert.FromBase64String(raw); } catch (FormatException) { }
            if (decoded is { Length: 32 }) _key = decoded;
            else if (raw.Length >= 16) _key = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
            else throw new InvalidOperationException("Storage:EncryptionKey must be a base64 32-byte key or a secret of at least 16 characters.");
        }
        else log.LogWarning("Storage:EncryptionKey is not set - medical documents are stored unencrypted. Set it before going live.");
    }

    public byte[] Protect(byte[] plain)
    {
        if (_key is null) return plain;
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag);
        return [.. nonce, .. tag, .. cipher];
    }

    public byte[] Unprotect(byte[] stored)
    {
        if (_key is null) return stored;
        var plain = new byte[stored.Length - NonceSize - TagSize];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(stored.AsSpan(0, NonceSize), stored.AsSpan(NonceSize + TagSize), stored.AsSpan(NonceSize, TagSize), plain);
        return plain;
    }

    public static string NewKey() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
}

/// <summary>Local disk storage (use a persistent disk or a cloud blob store in production).</summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly FileCrypto _crypto;

    public LocalFileStorage(IOptions<StorageOptions> options, FileCrypto crypto)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        _crypto = crypto;
        Directory.CreateDirectory(_root);
    }

    private string PathFor(string key)
    {
        if (key.Length != 32 || !key.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid storage key");
        return Path.Combine(_root, key[..2], key);
    }

    public async Task<string> SaveAsync(Stream content, CancellationToken ct)
    {
        var key = FileCrypto.NewKey();
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        await File.WriteAllBytesAsync(path, _crypto.Protect(ms.ToArray()), ct);
        return key;
    }

    public async Task<Stream> OpenAsync(string key, CancellationToken ct) =>
        new MemoryStream(_crypto.Unprotect(await File.ReadAllBytesAsync(PathFor(key), ct)));

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var p = PathFor(key);
        if (File.Exists(p)) File.Delete(p);
        return Task.CompletedTask;
    }
}

/// <summary>Stores document bytes in PostgreSQL. Simple and durable for small private deployments.</summary>
public class DatabaseFileStorage(Persistence.AmmaDbContext db, FileCrypto crypto) : IFileStorage
{
    public async Task<string> SaveAsync(Stream content, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var file = new StoredFile { Key = FileCrypto.NewKey(), Data = crypto.Protect(ms.ToArray()) };
        db.StoredFiles.Add(file);
        await db.SaveChangesAsync(ct);
        return file.Key;
    }

    public async Task<Stream> OpenAsync(string key, CancellationToken ct)
    {
        var f = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct) ?? throw new FileNotFoundException(key);
        return new MemoryStream(crypto.Unprotect(f.Data));
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        var f = await db.StoredFiles.FirstOrDefaultAsync(x => x.Key == key, ct);
        if (f is not null) { db.StoredFiles.Remove(f); await db.SaveChangesAsync(ct); }
    }
}

public class StoredFile
{
    public string Key { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
