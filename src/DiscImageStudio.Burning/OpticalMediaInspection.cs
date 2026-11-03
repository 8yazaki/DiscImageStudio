using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace DiscImageStudio.Burning;

public enum OpticalMediaFamily
{
    Unknown,
    Cd,
    Dvd,
    BluRay,
}

/// <summary>Which drive structure <see cref="OpticalMediaInfo.Structure"/> holds.</summary>
public enum OpticalMediaStructure
{
    None,
    DvdPreRecorded,
    DvdAdip,
    CdAtip,
}

/// <summary>What the drive reports about the inserted disc. The identity bytes are returned raw;
/// turning them into a comparable text is the caller's job.</summary>
public sealed record OpticalMediaInfo(
    bool HasDisc,
    OpticalMediaFamily Family,
    string TypeName,
    bool IsBlank,
    long? FreeSectors,
    long? TotalSectors,
    OpticalMediaStructure StructureKind,
    byte[]? Structure)
{
    public static OpticalMediaInfo Empty { get; } = new(
        false, OpticalMediaFamily.Unknown, string.Empty, false, null, null, OpticalMediaStructure.None, null);
}

public interface IOpticalMediaInspector
{
    Task<OpticalMediaInfo> InspectMediaAsync(
        string deviceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only inspection of the loaded disc: type, blank state, capacity, and the identity data
/// that is recorded on the disc by its maker. It issues no write, format or erase command. The
/// identity read is best effort: drives differ, and a failed read only means "identity unknown".
/// </summary>
internal static class ImapiMediaInspector
{
    // IMAPI_MEDIA_PHYSICAL_TYPE (imapi2.h).
    private const int CdRecordable = 0x2;
    private const int CdRewritable = 0x3;
    private const int DvdPlusRecordable = 0x6;
    private const int DvdPlusRecordableDualLayer = 0x8;
    private const int DvdDashRecordable = 0x9;
    private const int DvdDashRecordableDualLayer = 0xB;
    private const int BlankMediaState = 0x2;
    private const byte ReadDvdStructureOpcode = 0xAD;
    private const byte ReadTocPmaAtipOpcode = 0x43;
    private const byte DvdPreRecordedFormat = 0x0E;
    private const byte DvdAdipFormat = 0x11;
    private const int MaximumStructureBytes = 2052;
    private const uint CommandTimeoutSeconds = 15;

    internal static OpticalMediaInfo Inspect(string deviceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        dynamic recorder = WindowsImapiBurner.CreateRecorder(deviceId);
        dynamic? format = null;
        try
        {
            format = WindowsImapiBurner.CreateComObject("IMAPI2.MsftDiscFormat2Data");
            int physical = 0;
            try
            {
                format.Recorder = recorder;
                physical = Convert.ToInt32(format.CurrentPhysicalMediaType);
            }
            catch (COMException)
            {
                // No disc, or a disc IMAPI cannot describe: both read as "nothing usable".
                return OpticalMediaInfo.Empty;
            }

            if (physical == 0)
            {
                return OpticalMediaInfo.Empty;
            }

            bool blank = false;
            long? free = null, total = null;
            try
            {
                blank = (Convert.ToInt32(format.CurrentMediaStatus) & BlankMediaState) != 0;
                free = Convert.ToInt64(format.FreeSectorsOnMedia);
                total = Convert.ToInt64(format.TotalSectorsOnMedia);
            }
            catch (COMException)
            {
                // Capacity stays unknown; the burner re-validates before writing.
            }

            cancellationToken.ThrowIfCancellationRequested();
            (OpticalMediaStructure kind, byte[]? structure) = ReadStructure((object)recorder, physical);
            return new(true, FamilyOf(physical), NameOf(physical), blank, free, total, kind, structure);
        }
        finally
        {
            if (format is not null)
            {
                WindowsImapiBurner.ReleaseComObject((object)format);
            }

            WindowsImapiBurner.ReleaseComObject((object)recorder);
        }
    }

    internal static OpticalMediaFamily FamilyOf(int physical) => physical switch
    {
        0x1 or 0x2 or 0x3 => OpticalMediaFamily.Cd,
        >= 0x4 and <= 0xB or 0xD => OpticalMediaFamily.Dvd,
        >= 0x11 and <= 0x13 => OpticalMediaFamily.BluRay,
        _ => OpticalMediaFamily.Unknown,
    };

    internal static string NameOf(int physical) => physical switch
    {
        0x1 => "CD-ROM",
        0x2 => "CD-R",
        0x3 => "CD-RW",
        0x4 => "DVD-ROM",
        0x5 => "DVD-RAM",
        0x6 => "DVD+R",
        0x7 => "DVD+RW",
        0x8 => "DVD+R DL",
        0x9 => "DVD-R",
        0xA => "DVD-RW",
        0xB => "DVD-R DL",
        0xD => "DVD+RW DL",
        0x11 => "BD-ROM",
        0x12 => "BD-R",
        0x13 => "BD-RE",
        _ => "Unknown",
    };

    private static (OpticalMediaStructure Kind, byte[]? Data) ReadStructure(object recorder, int physical)
    {
        OpticalMediaStructure kind = physical switch
        {
            CdRecordable or CdRewritable => OpticalMediaStructure.CdAtip,
            DvdPlusRecordable or DvdPlusRecordableDualLayer => OpticalMediaStructure.DvdAdip,
            DvdDashRecordable or DvdDashRecordableDualLayer => OpticalMediaStructure.DvdPreRecorded,
            _ => OpticalMediaStructure.None,
        };
        if (kind == OpticalMediaStructure.None)
        {
            return (kind, null);
        }

        try
        {
            ImapiMmcCommandTransport transport = new(recorder);
            byte[]? data = kind switch
            {
                OpticalMediaStructure.CdAtip => ReadAtip(transport),
                OpticalMediaStructure.DvdAdip => ReadDvdStructure(transport, DvdAdipFormat),
                _ => ReadDvdStructure(transport, DvdPreRecordedFormat),
            };
            return (data is null ? OpticalMediaStructure.None : kind, data);
        }
        catch (Exception exception) when (exception is COMException or IOException or InvalidCastException
            or NotSupportedException or ArgumentOutOfRangeException)
        {
            return (OpticalMediaStructure.None, null);
        }
    }

    private static byte[]? ReadAtip(IMmcCommandTransport transport)
    {
        const int length = 32;
        byte[] cdb = new byte[10];
        cdb[0] = ReadTocPmaAtipOpcode;
        cdb[1] = 0x02; // MSF addressing
        cdb[2] = 0x04; // format 4: ATIP
        BinaryPrimitives.WriteUInt16BigEndian(cdb.AsSpan(7, 2), length);
        return Run(transport, cdb, length);
    }

    private static byte[]? ReadDvdStructure(IMmcCommandTransport transport, byte format)
    {
        // The first two bytes of every structure hold its length, so ask for the header first.
        byte[]? header = Run(transport, DvdStructureCdb(format, 4), 4);
        if (header is null || header.Length < 4)
        {
            return null;
        }

        int length = Math.Min(BinaryPrimitives.ReadUInt16BigEndian(header) + 2, MaximumStructureBytes);
        return length <= 4 ? null : Run(transport, DvdStructureCdb(format, (ushort)length), length);
    }

    private static byte[] DvdStructureCdb(byte format, ushort allocationLength)
    {
        byte[] cdb = new byte[12];
        cdb[0] = ReadDvdStructureOpcode;
        cdb[7] = format;
        BinaryPrimitives.WriteUInt16BigEndian(cdb.AsSpan(8, 2), allocationLength);
        return cdb;
    }

    private static byte[]? Run(IMmcCommandTransport transport, byte[] cdb, int allocationLength)
    {
        MmcCommandResult result = transport.Execute(cdb, allocationLength, CommandTimeoutSeconds);
        // S_IMAPI_COMMAND_HAS_SENSE_DATA is positive but means the drive failed the command.
        if (result.HResult < 0 || result.HResult == BluRayMmcCommands.CommandHasSenseData || result.Data.Length == 0)
        {
            return null;
        }

        return result.Data;
    }
}
