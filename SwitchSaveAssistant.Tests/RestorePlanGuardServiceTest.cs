using SwitchSaveAssistant.Core.Models;
using SwitchSaveAssistant.Core.Restore;
using Xunit;

namespace SwitchSaveAssistant.Tests;

public sealed class RestorePlanGuardServiceTest
{
    private readonly RestorePlanGuardService _service =
        new();

    [Fact]
    public void MesmoConjuntoDeArquivos_DeveSerAceito()
    {
        var expected =
            new Dictionary<string, string>(
                StringComparer.Ordinal
            )
            {
                ["a.bin"] = Sha('a'),
                ["dir/b.bin"] = Sha('b')
            };

        var target =
            new Dictionary<string, string>(
                StringComparer.Ordinal
            )
            {
                ["a.bin"] = Sha('c'),
                ["dir/b.bin"] = Sha('d')
            };

        Assert.True(
            _service.HasSameFileSet(
                target,
                expected
            )
        );
    }

    [Fact]
    public void ArquivoAdicionado_DeveSerBloqueado()
    {
        var expected =
            new Dictionary<string, string>
            {
                ["a.bin"] = Sha('a')
            };

        var target =
            new Dictionary<string, string>
            {
                ["a.bin"] = Sha('b'),
                ["novo.bin"] = Sha('c')
            };

        Assert.False(
            _service.HasSameFileSet(
                target,
                expected
            )
        );
    }

    [Fact]
    public void ArquivoRemovido_DeveSerBloqueado()
    {
        var expected =
            new Dictionary<string, string>
            {
                ["a.bin"] = Sha('a'),
                ["b.bin"] = Sha('b')
            };

        var target =
            new Dictionary<string, string>
            {
                ["a.bin"] = Sha('c')
            };

        Assert.False(
            _service.HasSameFileSet(
                target,
                expected
            )
        );
    }

    [Fact]
    public void CategoriaArbitraria_DeveSerBloqueada()
    {
        var plan =
            CreatePlan() with
            {
                Category = "1: SD Card"
            };

        Assert.Throws<InvalidOperationException>(
            () =>
                _service.Validate(
                    plan
                )
        );
    }

    [Fact]
    public void TraversalNoNomeDoJogo_DeveSerBloqueado()
    {
        var plan =
            CreatePlan() with
            {
                GameName = @"..\Outro"
            };

        Assert.Throws<InvalidOperationException>(
            () =>
                _service.Validate(
                    plan
                )
        );
    }

    [Fact]
    public void TraversalNoExpectedSwitchFiles_DeveSerBloqueado()
    {
        var plan =
            CreatePlan() with
            {
                ExpectedSwitchFiles =
                    new[]
                    {
                        new SyncFileState(
                            "../save.bin",
                            Sha('a')
                        )
                    }
            };

        Assert.Throws<InvalidOperationException>(
            () =>
                _service.Validate(
                    plan
                )
        );
    }

    [Fact]
    public void ShaInvalido_DeveSerBloqueado()
    {
        var plan =
            CreatePlan() with
            {
                ExpectedSwitchFiles =
                    new[]
                    {
                        new SyncFileState(
                            "save.bin",
                            "sha-invalido"
                        )
                    }
            };

        Assert.Throws<InvalidOperationException>(
            () =>
                _service.Validate(
                    plan
                )
        );
    }

    private static RestorePlan CreatePlan()
    {
        var root =
            Path.GetFullPath(
                Path.Combine(
                    Path.GetTempPath(),
                    "SwitchSaveAssistant-Test"
                )
            );

        return new RestorePlan(
            "Installed games",
            "Game Test",
            "Alfran",
            SaveEntryType.User,
            Path.Combine(
                root,
                "payload"
            ),
            Path.Combine(
                root,
                "manifest.sha256.txt"
            ),
            Path.Combine(
                root,
                "safety"
            ),
            Path.Combine(
                root,
                "safety",
                "manifest.sha256.txt"
            ),
            DateTimeOffset.UtcNow,
            new[]
            {
                new SyncFileState(
                    "save.bin",
                    Sha('a')
                )
            }
        );
    }

    private static string Sha(
        char character
    )
    {
        return new string(
            character,
            64
        );
    }
}
