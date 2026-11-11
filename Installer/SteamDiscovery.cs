using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace LevelEditorPlus.Setup
{
    public static class SteamDiscovery
    {
        public static IEnumerable<string> ParseLibraryPaths(string vdf)
        {
            // Supports current "path" entries and older numbered library entries.
            foreach (Match match in Regex.Matches(vdf, "\"(?:path|[0-9]+)\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase))
            {
                string path = match.Groups[1].Value.Replace(@"\\", @"\");
                if (Path.IsPathRooted(path))
                    yield return path;
            }
        }

        public static IEnumerable<string> GameCandidates(string steamRoot)
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamRoot };
            string config = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
            if (File.Exists(config))
                foreach (string path in ParseLibraryPaths(File.ReadAllText(config)))
                    libraries.Add(path);
            foreach (string library in libraries)
            {
                string apps = Path.Combine(library, "steamapps");
                string manifest = Path.Combine(apps, "appmanifest_304930.acf");
                if (File.Exists(manifest))
                {
                    Match match = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    if (match.Success && match.Groups[1].Value.IndexOfAny(new[] { '\\', '/', ':' }) < 0 &&
                        match.Groups[1].Value != "." && match.Groups[1].Value != "..")
                        yield return Path.Combine(apps, "common", match.Groups[1].Value);
                }
                yield return Path.Combine(apps, "common", "Unturned");
            }
        }
    }
}
