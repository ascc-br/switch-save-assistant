using System.Text;
using SwitchSaveAssistant.Core.Models;

namespace SwitchSaveAssistant.Core.Backup;

public sealed class ManifestService
{
    public string Write(
        string destinationPath,
        IReadOnlyList<ManifestEntry> entries
    )
    {
        Directory.CreateDirectory(destinationPath);

        var manifestPath = Path.Combine(
            destinationPath,
            "manifest.sha256.txt"
        );

        var lines = entries.Select(entry =>
            $"{entry.Sha256}  {entry.RelativePath.Replace('\\', '/')}"
        );

        var content = string.Join('\n', lines);

        if (entries.Count > 0)
        {
            content += "\n";
        }

        File.WriteAllText(
            manifestPath,
            content,
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false
            )
        );

        return manifestPath;
    }
}
