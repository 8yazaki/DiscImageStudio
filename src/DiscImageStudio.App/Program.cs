using System.IO;
using System.Runtime.InteropServices;
using DiscImageStudio.Localization;
using DiscImageStudio.Services;

namespace DiscImageStudio;

internal static class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            // Must run before any window, view model or static text is created.
            L.Initialize(Environment.GetEnvironmentVariable(L.EnvironmentVariable)
                         ?? new AppSettingsStore().Load().Language);
            return AppHost.Run();
        }

        AttachToParentConsole();
        // Command-line output stays in the source language unless asked otherwise, so
        // existing scripts keep parsing the same text: --lang <code> or the environment variable.
        arguments = ExtractLanguage(arguments, out string? language);
        L.Initialize(language ?? Environment.GetEnvironmentVariable(L.EnvironmentVariable) ?? L.SourceLanguage);
        return UnifiedCommandRunner.Run(arguments);
    }

    private static string[] ExtractLanguage(string[] arguments, out string? language)
    {
        language = null;
        List<string> rest = new(arguments.Length);
        for (int index = 0; index < arguments.Length; index++)
        {
            if (arguments[index] == "--lang" && index + 1 < arguments.Length)
            {
                language = arguments[++index];
                continue;
            }

            rest.Add(arguments[index]);
        }

        return [.. rest];
    }

    private static void AttachToParentConsole()
    {
        if (!OperatingSystem.IsWindows() || !AttachConsole(AttachParentProcess))
        {
            return;
        }

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }

    private const uint AttachParentProcess = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);
}
