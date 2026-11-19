using SwitchSaveAssistant.Core.Sync;

namespace SwitchSaveAssistant.Tests;

public sealed class ManifestComparisonForAssistantTests
{
    [Fact]
    public void SnapshotsIguais_SaoSincronizados()
    {
        var service =
            new ManifestComparisonService();

        var left =
            new Dictionary<string, string>
            {
                ["save/a.bin"] = new string('a', 64),
                ["save/b.bin"] = new string('b', 64)
            };

        var right =
            new Dictionary<string, string>(
                left
            );

        var result =
            service.Compare(
                left,
                right
            );

        Assert.True(
            result.AreEqual
        );

        Assert.Empty(
            result.Changes
        );
    }

    [Fact]
    public void HashDiferente_EhDetectadoComoModificacao()
    {
        var service =
            new ManifestComparisonService();

        var left =
            new Dictionary<string, string>
            {
                ["save/a.bin"] = new string('a', 64)
            };

        var right =
            new Dictionary<string, string>
            {
                ["save/a.bin"] = new string('b', 64)
            };

        var result =
            service.Compare(
                left,
                right
            );

        Assert.False(
            result.AreEqual
        );

        var change =
            Assert.Single(
                result.Changes
            );

        Assert.Equal(
            "save/a.bin",
            change.RelativePath
        );
    }

    [Fact]
    public void ArquivoAusente_EhDetectado()
    {
        var service =
            new ManifestComparisonService();

        var left =
            new Dictionary<string, string>
            {
                ["save/a.bin"] = new string('a', 64),
                ["save/b.bin"] = new string('b', 64)
            };

        var right =
            new Dictionary<string, string>
            {
                ["save/a.bin"] = new string('a', 64)
            };

        var result =
            service.Compare(
                left,
                right
            );

        Assert.False(
            result.AreEqual
        );

        Assert.Contains(
            result.Changes,
            change =>
                change.RelativePath ==
                "save/b.bin"
        );
    }
}
