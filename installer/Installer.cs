using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Win32;

[assembly: AssemblyTitle("BFS Forza Live Bridge Installer")]
[assembly: AssemblyVersion("0.2.4.0")]
[assembly: AssemblyFileVersion("0.2.4.0")]

internal static class Installer
{
    const string LoaderUrl = "https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip";
    const string LoaderHash = "f4cc496bd098a0df4164b81e3737297707f13a47c2478dba2f60eefab784817a";
    static string Root = AppDomain.CurrentDomain.BaseDirectory;
    static bool Yes, Check, ArgsPresent;

    static int Main(string[] args)
    {
        ArgsPresent = args.Length > 0;
        try
        {
            Console.WriteLine("BFS x Forza Live Bridge - installer package 0.2.4 (receiver 0.2.3)");
            string game = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--yes") Yes = true;
                else if (args[i] == "--check") Check = true;
                else if (args[i] == "--game" && i + 1 < args.Length) game = args[++i];
                else throw new Exception("Usage: Install-Bridge.exe [--game \"BFS folder\"] [--check] [--yes]");
            }
            if (game == null) game = FindGame();
            game = Path.GetFullPath(game.Trim().Trim('"'));
            if (!File.Exists(Path.Combine(game, "beatforspeed.exe")) || !File.Exists(Path.Combine(game, "GameAssembly.dll")))
                throw new Exception("Select the Beat For Speed Demo folder containing beatforspeed.exe and GameAssembly.dll.");
            string payload = Path.Combine(Root, "BepInEx", "plugins", "BFS.ForzaLive.dll");
            string manifest = File.ReadAllText(Path.Combine(Root, "install-manifest.json"));
            var hash = Regex.Match(manifest, "\"receiverSha256\"\\s*:\\s*\"([a-fA-F0-9]{64})\"");
            if (!hash.Success || Hash(payload) != hash.Groups[1].Value.ToLowerInvariant())
                throw new Exception("Release DLL checksum mismatch. Extract a fresh release ZIP.");
            bool loader = HasLoader(game);
            Console.WriteLine("Game: " + game);
            Console.WriteLine("Receiver checksum: verified");
            Console.WriteLine("Compatible BepInEx 6 IL2CPP loader: " + (loader ? "found" : "not found"));
            if (Check) { Console.WriteLine("Check complete; no files changed."); return 0; }
            foreach (var process in Process.GetProcessesByName("beatforspeed"))
            {
                string path;
                try { path = process.MainModule.FileName; }
                catch { throw new Exception("BFS is running. Close it before installing."); }
                if (string.Equals(path, Path.Combine(game, "beatforspeed.exe"), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Close BFS before installing. The installer will not close it for you.");
            }
            if (!loader)
            {
                if (Directory.Exists(Path.Combine(game, "BepInEx")) || File.Exists(Path.Combine(game, "winhttp.dll")))
                    throw new Exception("An existing or incomplete loader is present. Install a compatible BepInEx 6 Unity IL2CPP x64 loader using its official instructions; existing loader files will not be overwritten.");
                if (!Confirm("Download and install official BepInEx 6 IL2CPP x64 build 788?"))
                    throw new Exception("Loader installation declined. No game files changed.");
                InstallLoader(game);
            }
            string plugins = Path.Combine(game, "BepInEx", "plugins");
            Directory.CreateDirectory(plugins);
            string destination = Path.Combine(plugins, "BFS.ForzaLive.dll");
            string backup = null;
            if (File.Exists(destination))
            {
                string backups = Path.Combine(Root, "backups");
                Directory.CreateDirectory(backups);
                backup = Path.Combine(backups, "BFS.ForzaLive-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".dll");
                File.Copy(destination, backup);
            }
            try
            {
                File.Copy(payload, destination, true);
                if (Hash(destination) != Hash(payload)) throw new Exception("Installed DLL verification failed.");
            }
            catch
            {
                if (backup != null) File.Copy(backup, destination, true);
                throw;
            }
            Console.WriteLine("Installed and verified: " + destination);
            if (backup != null) Console.WriteLine("Previous bridge backed up: " + backup);
            Console.WriteLine("Next: open Forza in Drone Mode; use Backspace to hide its UI.");
            Console.WriteLine("Run Start-Bridge.ps1, then start a solo BFS song. F8 toggles video; F9 toggles clean mode.");
            Console.WriteLine("Python 3 is required. Start-Bridge downloads FFmpeg if needed. See INSTALL.txt.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("Install failed: " + ex.Message); return 1; }
        finally
        {
            if (!ArgsPresent && !Console.IsInputRedirected)
            {
                Console.WriteLine("Press Enter to close."); Console.ReadLine();
            }
        }
    }

    static bool Confirm(string question)
    {
        if (Yes) return true;
        Console.Write(question + " [y/N] ");
        return string.Equals(Console.ReadLine(), "y", StringComparison.OrdinalIgnoreCase);
    }

    static bool HasLoader(string game)
    {
        string core = Path.Combine(game, "BepInEx", "core", "BepInEx.Core.dll");
        string il2cpp = Path.Combine(game, "BepInEx", "core", "BepInEx.Unity.IL2CPP.dll");
        if (!File.Exists(core) || !File.Exists(il2cpp)) return false;
        return AssemblyName.GetAssemblyName(core).Version.Major == 6;
    }

    static string FindGame()
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string steam = Convert.ToString(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null));
        if (!string.IsNullOrEmpty(steam)) libraries.Add(steam);
        libraries.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        foreach (string home in libraries.ToArray())
        {
            string vdf = Path.Combine(home, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
        }
        var candidates = libraries.Select(p => Path.Combine(p, "steamapps", "common", "Beat For Speed Demo"))
            .Where(p => File.Exists(Path.Combine(p, "beatforspeed.exe"))).ToArray();
        if (candidates.Length == 1) return candidates[0];
        Console.WriteLine("Paste the full Beat For Speed Demo installation folder:");
        string result = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(result)) throw new Exception("Game folder is required. Use --game to specify it.");
        return result;
    }

    static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    static void InstallLoader(string game)
    {
        string runtime = Path.Combine(Root, "runtime");
        Directory.CreateDirectory(runtime);
        string archive = Path.Combine(runtime, "BepInEx-IL2CPP-x64-build-788.zip");
        if (!File.Exists(archive) || Hash(archive) != LoaderHash)
        {
            Console.WriteLine("Downloading verified official loader...");
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (var client = new WebClient()) client.DownloadFile(LoaderUrl, archive);
        }
        if (Hash(archive) != LoaderHash) throw new Exception("Loader checksum mismatch. No loader files installed.");
        string stage = Path.Combine(runtime, "loader-stage-" + Guid.NewGuid().ToString("N"));
        string boundary = Path.GetFullPath(stage) + Path.DirectorySeparatorChar;
        using (var zip = ZipFile.OpenRead(archive))
        {
            foreach (var entry in zip.Entries)
                if (!Path.GetFullPath(Path.Combine(stage, entry.FullName)).StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Unsafe loader archive path.");
        }
        ZipFile.ExtractToDirectory(archive, stage);
        if (!HasLoader(stage)) throw new Exception("Loader archive layout was not recognized.");
        var files = Directory.GetFiles(stage, "*", SearchOption.AllDirectories);
        var created = new List<string>();
        foreach (string file in files)
            if (File.Exists(Path.Combine(game, file.Substring(boundary.Length))))
                throw new Exception("Loader file already exists; refusing overwrite: " + file.Substring(boundary.Length));
        try
        {
            foreach (string file in files)
            {
                string dest = Path.Combine(game, file.Substring(boundary.Length));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(file, dest); created.Add(dest);
            }
        }
        catch
        {
            foreach (string file in created) File.Delete(file);
            throw;
        }
        Console.WriteLine("Official BepInEx build 788 installed. First BFS launch generates loader data and may take longer.");
    }
}
