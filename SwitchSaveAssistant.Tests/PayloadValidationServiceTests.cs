using SwitchSaveAssistant.Core.Hashing;
using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Sync;

namespace SwitchSaveAssistant.Tests;

public sealed class PayloadValidationServiceTests :
    IDisposable
{
    private readonly string _tempDirectory;

    public PayloadValidationServiceTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "SwitchSaveAssistant.Tests",
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(
            _tempDirectory
        );
    }

    [Fact]
    public void UnchangedPayload_IsValid()
    {
        var payload = CreatePayload();

        var file = Path.Combine(
            payload,
            "save.dat"
        );

        File.WriteAllText(
            file,
            "conteudo-original"
        );

        var manifest = CreateManifest(
            file
        );

        var result =
            new PayloadValidationService()
                .Validate(
                    payload,
                    manifest
                );

        Assert.True(
            result.AreEqual
        );

        Assert.Empty(
            result.Changes
        );
    }

    [Fact]
    public void ModifiedPayload_IsDetected()
    {
        var payload = CreatePayload();

        var file = Path.Combine(
            payload,
            "save.dat"
        );

        File.WriteAllText(
            file,
            "conteudo-original"
        );

        var manifest = CreateManifest(
            file
        );

        File.AppendAllText(
            file,
            "-alterado"
        );

        var result =
            new PayloadValidationService()
                .Validate(
                    payload,
                    manifest
                );

        Assert.False(
            result.AreEqual
        );

        var change = Assert.Single(
            result.Changes
        );

        Assert.Equal(
            ManifestChangeType.Modified,
            change.ChangeType
        );

        Assert.Equal(
            "save.dat",
            change.RelativePath
        );
    }

    private string CreatePayload()
    {
        var payload = Path.Combine(
            _tempDirectory,
            "payload"
        );

        Directory.CreateDirectory(
            payload
        );

        return payload;
    }

    private string CreateManifest(
        string file
    )
    {
        var sha256 =
            new Sha256Service()
                .CalculateFile(
                    file
                );

        var manifest = Path.Combine(
            _tempDirectory,
            "manifest.sha256.txt"
        );

        File.WriteAllText(
            manifest,
            $"{sha256}  save.dat\n"
        );

        return manifest;
    }

    public void Dispose()
    {
        if (Directory.Exists(
                _tempDirectory
            ))
        {
            Directory.Delete(
                _tempDirectory,
                recursive: true
            );
        }
    }
}
