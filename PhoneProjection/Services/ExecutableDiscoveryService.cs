using System;
using System.Collections.Generic;
using System.IO;

namespace PhoneProjection.Services
{
    public class ExecutableDiscoveryService
    {
        public string? FindAdbPath(string? customPath = null)
        {
            if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
                return Path.GetFullPath(customPath);

            // 1. Check in PATH
            string? inPath = FindExecutableInPath("adb.exe");
            if (inPath != null) return inPath;

            // 2. Common Android SDK paths
            var candidates = new List<string>();

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            candidates.Add(Path.Combine(localAppData, "Android", "Sdk", "platform-tools", "adb.exe"));

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            candidates.Add(Path.Combine(programFiles, "Android", "platform-tools", "adb.exe"));

            // Check adjacent to scrcpy if scrcpy is installed
            string? scrcpyPath = FindScrcpyPath();
            if (scrcpyPath != null)
            {
                string scrcpyDir = Path.GetDirectoryName(scrcpyPath) ?? string.Empty;
                candidates.Add(Path.Combine(scrcpyDir, "adb.exe"));
            }

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }

            return null;
        }

        public string? FindScrcpyPath(string? customPath = null)
        {
            if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
                return Path.GetFullPath(customPath);

            // 1. Check in PATH
            string? inPath = FindExecutableInPath("scrcpy.exe");
            if (inPath != null) return inPath;

            // 2. Search known WinGet packages dynamically without hardcoding username
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string wingetRoot = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(wingetRoot))
            {
                try
                {
                    var matches = Directory.GetFiles(wingetRoot, "scrcpy.exe", SearchOption.AllDirectories);
                    if (matches.Length > 0)
                        return Path.GetFullPath(matches[0]);
                }
                catch { }
            }

            // 3. Program Files & Chocolatey
            string chocoBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "bin", "scrcpy.exe");
            if (File.Exists(chocoBin)) return chocoBin;

            string chocoLib = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "lib", "scrcpy", "tools", "scrcpy.exe");
            if (File.Exists(chocoLib)) return chocoLib;

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pfScrcpy = Path.Combine(programFiles, "scrcpy", "scrcpy.exe");
            if (File.Exists(pfScrcpy)) return pfScrcpy;

            return null;
        }

        private static string? FindExecutableInPath(string exeName)
        {
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathEnv))
                return null;

            string[] paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in paths)
            {
                try
                {
                    string candidate = Path.Combine(p.Trim(), exeName);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
                catch { }
            }
            return null;
        }
    }
}
