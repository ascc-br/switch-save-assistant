using SwitchSaveAssistant.Core.Safety;

namespace SwitchSaveAssistant.Tests;

public sealed class PathSafetyServiceTests
{
    [Fact]
    public void ValidNestedPath_RemainsInsideRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "SwitchSaveAssistant",
            "payload"
        );

        var result =
            PathSafetyService.ResolveChildPath(
                root,
                "folder/save.dat"
            );

        var expected = Path.GetFullPath(
            Path.Combine(
                root,
                "folder",
                "save.dat"
            )
        );

        Assert.Equal(
            expected,
            result
        );
    }

    [Fact]
    public void ParentTraversal_IsRejected()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "SwitchSaveAssistant",
            "payload"
        );

        Assert.Throws<InvalidDataException>(
            () =>
                PathSafetyService.ResolveChildPath(
                    root,
                    "../outside.dat"
                )
        );
    }

    [Fact]
    public void RootedPath_IsRejected()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "SwitchSaveAssistant",
            "payload"
        );

        var driveRoot =
            Path.GetPathRoot(
                Path.GetFullPath(root)
            )
            ?? @"C:\";

        var absolutePath = Path.Combine(
            driveRoot,
            "outside.dat"
        );

        Assert.Throws<InvalidDataException>(
            () =>
                PathSafetyService.ResolveChildPath(
                    root,
                    absolutePath
                )
        );
    }
}
