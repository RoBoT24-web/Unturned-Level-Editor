using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace LevelEditorPlus.Setup
{
    public sealed class PayloadFile
    {
        public string RelativePath { get; }
        public byte[] Bytes { get; }
        public PayloadFile(string relativePath, byte[] bytes)
        {
            RelativePath = relativePath;
            Bytes = bytes;
        }
    }

    public static class InstallEngine
    {
        public static bool IsGameDirectory(string path) =>
            !string.IsNullOrWhiteSpace(path) &&
            File.Exists(Path.Combine(path, "Unturned.exe")) &&
            Directory.Exists(Path.Combine(path, "Unturned_Data", "Managed"));

        public static List<PayloadFile> ReadPayload(Assembly assembly)
        {
            var files = new List<PayloadFile>();
            foreach (string resource in assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith("Payload.", StringComparison.Ordinal)))
            {
                using (Stream stream = assembly.GetManifestResourceStream(resource))
                using (var buffer = new MemoryStream())
                {
                    if (stream == null)
                        throw new InvalidDataException("Missing installer resource: " + resource);
                    stream.CopyTo(buffer);
                    files.Add(new PayloadFile(resource.Substring("Payload.".Length), buffer.ToArray()));
                }
            }
            string[] required =
            {
                @"Modules\LevelEditorPlus\LevelEditorPlus.dll",
                @"Modules\LevelEditorPlus\0Harmony.dll",
                @"Modules\LevelEditorPlus\LevelEditorPlus.module",
                @"Modules\LevelEditorPlus\Harmony-LICENSE.txt",
                @"Sandbox\BarricadeSlots\barricadeslots.masterbundle",
                @"Sandbox\BarricadeSlots\MasterBundle.dat",
                @"Sandbox\BarricadeSlots\Objects\Slot_Door\Slot_Door.dat",
                @"Sandbox\BarricadeSlots\Objects\Slot_Gate\Slot_Gate.dat",
                @"Sandbox\BarricadeSlots\Objects\Slot_Window\Slot_Window.dat",
                @"Sandbox\BarricadeSlots\Objects\Slot_Hatch\Slot_Hatch.dat"
            };
            foreach (string path in required)
                if (!files.Any(file => file.RelativePath == path && file.Bytes.Length > 0))
                    throw new InvalidDataException("Incomplete installer payload: " + path);
            return files;
        }

        public static int Install(string gamePath, bool includeAssets, IReadOnlyList<PayloadFile> payload)
        {
            if (!IsGameDirectory(gamePath))
                throw new InvalidOperationException("Choose the Unturned folder containing Unturned.exe and Unturned_Data\\Managed.");
            string root = Path.GetFullPath(gamePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var pending = new List<Tuple<string, byte[]>>();
            foreach (PayloadFile file in payload)
            {
                string path = file.RelativePath;
                bool module = path.StartsWith(@"Modules\LevelEditorPlus\", StringComparison.Ordinal);
                bool assets = path.StartsWith(@"Sandbox\BarricadeSlots\", StringComparison.Ordinal);
                if ((!module && !assets) || Path.IsPathRooted(path) ||
                    path.Split('\\').Any(part => part == ".." || part == "." || part.Length == 0) ||
                    path.IndexOfAny(new[] { ':', '/' }) >= 0)
                    throw new InvalidDataException("Invalid installer destination: " + path);
                if (assets && !includeAssets)
                    continue;
                string target = Path.GetFullPath(Path.Combine(root, path));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Installer destination is outside the game folder.");
                // Do not follow links in writable mod directories when running elevated.
                for (string check = target; check.Length > root.Length; check = Path.GetDirectoryName(check))
                {
                    if ((File.Exists(check) || Directory.Exists(check)) &&
                        (File.GetAttributes(check) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Remove the linked file/folder before installing: " + check);
                }
                pending.Add(Tuple.Create(target, file.Bytes));
            }

            var backups = new List<Tuple<string, byte[]>>();
            try
            {
                foreach (var file in pending)
                {
                    byte[] previous = File.Exists(file.Item1) ? File.ReadAllBytes(file.Item1) : null;
                    Directory.CreateDirectory(Path.GetDirectoryName(file.Item1));
                    backups.Add(Tuple.Create(file.Item1, previous));
                    File.WriteAllBytes(file.Item1, file.Item2);
                }
            }
            catch (Exception installError) when (installError is IOException || installError is UnauthorizedAccessException)
            {
                var errors = new List<Exception> { installError };
                for (int i = backups.Count - 1; i >= 0; i--)
                {
                    var backup = backups[i];
                    try
                    {
                        if (backup.Item2 == null)
                            File.Delete(backup.Item1);
                        else
                            File.WriteAllBytes(backup.Item1, backup.Item2);
                    }
                    catch (Exception restoreError) when (restoreError is IOException || restoreError is UnauthorizedAccessException)
                    {
                        errors.Add(new IOException("Could not restore: " + backup.Item1, restoreError));
                    }
                }
                if (errors.Count > 1)
                    throw new AggregateException("Installation failed and some files could not be restored. Close Unturned and run the installer again.", errors);
                throw new IOException("Installation failed. Files changed by this attempt were rolled back. Close Unturned, check folder permissions, and try again.", installError);
            }
            return pending.Count;
        }
    }
}
