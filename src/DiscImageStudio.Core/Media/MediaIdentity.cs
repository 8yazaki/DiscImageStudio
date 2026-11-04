using System.Text;

namespace DiscImageStudio.Core.Media;

public enum MediaFamily
{
    Unknown,
    Cd,
    Dvd,
    BluRay,
}

/// <summary>Which drive structure the raw bytes of an identity probe came from.</summary>
public enum MediaStructureKind
{
    None,
    /// <summary>READ DVD STRUCTURE format 0Eh: DVD-R/-RW pre-recorded information.</summary>
    DvdPreRecorded,
    /// <summary>READ DVD STRUCTURE format 11h: DVD+R/+RW ADIP information.</summary>
    DvdAdip,
    /// <summary>READ TOC/PMA/ATIP format 4: CD-R/-RW absolute time in pre-groove.</summary>
    CdAtip,
}

/// <summary>
/// Turns the raw manufacturer data a drive returns into a short, stable text the app can
/// compare and store. The text is deliberately lenient: the exact byte layout differs between
/// DVD-R, DVD+R and CD-R, so DVD structures are scanned for readable ASCII runs (the
/// manufacturer and product codes, such as <c>MCC 03RG</c>) instead of trusting fixed offsets.
/// The same disc model always yields the same text; that is all the matching needs.
/// </summary>
public static class MediaIdentity
{
    private const int MinimumRunLength = 5;
    private const int MinimumAlphanumeric = 3;
    private const int MaximumRuns = 3;
    private const int StructureHeaderLength = 4;

    public static string? FromStructure(MediaStructureKind kind, ReadOnlySpan<byte> data)
        => kind switch
        {
            MediaStructureKind.DvdPreRecorded or MediaStructureKind.DvdAdip
                => data.Length > StructureHeaderLength ? FromAscii(data[StructureHeaderLength..]) : null,
            MediaStructureKind.CdAtip => FromAtip(data),
            _ => null,
        };

    /// <summary>Readable ASCII runs of the payload, normalized and joined with " | ". Two runs
    /// split by a single unreadable byte are one code with a stray byte inside (a drive may put
    /// a separator in the middle of <c>CMC MAG. AM3</c>), so they are glued back together.</summary>
    public static string? FromAscii(ReadOnlySpan<byte> payload)
    {
        List<string> pieces = [];
        List<int> gaps = [];
        StringBuilder current = new();
        int gap = 0;
        for (int index = 0; index < payload.Length; index++)
        {
            byte value = payload[index];
            if (value is >= 0x20 and <= 0x7E)
            {
                if (current.Length == 0)
                {
                    gaps.Add(gap);
                    gap = 0;
                }

                current.Append((char)value);
                continue;
            }

            if (current.Length > 0)
            {
                pieces.Add(current.ToString());
                current.Clear();
            }

            gap++;
        }

        if (current.Length > 0)
        {
            pieces.Add(current.ToString());
        }

        List<string> merged = [];
        for (int index = 0; index < pieces.Count; index++)
        {
            if (index > 0 && gaps[index] == 1 && merged.Count > 0)
            {
                merged[^1] += pieces[index];
            }
            else
            {
                merged.Add(pieces[index]);
            }
        }

        List<string> runs = [];
        foreach (string piece in merged)
        {
            AddRun(runs, piece);
            if (runs.Count >= MaximumRuns)
            {
                break;
            }
        }

        return runs.Count == 0 ? null : string.Join(" | ", runs);
    }

    /// <summary>The CD-R lead-in start time identifies the dye/maker pair, for example
    /// <c>ATIP 97:24.01</c>. The ATIP descriptor holds it at bytes 8–10 behind a 4-byte header.</summary>
    public static string? FromAtip(ReadOnlySpan<byte> data)
    {
        if (data.Length < 11)
        {
            return null;
        }

        int minute = data[8], second = data[9], frame = data[10];
        if (minute is 0 or > 99 || second > 59 || frame > 74)
        {
            return null;
        }

        return $"ATIP {minute:00}:{second:00}.{frame:00}";
    }

    /// <summary>Upper-case, readable characters only, single spaces. Null and blank become empty.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        StringBuilder builder = new(text.Length);
        bool pendingSpace = false;
        foreach (char raw in text)
        {
            char c = char.ToUpperInvariant(raw);
            bool keep = c is >= 'A' and <= 'Z' or >= '0' and <= '9' or ':' or '.' or '+' or '-' or '/' or '_' or '|';
            if (keep)
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                pendingSpace = false;
                builder.Append(c);
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    private static void AddRun(List<string> runs, string run)
    {
        string normalized = Normalize(run);
        if (normalized.Length < MinimumRunLength
            || normalized.Count(char.IsLetterOrDigit) < MinimumAlphanumeric
            || runs.Contains(normalized))
        {
            return;
        }

        runs.Add(normalized);
    }
}
