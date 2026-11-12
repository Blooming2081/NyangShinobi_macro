using System.Numerics;
using System.Text.RegularExpressions;

namespace NyangShinobi.App.Updates;

public sealed class SemVersion : IComparable<SemVersion>
{
    private static readonly Regex Pattern = new(@"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private readonly BigInteger[] core;
    private readonly string[] prerelease;
    private SemVersion(string value, Match match)
    {
        Value = value;
        core = Enumerable.Range(1, 3).Select(index => BigInteger.Parse(match.Groups[index].Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        prerelease = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : [];
    }
    public string Value { get; }
    public bool IsPrerelease => prerelease.Length != 0;
    public static bool TryParse(string? value, out SemVersion version)
    {
        version = null!;
        if (value is null || value.Length > 200) return false;
        var match = Pattern.Match(value);
        if (!match.Success || (match.Groups[4].Success && match.Groups[4].Value.Split('.').Any(id =>
                id.Length > 1 && id[0] == '0' && id.All(char.IsAsciiDigit)))) return false;
        version = new(value, match);
        return true;
    }
    public static SemVersion Parse(string value) => TryParse(value, out var version) ? version : throw new FormatException($"SemVer 형식이 아닙니다: {value}");
    public int CompareTo(SemVersion? other)
    {
        if (other is null) return 1;
        for (var index = 0; index < 3; index++)
        { var result = core[index].CompareTo(other.core[index]); if (result != 0) return result; }
        if (!IsPrerelease || !other.IsPrerelease) return IsPrerelease == other.IsPrerelease ? 0 : IsPrerelease ? -1 : 1;
        for (var index = 0; index < Math.Min(prerelease.Length, other.prerelease.Length); index++)
        {
            var a = prerelease[index]; var b = other.prerelease[index];
            var numericA = a.All(char.IsAsciiDigit); var numericB = b.All(char.IsAsciiDigit);
            var result = numericA && numericB ? BigInteger.Parse(a).CompareTo(BigInteger.Parse(b)) :
                numericA != numericB ? numericA ? -1 : 1 : string.CompareOrdinal(a, b);
            if (result != 0) return result;
        }
        return prerelease.Length.CompareTo(other.prerelease.Length);
    }
    public override string ToString() => Value;
}
