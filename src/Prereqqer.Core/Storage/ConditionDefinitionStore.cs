using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using Prereqqer.Core.Definitions;

namespace Prereqqer.Core.Storage;

public sealed class ConditionDefinitionStore
{
    public ConditionDefinitionStore(string? path = null)
    {
        Path = path ?? GetDefaultPath();
    }

    public string Path { get; }

    public async Task<ConditionDefinitionDocument> LoadOrCreateAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path))
        {
            var document = DefinitionFactory.CreateDefaultDocument();
            await SaveAsync(document, cancellationToken).ConfigureAwait(false);
            return document;
        }

        return await LoadAsync(Path, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ConditionDefinitionDocument> LoadAsync(CancellationToken cancellationToken = default) =>
        await LoadAsync(Path, cancellationToken).ConfigureAwait(false);

    public async Task<ConditionDefinitionDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<ConditionDefinitionDocument>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        return DefinitionNormalizer.Normalize(document ?? DefinitionFactory.CreateDefaultDocument());
    }

    public async Task<ConditionDefinitionDocument> LoadFromUrlAsync(
        string url,
        int timeoutSeconds = 20,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Configuration URL must be an absolute HTTP or HTTPS URL.");
        }

        using var httpClient = new HttpClient();
        return await LoadFromUrlAsync(uri, httpClient, TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 300)), cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<ConditionDefinitionDocument> LoadFromUrlAsync(
        Uri uri,
        HttpClient httpClient,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Configuration URL must use HTTP or HTTPS.");
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Configuration URL timeout must be greater than zero.");
        }

        using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutTokenSource.CancelAfter(timeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.UserAgent.ParseAdd("Prereqqer/1.0");

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutTokenSource.Token).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutTokenSource.Token)
                .ConfigureAwait(false);
            var document = await JsonSerializer.DeserializeAsync<ConditionDefinitionDocument>(
                stream,
                JsonOptions,
                timeoutTokenSource.Token).ConfigureAwait(false);

            return DefinitionNormalizer.Normalize(document ?? DefinitionFactory.CreateDefaultDocument());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Configuration URL did not respond within {timeout.TotalSeconds:0} seconds.");
        }
    }

    public async Task SaveAsync(
        ConditionDefinitionDocument document,
        CancellationToken cancellationToken = default) =>
        await SaveAsync(document, Path, cancellationToken).ConfigureAwait(false);

    public async Task SaveAsync(
        ConditionDefinitionDocument document,
        string path,
        CancellationToken cancellationToken = default)
    {
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        document = DefinitionNormalizer.Normalize(document);

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public static string GetDefaultPath()
    {
        return GetDefaultPath(AppContext.BaseDirectory, RuntimeInformation.IsOSPlatform(OSPlatform.OSX));
    }

    public static string GetDefaultPath(string baseDirectory, bool isMacOs)
    {
        var applicationDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? AppContext.BaseDirectory
            : System.IO.Path.GetFullPath(baseDirectory);

        var directory = isMacOs && IsMacOsBundleMacOsDirectory(applicationDirectory)
            ? System.IO.Path.GetFullPath(System.IO.Path.Combine(applicationDirectory, "..", "Resources"))
            : applicationDirectory;

        return System.IO.Path.Combine(directory, "conditions.json");
    }

    private static bool IsMacOsBundleMacOsDirectory(string directory)
    {
        var directoryInfo = new DirectoryInfo(directory);
        var contentsDirectory = directoryInfo.Parent;
        var bundleDirectory = contentsDirectory?.Parent;

        return string.Equals(directoryInfo.Name, "MacOS", StringComparison.Ordinal)
            && string.Equals(contentsDirectory?.Name, "Contents", StringComparison.Ordinal)
            && string.Equals(bundleDirectory?.Extension, ".app", StringComparison.OrdinalIgnoreCase);
    }

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };
}
