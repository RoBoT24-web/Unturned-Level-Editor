using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Windows.Forms;
using LevelEditorPlus.Setup;

internal static class Program
{
    private static int checks;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LockResource(IntPtr resource);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr module);

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception(description);
        checks++;
    }

    private static void ExpectFailure(Action action, string description)
    {
        try { action(); }
        catch (Exception error) when (error is IOException || error is InvalidDataException ||
            error is InvalidOperationException || error is UnauthorizedAccessException)
        {
            checks++;
            return;
        }
        throw new Exception(description);
    }

    [STAThread]
    private static void Main(string[] args)
    {
        string workspace = Path.GetFullPath(args[0]);
        string scratch = Path.Combine(Path.GetTempPath(), "LevelEditorPlus-InstallerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            Assembly assembly = typeof(InstallEngine).Assembly;
            List<PayloadFile> payload = InstallEngine.ReadPayload(assembly);
            Check(payload.Count == 14, "Exactly 4 module/license and 10 asset files must be embedded.");
            Check(payload.Select(f => f.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() == payload.Count,
                "No duplicate payload paths.");
            foreach (PayloadFile file in payload)
            {
                string source;
                if (file.RelativePath.StartsWith(@"Sandbox\BarricadeSlots\"))
                    source = Path.Combine(workspace, @"BarricadeSlots\Content\BarricadeSlots",
                        file.RelativePath.Substring(@"Sandbox\BarricadeSlots\".Length));
                else if (file.RelativePath.EndsWith(".module"))
                    source = Path.Combine(workspace, @"LevelEditorPlus\module\LevelEditorPlus.module");
                else if (file.RelativePath.EndsWith("LICENSE.txt"))
                    source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        @".nuget\packages\lib.harmony\2.3.3\LICENSE");
                else
                    source = Path.Combine(workspace, @"LevelEditorPlus\src\LevelEditorPlus\bin\Release", Path.GetFileName(file.RelativePath));
                Check(file.Bytes.SequenceEqual(File.ReadAllBytes(source)), "Payload must match build/export: " + file.RelativePath);
            }
            Check(assembly.GetReferencedAssemblies().All(a =>
                a.Name == "mscorlib" || a.Name == "System" || a.Name == "System.Core" ||
                a.Name == "System.Drawing" || a.Name == "System.Windows.Forms"),
                "Single EXE must need no non-framework runtime assemblies.");
            IntPtr image = LoadLibraryEx(assembly.Location, IntPtr.Zero, 2);
            Check(image != IntPtr.Zero, "Open packaged executable for manifest verification.");
            try
            {
                IntPtr resource = FindResource(image, (IntPtr)1, (IntPtr)24);
                Check(resource != IntPtr.Zero, "Executable has a native application manifest.");
                uint size = SizeofResource(image, resource);
                IntPtr bytes = LockResource(LoadResource(image, resource));
                Check(size > 0 && bytes != IntPtr.Zero, "Read executable manifest.");
                var data = new byte[size];
                Marshal.Copy(bytes, data, 0, data.Length);
                using (var stream = new MemoryStream(data))
                {
                    XDocument manifest = XDocument.Load(stream);
                    Check(manifest.Descendants().Any(e => e.Name.LocalName == "requestedExecutionLevel" &&
                        (string)e.Attribute("level") == "requireAdministrator"),
                        "Packaged EXE requests elevation for protected Steam directories.");
                }
            }
            finally { FreeLibrary(image); }
            Check(!InstallEngine.IsGameDirectory(scratch), "Reject non-game directory.");
            ExpectFailure(() => InstallEngine.Install(scratch, true, payload), "Invalid game must fail.");
            File.WriteAllText(Path.Combine(scratch, "Unturned.exe"), "test fixture, not an executable");
            Check(!InstallEngine.IsGameDirectory(scratch), "EXE alone is not enough.");
            Directory.CreateDirectory(Path.Combine(scratch, @"Unturned_Data\Managed"));
            Check(InstallEngine.IsGameDirectory(scratch), "Valid game layout accepted.");
            Check(InstallEngine.Install(scratch, false, payload) == 4, "Module-only install count.");
            Check(!Directory.Exists(Path.Combine(scratch, "Sandbox")), "Module-only install skips assets.");
            string settings = Path.Combine(scratch, @"Modules\LevelEditorPlus\LevelEditorPlus.settings.json");
            string clipboard = Path.Combine(scratch, @"Modules\LevelEditorPlus\clipboard.json");
            File.WriteAllText(settings, "keep settings");
            File.WriteAllText(clipboard, "keep clipboard");
            Check(InstallEngine.Install(scratch, true, payload) == 14, "Full install count.");
            foreach (PayloadFile file in payload)
                Check(File.ReadAllBytes(Path.Combine(scratch, file.RelativePath)).SequenceEqual(file.Bytes),
                    "Installed bytes must match payload: " + file.RelativePath);
            Check(File.ReadAllText(settings) == "keep settings", "Preserve settings.");
            Check(File.ReadAllText(clipboard) == "keep clipboard", "Preserve clipboard.");
            string module = Path.Combine(scratch, @"Modules\LevelEditorPlus\LevelEditorPlus.dll");
            File.WriteAllText(module, "old version");
            InstallEngine.Install(scratch, false, payload);
            Check(File.ReadAllBytes(module).SequenceEqual(payload.Single(f => f.RelativePath.EndsWith("LevelEditorPlus.dll")).Bytes),
                "Reinstall updates owned files.");
            string first = Path.Combine(scratch, @"Modules\LevelEditorPlus\RollbackFirst.txt");
            string locked = Path.Combine(scratch, @"Modules\LevelEditorPlus\RollbackLocked.txt");
            File.WriteAllText(first, "old first");
            File.WriteAllText(locked, "old locked");
            var failurePayload = new List<PayloadFile>
            {
                new PayloadFile(@"Modules\LevelEditorPlus\RollbackFirst.txt", new byte[] { 1, 2 }),
                new PayloadFile(@"Modules\LevelEditorPlus\RollbackNew.txt", new byte[] { 3, 4 }),
                new PayloadFile(@"Modules\LevelEditorPlus\RollbackLocked.txt", new byte[] { 5, 6 })
            };
            using (var fileLock = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                ExpectFailure(() => InstallEngine.Install(scratch, false, failurePayload), "Locked file must fail installation.");
            Check(File.ReadAllText(first) == "old first", "Rollback restores overwritten files.");
            Check(!File.Exists(Path.Combine(scratch, @"Modules\LevelEditorPlus\RollbackNew.txt")), "Rollback removes newly written files.");
            Check(File.ReadAllText(locked) == "old locked", "Locked original remains intact.");
            foreach (string invalid in new[]
            {
                @"..\escape.txt", @"Modules\LevelEditorPlus\..\escape.txt",
                @"C:\escape.txt", @"Modules\LevelEditorPlus\bad.txt:stream",
                @"Other\file.txt", @"Modules\LevelEditorPlus\folder/escape.txt"
            })
                ExpectFailure(() => InstallEngine.Install(scratch, true, new[] { new PayloadFile(invalid, new byte[] { 1 }) }),
                    "Invalid destination must fail: " + invalid);
            string vdf = "\"libraryfolders\" { \"0\" { \"path\" \"C:\\\\Steam\" } \"1\" { \"path\" \"D:\\\\SteamLibrary\" } }";
            Check(SteamDiscovery.ParseLibraryPaths(vdf).SequenceEqual(new[] { @"C:\Steam", @"D:\SteamLibrary" }),
                "Modern Steam libraries parse with escaped separators.");
            Check(SteamDiscovery.ParseLibraryPaths("\"1\" \"E:\\\\OldLibrary\"").Single() == @"E:\OldLibrary",
                "Old Steam libraries parse.");
            string steam = Path.Combine(scratch, "Steam");
            string library = Path.Combine(scratch, "OtherLibrary");
            Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
            Directory.CreateDirectory(Path.Combine(library, "steamapps"));
            File.WriteAllText(Path.Combine(steam, @"steamapps\libraryfolders.vdf"),
                "\"libraryfolders\" { \"1\" { \"path\" \"" + library.Replace(@"\", @"\\") + "\" } }");
            File.WriteAllText(Path.Combine(library, @"steamapps\appmanifest_304930.acf"), "\"installdir\" \"CustomUnturned\"");
            var candidates = SteamDiscovery.GameCandidates(steam).ToList();
            Check(candidates.Contains(Path.Combine(steam, @"steamapps\common\Unturned")), "Default Steam candidate.");
            Check(candidates.Contains(Path.Combine(library, @"steamapps\common\Unturned")), "Secondary library candidate.");
            Check(candidates.Contains(Path.Combine(library, @"steamapps\common\CustomUnturned")), "App manifest directory candidate.");
            File.WriteAllText(Path.Combine(library, @"steamapps\appmanifest_304930.acf"), "\"installdir\" \"..\"");
            Check(!SteamDiscovery.GameCandidates(steam).Any(p => p.EndsWith("..")), "Reject traversal in Steam manifest.");

            Application.EnableVisualStyles();
            Type formType = assembly.GetType("LevelEditorPlus.Setup.InstallerForm", true);
            using (var form = (Form)Activator.CreateInstance(formType, true))
            {
                form.PerformLayout();
                var table = form.Controls.OfType<TableLayoutPanel>().Single();
                var installButton = table.Controls.OfType<Button>().Single(b => b.Text == "Install / Update");
                Check(installButton.Bottom <= table.ClientSize.Height, "Install button fits within client area.");
                Check(table.Controls.OfType<ComboBox>().Single().Width > 200, "Directory confirmation field has usable width.");
                var checkbox = table.Controls.OfType<CheckBox>().Single();
                Check(checkbox.Checked, "Asset pack defaults enabled.");
                table.Controls.OfType<ComboBox>().Single().Text = scratch;
                Check(InstallEngine.IsGameDirectory(table.Controls.OfType<ComboBox>().Single().Text),
                    "Game directory can be manually confirmed.");
                foreach (string message in new[]
                {
                    "Game not detected. Use Browse to choose your Unturned folder.",
                    "Some Steam folders could not be checked. Browse manually if needed.",
                    "Installed 14 files successfully. Launch WITHOUT BattlEye."
                })
                {
                    table.Controls.OfType<Label>().Single(label => label.Text == "").Text = message;
                    form.PerformLayout();
                    Check(table.Controls.Cast<Control>().All(control =>
                        control.Bottom <= table.ClientSize.Height && control.Right <= table.ClientSize.Width),
                        "Installer controls fit after status change: " + message);
                    table.Controls.OfType<Label>().Single(label => label.Text == message).Text = "";
                }
            }
            Console.WriteLine($"PASS: {checks} installer checks (real embedded payload, temporary game layout, native WinForms layout).");
        }
        finally
        {
            Directory.Delete(scratch, true);
        }
    }
}
