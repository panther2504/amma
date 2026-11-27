using System.Security.Cryptography;
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
    /// <summary>Base64 encoded 32-byte key. When set, files are encrypted at rest with AES-GCM.</summary>
    public string? EncryptionKey { get; set; }
}

/// <summary>
/// Local disk storage. Swap for a cloud blob store (S3 / Azure Blob) in production;
/// the interface stays the same.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private const int NonceSize = 12, TagSize = 16;
    private readonly string _root;
    private readonly byte[]? _key;

    public LocalFileStorage(IOptions<StorageOptions> options, ILogger<LocalFileStorage> log)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
        if (!string.IsNullOrWhiteSpace(options.Value.EncryptionKey))
        {
            _key = Convert.FromBase64String(options.Value.EncryptionKey);
            if (_key.Length != 32) throw new InvalidOperationException("Storage:EncryptionKey must be 32 bytes (base64).");
        }
        else
        {
            log.LogWarning("Storage:EncryptionKey is not set - medical documents are stored unencrypted. Set it before going live.");
        }
    }

    private string PathFor(string key)
    {
        if (key.Length != 32 || !key.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid storage key");
        return Path.Combine(_root, key[..2], key);
    }

    public async Task<string> SaveAsync(Stream content, CancellationToken ct)
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        if (_key is not null)
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cipher = new byte[bytes.Length];
            var tag = new byte[TagSize];
            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, bytes, cipher, tag);
            bytes = [.. nonce, .. tag, .. cipher];
        }
        await File.WriteAllBytesAsync(path, bytes, ct);
        return key;
    }

    public async Task<Stream> OpenAsync(string key, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(PathFor(key), ct);
        if (_key is null) return new MemoryStream(bytes);
        var nonce = bytes.AsSpan(0, NonceSize);
        var tag = bytes.AsSpan(NonceSize, TagSize);
        var cipher = bytes.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return new MemoryStream(plain);
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var p = PathFor(key);
        if (File.Exists(p)) File.Delete(p);
        return Task.CompletedTask;
    }
}
