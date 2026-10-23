using System.Globalization;
using System.Text.RegularExpressions;
using CreditFlow.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CreditFlow.Infrastructure.Storage;

/// <summary>Section <c>Storage</c>.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Dossier racine des pièces (absolu, ou relatif au dossier de contenu de l'application). Jamais sous wwwroot.</summary>
    public string RootPath { get; set; } = "App_Data/uploads";
}

/// <summary>
/// Stockage sur disque local : clés <c>AAAA/MM/&lt;guid&gt;</c> générées aléatoirement, aucune extension ni nom fourni
/// par le client ; toute clé non conforme est refusée (pas de traversée de répertoire).
/// </summary>
internal sealed partial class LocalFileStorage : IFileStorage
{
    private readonly string _root;
    private readonly TimeProvider _clock;

    public LocalFileStorage(IOptions<StorageOptions> options, IHostEnvironment environment, TimeProvider clock)
    {
        var configured = options.Value.RootPath;
        _root = Path.GetFullPath(Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured));
        _clock = clock;
    }

    public async Task<string> SaveAsync(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var now = _clock.GetUtcNow();
        var key = string.Create(CultureInfo.InvariantCulture, $"{now:yyyy}/{now:MM}/{Guid.NewGuid():N}");
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = Resolve(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = Resolve(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string Resolve(string key)
    {
        if (string.IsNullOrEmpty(key) || !KeyRegex().IsMatch(key))
        {
            throw new ArgumentException("Clé de stockage invalide.", nameof(key));
        }

        var path = Path.GetFullPath(Path.Combine(_root, key));
        return path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? path
            : throw new ArgumentException("Clé de stockage invalide.", nameof(key));
    }

    [GeneratedRegex("^[0-9]{4}/[0-9]{2}/[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();
}
