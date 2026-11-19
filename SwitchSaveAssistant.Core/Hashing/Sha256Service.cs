using System.Security.Cryptography;

namespace SwitchSaveAssistant.Core.Hashing;

public sealed class Sha256Service
{
    public string CalculateFile(string path)
    {
        using var stream = File.OpenRead(path);

        var hash = SHA256.HashData(stream);

        return Convert
            .ToHexString(hash)
            .ToLowerInvariant();
    }
}
