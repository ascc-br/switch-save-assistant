using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SwitchSaveAssistant.Core.Settings;
using SwitchSaveAssistant.Core.Updates;

namespace SwitchSaveAssistant.Tests;

public sealed class UpdateServiceTests
{
    [Theory]
    [InlineData("0.1.0-preview.10", "0.1.0-preview.2", 1)]
    [InlineData("v0.1.0", "0.1.0-preview.99", 1)]
    [InlineData("1.10.0", "1.9.9", 1)]
    [InlineData("1.0.0+abc", "1.0.0+def", 0)]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1", -1)]
    [InlineData("1.0.0-1", "1.0.0-alpha", -1)]
    public void ComparesSemanticVersions(string left, string right, int expected)
    {
        Assert.True(ReleaseVersion.TryParse(left, out var a));
        Assert.True(ReleaseVersion.TryParse(right, out var b));
        Assert.Equal(expected, Math.Sign(a.CompareTo(b)));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-preview.01")]
    [InlineData("1.0.0-")]
    [InlineData("1.0.0/path")]
    [InlineData("1.0.0-β")]
    public void RejectsMalformedVersions(string value) => Assert.False(ReleaseVersion.TryParse(value, out _));

    private static object Release(string version, string kind = "Setup", bool draft = false, bool preview = false, string? digest = null, string? url = null)
    {
        var name = $"SwitchSaveAssistant-{kind}-{version}-win-x64.{(kind == "Setup" ? "exe" : "zip")}";
        return new { tag_name = "v" + version, draft, prerelease = preview, assets = new object[] {
            new { name, state = "uploaded", size = 4L, digest = digest ?? "sha256:" + new string('a', 64),
                browser_download_url = url ?? $"https://github.com/{GitHubUpdateService.Repository}/releases/download/v{version}/{name}" }
        }};
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };
    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response) => new(new Handler(response));

    [Fact]
    public async Task ChoosesHighestSemanticVersionIncludingPreviewForPreviewClients()
    {
        using var client = Client(_ => Json(new[] { Release("0.1.0-preview.2"), Release("0.1.0-preview.10"), Release("0.1.0-preview.3") }));
        var result = await new GitHubUpdateService(client).CheckAsync("0.1.0-preview.1", UpdatePackageKind.Installer);
        Assert.Equal("0.1.0-preview.10", result.Update?.Version);
    }

    [Fact]
    public async Task StableClientsSkipPreviewAndDraftAndOlderVersions()
    {
        using var client = Client(_ => Json(new[] { Release("9.0.0", draft: true), Release("8.0.0-preview.1"), Release("7.0.0", preview: true), Release("1.0.0") }));
        var result = await new GitHubUpdateService(client).CheckAsync("1.0.0", UpdatePackageKind.Installer);
        Assert.Null(result.Update);
        Assert.True(result.HasPublishedPackages);
    }

    [Fact]
    public async Task PreviewClientsCanMoveToStable()
    {
        using var client = Client(_ => Json(new[] { Release("0.1.0") }));
        Assert.Equal("0.1.0", (await new GitHubUpdateService(client).CheckAsync("0.1.0-preview.2", UpdatePackageKind.Installer)).Update?.Version);
    }

    [Fact]
    public async Task PortableUsesZipAndNoReleasesIsNotCurrentLatest()
    {
        using var client = Client(_ => Json(new[] { Release("0.2.0") }));
        var result = await new GitHubUpdateService(client).CheckAsync("0.1.0", UpdatePackageKind.Portable);
        Assert.Null(result.Update);
        Assert.False(result.HasPublishedPackages);
        using var zipClient = Client(_ => Json(new[] { Release("0.2.0", "Portable") }));
        Assert.EndsWith(".zip", (await new GitHubUpdateService(zipClient).CheckAsync("0.1.0", UpdatePackageKind.Portable)).Update!.Package.Name);
    }

    [Theory]
    [InlineData("https://evil.example/setup.exe")]
    [InlineData("http://github.com/ascc-br/switch-save-assistant/releases/download/v0.2.0/a.exe")]
    [InlineData("https://github.com/another/project/releases/download/v0.2.0/a.exe")]
    public async Task RejectsForeignAssets(string url)
    {
        using var client = Client(_ => Json(new[] { Release("0.2.0", url: url) }));
        Assert.Null((await new GitHubUpdateService(client).CheckAsync("0.1.0", UpdatePackageKind.Installer)).Update);
    }

    [Fact]
    public async Task FollowsPaginationAndSendsPublicApiHeaders()
    {
        using var client = Client(request => {
            Assert.NotEmpty(request.Headers.UserAgent);
            Assert.True(request.Headers.Contains("X-GitHub-Api-Version"));
            return Json(request.RequestUri!.Query.EndsWith("page=1") ? Enumerable.Range(0, 100).Select(_ => Release("0.1.0")).ToArray() : new[] { Release("0.2.0") });
        });
        Assert.Equal("0.2.0", (await new GitHubUpdateService(client).CheckAsync("0.1.0", UpdatePackageKind.Installer)).Update?.Version);
    }

    [Fact]
    public async Task RateLimitIsNotReportedAsUpToDate()
    {
        using var client = Client(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        await Assert.ThrowsAsync<HttpRequestException>(() => new GitHubUpdateService(client).CheckAsync("0.1.0", UpdatePackageKind.Installer));
    }

    [Fact]
    public async Task MissingDigestAndChecksumDoesNotOfferUnverifiableUpdate()
    {
        using var client = Client(_ => Json(new[] { Release("0.2.0", digest: "invalid") }));
        Assert.Null((await new GitHubUpdateService(client).CheckAsync("0.1.0", UpdatePackageKind.Installer)).Update);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DownloadsVerifiedPackageUsingDigestOrChecksumFile(bool fallback)
    {
        var data = Encoding.UTF8.GetBytes("TEST");
        var hash = Convert.ToHexString(SHA256.HashData(data));
        var package = new UpdateAsset("SwitchSaveAssistant-Setup-0.2.0-win-x64.exe", new Uri($"https://github.com/{GitHubUpdateService.Repository}/releases/download/v0.2.0/SwitchSaveAssistant-Setup-0.2.0-win-x64.exe"), 4, fallback ? null : "sha256:" + hash);
        var sums = new UpdateAsset("SHA256SUMS.txt", new Uri($"https://github.com/{GitHubUpdateService.Repository}/releases/download/v0.2.0/SHA256SUMS.txt"), 100, null);
        using var client = Client(request => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = request.RequestUri!.AbsolutePath.EndsWith(".txt") ? new StringContent(hash + "  " + package.Name) : new ByteArrayContent(data)
        });
        var root = Directory.CreateTempSubdirectory("SSA-Updates-").FullName;
        try {
            var path = await new GitHubUpdateService(client).DownloadAsync(new("0.2.0", "v0.2.0", package, sums), root);
            Assert.Equal(data, await File.ReadAllBytesAsync(path));
            Assert.Empty(Directory.GetFiles(root, "*.partial", SearchOption.AllDirectories));
        } finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(3, true)]
    public async Task RejectsCorruptionOrLengthMismatchAndCleansPartialFiles(int size, bool correctHash)
    {
        var data = Encoding.UTF8.GetBytes("TEST");
        var hash = correctHash ? Convert.ToHexString(SHA256.HashData(data)) : new string('a', 64);
        var asset = new UpdateAsset("test.exe", new Uri($"https://github.com/{GitHubUpdateService.Repository}/releases/download/v0.2.0/test.exe"), size, hash);
        using var client = Client(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });
        var root = Directory.CreateTempSubdirectory("SSA-Updates-").FullName;
        try {
            await Assert.ThrowsAsync<InvalidDataException>(() => new GitHubUpdateService(client).DownloadAsync(new("0.2.0", "v0.2.0", asset, null), root));
            Assert.Empty(Directory.GetFileSystemEntries(root));
        } finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ExistingSettingsDefaultToEnabledAndExplicitFalseSurvives()
    {
        var root = Directory.CreateTempSubdirectory("SSA-UpdateSettings-").FullName;
        try {
            var path = Path.Combine(root, "settings.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new { FolderPath = @"D:\BACKUPS" }));
            var service = new AssistantSettingsService(path);
            Assert.True(service.Load().CheckForUpdatesOnStartup);
            service.Update(s => s with { CheckForUpdatesOnStartup = false });
            Assert.False(service.Load().CheckForUpdatesOnStartup);
            Assert.Equal(@"D:\BACKUPS", service.Load().FolderPath);
        } finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task CancellationDoesNotLeavePartialDownload()
    {
        using var client = Client(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3, 4]) });
        var asset = new UpdateAsset("test.exe", new Uri($"https://github.com/{GitHubUpdateService.Repository}/releases/download/v0.2.0/test.exe"), 4, new string('a', 64));
        var root = Directory.CreateTempSubdirectory("SSA-UpdateCancel-").FullName;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        try {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new GitHubUpdateService(client).DownloadAsync(new("0.2.0", "v0.2.0", asset, null), root, cancellation.Token));
            Assert.Empty(Directory.GetFileSystemEntries(root));
        } finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task EmptyReleasesHaveAnExplicitState()
    {
        using var client = Client(_ => Json(Array.Empty<object>()));
        var result = await new GitHubUpdateService(client).CheckAsync("0.1.0", UpdatePackageKind.Installer);
        Assert.False(result.HasPublishedPackages);
        Assert.Null(result.Update);
    }

    [Fact]
    public async Task BadChecksumFilePreventsPackageDownload()
    {
        var asset = new UpdateAsset("test.exe", new Uri($"https://github.com/{GitHubUpdateService.Repository}/releases/download/v0.2.0/test.exe"), 4, null);
        var sums = new UpdateAsset("SHA256SUMS.txt", new Uri($"https://github.com/{GitHubUpdateService.Repository}/releases/download/v0.2.0/SHA256SUMS.txt"), 80, null);
        using var client = Client(request => {
            Assert.EndsWith("SHA256SUMS.txt", request.RequestUri!.AbsolutePath);
            return new(HttpStatusCode.OK) { Content = new StringContent(new string('a', 64) + "  wrong.exe") };
        });
        var root = Directory.CreateTempSubdirectory("SSA-UpdateChecksum-").FullName;
        try {
            await Assert.ThrowsAsync<InvalidDataException>(() => new GitHubUpdateService(client).DownloadAsync(new("0.2.0", "v0.2.0", asset, sums), root));
            Assert.Empty(Directory.GetFileSystemEntries(root));
        } finally { Directory.Delete(root, true); }
    }
}
