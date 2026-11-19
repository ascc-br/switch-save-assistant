using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace SwitchSaveAssistant.Core.Updates;

public sealed partial class ReleaseVersion : IComparable<ReleaseVersion>
{
    private readonly BigInteger[] _numbers;
    private readonly string[] _preview;
    public string Value { get; }
    public bool IsPrerelease => _preview.Length > 0;

    private ReleaseVersion(string value, BigInteger[] numbers, string[] preview)
        => (Value, _numbers, _preview) = (value, numbers, preview);

    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = null!;
        if (text is null || text.Length > 200) return false;
        var match = VersionPattern().Match(text.Trim());
        if (!match.Success) return false;
        var preview = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : [];
        if (preview.Any(p => IsNumeric(p) && p.Length > 1 && p[0] == '0')) return false;
        version = new ReleaseVersion(match.Value.TrimStart('v', 'V').Split('+')[0],
            Enumerable.Range(1, 3).Select(i => BigInteger.Parse(match.Groups[i].Value, CultureInfo.InvariantCulture)).ToArray(), preview);
        return true;
    }

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;
        for (var i = 0; i < 3; i++)
        {
            var result = _numbers[i].CompareTo(other._numbers[i]);
            if (result != 0) return result;
        }
        if (!IsPrerelease || !other.IsPrerelease)
            return IsPrerelease == other.IsPrerelease ? 0 : IsPrerelease ? -1 : 1;
        for (var i = 0; i < Math.Min(_preview.Length, other._preview.Length); i++)
        {
            var a = _preview[i];
            var b = other._preview[i];
            var na = IsNumeric(a);
            var nb = IsNumeric(b);
            var result = na && nb ? BigInteger.Parse(a, CultureInfo.InvariantCulture).CompareTo(BigInteger.Parse(b, CultureInfo.InvariantCulture))
                : na != nb ? (na ? -1 : 1) : string.CompareOrdinal(a, b);
            if (result != 0) return result;
        }
        return _preview.Length.CompareTo(other._preview.Length);
    }

    private static bool IsNumeric(string value) => value.All(c => c is >= '0' and <= '9');

    [GeneratedRegex(@"^[vV]?(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
