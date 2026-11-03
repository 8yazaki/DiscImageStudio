using System.IO;
using System.Text;
using DiscImageStudio.Core.Media;

/// <summary>Disc identity text, saved pairings and the easy-mode decision. Pure logic: no
/// drive, no UI, so the same file also runs on non-Windows machines.</summary>
internal static class EasyModeTests
{
    internal static void Run()
    {
        TestIdentityText();
        TestAtip();
        TestStore();
        TestResolver();
        TestPlanner();
        Console.WriteLine("easy-mode: passed (identity text, saved pairings, preset resolution, plan notices)");
    }

    private static void TestIdentityText()
    {
        // 4-byte header, binary fields around readable manufacturer text.
        byte[] dvdR = [0x00, 0x2E, 0, 0, 0x01, 0x40, 0x0F, 0x22, 0x0A, 0xAB, 0xCD,
            .. Encoding.ASCII.GetBytes("MCC 03RG"), 0x00, 0x00, 0x02, 0x00, .. Encoding.ASCII.GetBytes("20"), 0x00];
        Require(MediaIdentity.FromStructure(MediaStructureKind.DvdPreRecorded, dvdR) == "MCC 03RG",
            "DVD-R manufacturer text is extracted and short noise is ignored");

        byte[] plusR = [0, 0x0A, 0, 0, 0x01, .. Encoding.ASCII.GetBytes("RICOHJPN"), 0, 0, .. Encoding.ASCII.GetBytes("R02"), 0];
        Require(MediaIdentity.FromStructure(MediaStructureKind.DvdAdip, plusR) == "RICOHJPN",
            "DVD+R manufacturer text is extracted");

        byte[] spaced = [0, 0, 0, 0, .. Encoding.ASCII.GetBytes("MCC 004     000"), 0];
        Require(MediaIdentity.FromStructure(MediaStructureKind.DvdAdip, spaced) == "MCC 004 000",
            "padding collapses to single spaces");

        Require(MediaIdentity.FromStructure(MediaStructureKind.DvdAdip, new byte[40]) is null,
            "an all-zero structure has no identity");
        Require(MediaIdentity.FromStructure(MediaStructureKind.None, dvdR) is null, "no structure, no identity");
        Require(MediaIdentity.FromStructure(MediaStructureKind.DvdAdip, [1, 2, 3]) is null, "a truncated structure has no identity");
        Require(MediaIdentity.Normalize("  mcc   03rg\u0000 ") == "MCC 03RG", "normalization upper-cases and trims");
        Require(MediaIdentity.Normalize(null).Length == 0, "null normalizes to empty");
    }

    private static void TestAtip()
    {
        byte[] atip = new byte[28];
        atip[8] = 97; atip[9] = 24; atip[10] = 1;
        Require(MediaIdentity.FromStructure(MediaStructureKind.CdAtip, atip) == "ATIP 97:24.01", "ATIP lead-in time is formatted");
        atip[9] = 99;
        Require(MediaIdentity.FromStructure(MediaStructureKind.CdAtip, atip) is null, "an impossible time is rejected");
        Require(MediaIdentity.FromStructure(MediaStructureKind.CdAtip, new byte[5]) is null, "a short ATIP block is rejected");
    }

    private static void TestStore()
    {
        string directory = Path.Combine(Path.GetTempPath(), "easy-mode-" + Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(directory, "nested", UserMediaPresetStore.FileName);
            UserMediaPresetStore store = new(path);
            Require(store.Load().Count == 0, "a missing file reads as empty");
            Require(store.Find(MediaFamily.Dvd, "MXL RG03") is null, "nothing is found in an empty store");

            DateTimeOffset now = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
            store.Remember(new("mxl  rg03", MediaFamily.Dvd, "user-maxell", "Drive A", now));
            store.Remember(new("MXL RG03", MediaFamily.Dvd, "user-maxell-2", "Drive A", now.AddHours(1)));
            store.Remember(new("MXL RG03", MediaFamily.Cd, "user-cd", null, now));
            Require(store.Load().Count == 2, "the same disc model replaces its earlier pairing, other families stay");
            Require(store.Find(MediaFamily.Dvd, " mxl rg03 ")?.PresetId == "user-maxell-2", "lookup ignores case and spacing");
            Require(new UserMediaPresetStore(path).Find(MediaFamily.Cd, "MXL RG03")?.PresetId == "user-cd", "pairings persist");
            Require(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Length == 0, "no temporary file is left behind");

            Throws(() => store.Remember(new("", MediaFamily.Dvd, "x", null, now)), "an empty identity is refused");
            Throws(() => store.Remember(new("ID", MediaFamily.Unknown, "x", null, now)), "an unknown family is refused");

            File.WriteAllText(path, "{ not json");
            Require(store.Load().Count == 0, "a damaged file reads as empty");
            store.Remember(new("MXL RG03", MediaFamily.Dvd, "user-maxell", null, now));
            Require(store.Find(MediaFamily.Dvd, "MXL RG03")?.PresetId == "user-maxell", "a damaged file is replaced on the next save");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void TestResolver()
    {
        HashSet<string> ids = ["verbatim-dvd-r-azo-43533", "verbatim-dvd-r-azo-43523", "user-maxell"];
        string directory = Path.Combine(Path.GetTempPath(), "easy-mode-" + Guid.NewGuid().ToString("N"));
        try
        {
            UserMediaPresetStore store = new(Path.Combine(directory, UserMediaPresetStore.FileName));
            MediaPresetMatch builtIn = MediaPresetResolver.Resolve(MediaFamily.Dvd, "MCC 03RG | X", store, ids.Contains);
            Require(builtIn.Kind == MediaMatchKind.BuiltIn && builtIn.PresetId == "verbatim-dvd-r-azo-43533"
                && builtIn.AlternativePresetIds.SequenceEqual(["verbatim-dvd-r-azo-43523"]), "a known Verbatim code resolves to its presets");
            Require(MediaPresetResolver.Resolve(MediaFamily.Cd, "MCC 03RG", store, ids.Contains).Kind == MediaMatchKind.None,
                "a rule never crosses disc families");
            Require(MediaPresetResolver.Resolve(MediaFamily.Dvd, "MCC 004", store, ids.Contains).Kind == MediaMatchKind.None,
                "a rule whose presets are missing does not match");
            Require(MediaPresetResolver.Resolve(MediaFamily.Dvd, null, store, ids.Contains).Kind == MediaMatchKind.None,
                "no identity, no match");

            store.Remember(new("MCC 03RG", MediaFamily.Dvd, "user-maxell", null, DateTimeOffset.UnixEpoch));
            Require(MediaPresetResolver.Resolve(MediaFamily.Dvd, "MCC 03RG", store, ids.Contains).Kind == MediaMatchKind.UserSaved,
                "the user's pairing beats the built-in hint");
            ids.Remove("user-maxell");
            Require(MediaPresetResolver.Resolve(MediaFamily.Dvd, "MCC 03RG", store, ids.Contains).Kind == MediaMatchKind.BuiltIn,
                "a pairing whose preset was deleted falls back");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void TestPlanner()
    {
        EasyPresetOption[] presets =
        [
            new("cd-80", "Standard CD 80", MediaFamily.Cd, 359_849, true),
            new("dvd-5-120mm", "Standard DVD", MediaFamily.Dvd, 2_295_104, true),
            new("verbatim-dvd-r-azo-43533", "Verbatim AZO", MediaFamily.Dvd, 2_297_888, false),
            new("user-maxell", "My maxell", MediaFamily.Dvd, 2_296_000, false),
        ];
        UserMediaPresetStore? none = null;

        EasyPlan empty = EasyModePlanner.Plan(new(false, false, MediaFamily.Unknown, null, null), presets, none);
        Require(!empty.CanProceed && empty.Notices.Single().Kind == EasyNoticeKind.NoDisc, "no disc blocks");

        EasyPlan used = EasyModePlanner.Plan(new(true, false, MediaFamily.Dvd, 100, "MCC 03RG"), presets, none);
        Require(!used.CanProceed && Has(used, EasyNoticeKind.NotBlank), "a written disc blocks");

        EasyPlan known = EasyModePlanner.Plan(new(true, true, MediaFamily.Dvd, 2_300_000, "MCC 03RG"), presets, none);
        Require(known.CanProceed && known.PresetId == "verbatim-dvd-r-azo-43533" && known.Match == MediaMatchKind.BuiltIn
            && Has(known, EasyNoticeKind.UsingBuiltInPreset) && !known.NeedsCalibration, "a known disc model uses its measured preset");

        EasyPlan unknown = EasyModePlanner.Plan(new(true, true, MediaFamily.Dvd, 2_300_000, "MXL RG03"), presets, none);
        Require(unknown.CanProceed && unknown.PresetId == "dvd-5-120mm" && unknown.NeedsCalibration,
            "an unknown disc model proceeds with the standard preset and a calibration warning");

        EasyPlan manual = EasyModePlanner.Plan(new(true, true, MediaFamily.Dvd, 2_300_000, "MXL RG03"), presets, none, "user-maxell");
        Require(manual.CanProceed && manual.PresetId == "user-maxell" && !manual.NeedsCalibration
            && Has(manual, EasyNoticeKind.ChosenManually), "a manual choice is honoured");

        EasyPlan mismatch = EasyModePlanner.Plan(new(true, true, MediaFamily.Dvd, 2_300_000, "MXL RG03"), presets, none, "cd-80");
        Require(!mismatch.CanProceed && Has(mismatch, EasyNoticeKind.FamilyMismatch), "a CD preset on a DVD blocks");

        EasyPlan small = EasyModePlanner.Plan(new(true, true, MediaFamily.Dvd, 2_290_000, "MCC 03RG"), presets, none);
        EasyNotice shortNotice = small.Notices.Single(notice => notice.Kind == EasyNoticeKind.CapacityShort);
        Require(!small.CanProceed && shortNotice.Number == 2_297_888 - 2_290_000, "too little room blocks and reports the shortfall");

        EasyPlan cdEdge = EasyModePlanner.Plan(new(true, true, MediaFamily.Cd, 359_846, null), presets, none);
        Require(cdEdge.CanProceed && cdEdge.PresetId == "cd-80", "a CD-R may run the overburn allowance past its capacity");
        EasyPlan cdOver = EasyModePlanner.Plan(new(true, true, MediaFamily.Cd, 359_800, null), presets, none);
        Require(!cdOver.CanProceed && Has(cdOver, EasyNoticeKind.CapacityShort), "a CD-R beyond the allowance blocks");

        EasyPlan bd = EasyModePlanner.Plan(new(true, true, MediaFamily.BluRay, null, null), presets, none);
        Require(!bd.CanProceed && Has(bd, EasyNoticeKind.NoPresetForFamily), "a family without presets blocks");

        EasyPlan unknownKind = EasyModePlanner.Plan(new(true, true, MediaFamily.Unknown, null, null), presets, none, "dvd-5-120mm");
        Require(unknownKind.CanProceed && Has(unknownKind, EasyNoticeKind.UnknownMedia), "an unrecognized disc type warns but allows a manual choice");
    }

    private static bool Has(EasyPlan plan, EasyNoticeKind kind) => plan.Notices.Any(notice => notice.Kind == kind);

    private static void Throws(Action action, string name)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException($"Easy mode: {name}.");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Easy mode: {name}.");
        }
    }
}
