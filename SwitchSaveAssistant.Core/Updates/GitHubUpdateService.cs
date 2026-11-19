using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace SwitchSaveAssistant.Core.Updates;

public enum UpdatePackageKind { Installer, Portable }
public sealed record UpdateAsset(string Name, Uri Url, long Size, string? Digest);
public sealed record AvailableUpdate(string Version, string Tag, UpdateAsset Package, UpdateAsset? Checksums);
public sealed record UpdateCheckResult(AvailableUpdate? Update, bool HasPublishedPackages);

public sealed class GitHubUpdateService(HttpClient client)
{
    public const string Repository = "ascc-br/switch-save-assistant";
    public const string ReleasesPage = "https://github.com/" + Repository + "/releases";
    private const long MaxPackageSize = 512L * 1024 * 1024;

    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, UpdatePackageKind kind, CancellationToken cancellationToken = default)
    {
        if (!ReleaseVersion.TryParse(currentVersion, out var current)) throw new InvalidDataException("Invalid current version.");
        AvailableUpdate? best = null;
        ReleaseVersion? bestVersion = null;
        var published = false;
        // Tags alone do not constitute releases. Include prereleases only for preview clients.
        for (var page = 1; page <= 10; page++)
        {
            using var request = Request($"https://api.github.com/repos/{Repository}/releases?per_page=100&page={page}");
            using var response = await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await ReadLimitedAsync(response, 8 * 1024 * 1024, cancellationToken);
            using var document = JsonDocument.Parse(json);
            var releases = document.RootElement;
            if (releases.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid releases response.");
            foreach (var release in releases.EnumerateArray())
            {
                if (Flag(release, "draft")) continue;
                var tag = Text(release, "tag_name");
                if (!ReleaseVersion.TryParse(tag, out var version) ||
                    (!current.IsPrerelease && (version.IsPrerelease || Flag(release, "prerelease")))) continue;
                var name = $"SwitchSaveAssistant-{(kind == UpdatePackageKind.Installer ? "Setup" : "Portable")}-{version.Value}-win-x64.{(kind == UpdatePackageKind.Installer ? "exe" : "zip")}";
                var package = Asset(release, tag!, name);
                if (package is null || package.Size <= 0 || package.Size > MaxPackageSize) continue;
                var checksums = Asset(release, tag!, "SHA256SUMS.txt");
                if (!ValidHash(package.Digest) && checksums is null) continue;
                published = true;
                if (version.CompareTo(current) > 0 && (bestVersion is null || version.CompareTo(bestVersion) > 0))
                {
                    bestVersion = version;
                    best = new AvailableUpdate(version.Value, tag!, package, checksums);
                }
            }
            if (releases.GetArrayLength() < 100) break;
            if (page == 10) throw new InvalidDataException("Too many release pages.");
        }
        return new UpdateCheckResult(best, published);
    }

    public async Task<string> DownloadAsync(AvailableUpdate update, string destinationRoot, CancellationToken cancellationToken = default)
    {
        if (!TrustedUrl(update.Package.Url, update.Tag, update.Package.Name) ||
            update.Package.Size <= 0 || update.Package.Size > MaxPackageSize) throw new InvalidDataException("Invalid package.");
        var expectedHash = update.Package.Digest;
        if (!ValidHash(expectedHash))
        {
            var sums = update.Checksums ?? throw new InvalidDataException("Missing checksum.");
            if (!TrustedUrl(sums.Url, update.Tag, "SHA256SUMS.txt")) throw new InvalidDataException("Invalid checksum URL.");
            using var sumRequest = Request(sums.Url.AbsoluteUri);
            using var sumResponse = await client.SendAsync(sumRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            sumResponse.EnsureSuccessStatusCode();
            var content = System.Text.Encoding.UTF8.GetString(await ReadLimitedAsync(sumResponse, 64 * 1024, cancellationToken));
            var hashes = content.Split('\n').Select(line => line.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries))
                .Where(parts => parts.Length == 2 && parts[1].TrimStart('*') == update.Package.Name)
                .Select(parts => parts[0]).ToArray();
            if (hashes.Length != 1 || !ValidHash(hashes[0])) throw new InvalidDataException("Missing or ambiguous package checksum.");
            expectedHash = hashes[0];
        }
        var folder = Path.Combine(destinationRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, update.Package.Name);
        var partial = path + ".partial";
        try
        {
            using var request = Request(update.Package.Url.AbsoluteUri);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != update.Package.Size)
                throw new InvalidDataException("Package length mismatch.");
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    total += count;
                    if (total > update.Package.Size) throw new InvalidDataException("Package exceeds expected size.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
                if (total != update.Package.Size) throw new InvalidDataException("Incomplete package.");
            }
            await using (var file = File.OpenRead(partial))
            {
                var actual = await SHA256.HashDataAsync(file, cancellationToken);
                if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expectedHash!.Replace("sha256:", "", StringComparison.Ordinal))))
                    throw new InvalidDataException("Package checksum mismatch.");
            }
            File.Move(partial, path);
            return path;
        }
        catch
        {
            if (File.Exists(partial)) File.Delete(partial);
            if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            throw;
        }
    }

    private static bool TrustedUrl(Uri uri, string tag, string name)
        => uri.Scheme == Uri.UriSchemeHttps && uri.Host == "github.com" && uri.IsDefaultPort &&
           string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
           uri.AbsolutePath == $"/{Repository}/releases/download/{Uri.EscapeDataString(tag)}/{Uri.EscapeDataString(name)}" &&
           !name.Contains('/') && !name.Contains('\\') && name != "." && name != "..";

    private static UpdateAsset? Asset(JsonElement release, string tag, string name)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        var found = assets.EnumerateArray().Where(a => Text(a, "name") == name && Text(a, "state") == "uploaded").ToArray();
        if (found.Length != 1) return null;
        var a = found[0];
        if (!Uri.TryCreate(Text(a, "browser_download_url"), UriKind.Absolute, out var uri) || !TrustedUrl(uri, tag, name) ||
            !a.TryGetProperty("size", out var size) || !size.TryGetInt64(out var bytes)) return null;
        return new UpdateAsset(name, uri, bytes, Text(a, "digest"));
    }

    private static string? Text(JsonElement element, string key)
        => element.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static bool Flag(JsonElement element, string key)
        => element.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.True;
    private static bool ValidHash(string? hash)
    {
        if (hash?.StartsWith("sha256:", StringComparison.Ordinal) == true) hash = hash[7..];
        return hash?.Length == 64 && hash.All(Uri.IsHexDigit);
    }
    private static HttpRequestMessage Request(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("SwitchSaveAssistant-UpdateChecker/1.0");
        if (request.RequestUri!.Host == "api.github.com")
        {
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        }
        return request;
    }
    private static async Task<byte[]> ReadLimitedAsync(HttpResponseMessage response, int limit, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (output.Length + count > limit) throw new InvalidDataException("Response too large.");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}
