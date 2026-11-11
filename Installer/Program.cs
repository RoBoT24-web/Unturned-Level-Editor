using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LevelEditorPlus.Setup
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly ComboBox directory = new ComboBox { Dock = DockStyle.Fill };
        private readonly CheckBox assets = new CheckBox
        {
            Text = "Also install BarricadeSlots assets (doors / windows / gates / hatches)",
            Checked = true, AutoSize = true
        };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(600, 0) };
        private readonly Button install = new Button { Text = "Install / Update", AutoSize = true };

        public InstallerForm()
        {
            Text = "LevelEditorPlus Installer";
            ClientSize = new Size(650, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 8
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(layout);
            AddWide(layout, new Label { Text = "Install LevelEditorPlus for Unturned", Font = new Font(Font, FontStyle.Bold), AutoSize = true }, 0);
            AddWide(layout, new Label { Text = "Confirm your game folder (not the Maps or Modules folder):", AutoSize = true }, 1);
            layout.Controls.Add(directory, 0, 2);
            var browse = new Button { Text = "Browse...", AutoSize = true };
            browse.Click += Browse;
            layout.Controls.Add(browse, 1, 2);
            AddWide(layout, assets, 3);
            AddWide(layout, new Label
            {
                AutoSize = true, MaximumSize = new Size(600, 0),
                Text = "Uncheck assets if this pack is already embedded in your map's Bundles folder " +
                    "or installed from another source. Duplicate asset GUIDs can conflict.\r\n\r\n" +
                    "Close Unturned before installing. Existing settings and clipboard are preserved.\r\n" +
                    "After installing, launch Unturned WITHOUT BattlEye to use the editor module.\r\n" +
                    "No Unity or Visual Studio needed. Requires Windows with .NET Framework 4.8."
            }, 4);
            AddWide(layout, status, 5);
            install.Click += Install;
            layout.Controls.Add(install, 0, 6);
            var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
            layout.Controls.Add(close, 1, 6);
            CancelButton = close;
            Shown += (s, e) => DetectDirectories();
        }

        private static void AddWide(TableLayoutPanel panel, Control control, int row)
        {
            control.Margin = new Padding(0, 5, 0, 10);
            panel.Controls.Add(control, 0, row);
            panel.SetColumnSpan(control, 2);
        }

        private void DetectDirectories()
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam")
            };
            var errors = new List<string>();
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                try
                {
                    using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                    using (RegistryKey key = root.OpenSubKey(@"Software\Valve\Steam"))
                    {
                        string path = (key?.GetValue("SteamPath") ?? key?.GetValue("InstallPath")) as string;
                        if (!string.IsNullOrWhiteSpace(path))
                            roots.Add(path.Replace('/', '\\'));
                    }
                }
                catch (Exception error) when (error is SecurityException || error is UnauthorizedAccessException || error is IOException)
                {
                    errors.Add(error.Message);
                }
            }
            var games = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                try
                {
                    foreach (string path in SteamDiscovery.GameCandidates(root))
                        if (InstallEngine.IsGameDirectory(path))
                            games.Add(path);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                    error is ArgumentException || error is SecurityException)
                {
                    errors.Add(error.Message);
                }
            }
            directory.Items.AddRange(games.Cast<object>().ToArray());
            if (directory.Items.Count > 0)
                directory.SelectedIndex = 0;
            status.Text = errors.Count > 0
                ? "Some Steam folders could not be checked. Browse manually if needed."
                : games.Count > 0 ? "Game detected. Confirm the directory, then install." : "Game not detected. Use Browse to choose your Unturned folder.";
            if (errors.Count > 0)
                MessageBox.Show(this, string.Join("\r\n", errors), "Steam detection warnings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void Browse(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = "Select the Unturned folder containing Unturned.exe",
                SelectedPath = directory.Text, ShowNewFolderButton = false
            })
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    directory.Text = dialog.SelectedPath;
        }

        private void Install(object sender, EventArgs e)
        {
            install.Enabled = false;
            try
            {
                string target = directory.Text.Trim().Trim('"');
                if (!InstallEngine.IsGameDirectory(target))
                    throw new InvalidOperationException("Choose the game folder containing Unturned.exe and Unturned_Data\\Managed.");
                Process[] processes = Process.GetProcessesByName("Unturned");
                bool running = processes.Length > 0;
                foreach (Process process in processes)
                    process.Dispose();
                if (running)
                    throw new InvalidOperationException("Close Unturned before installing, then try again.");
                int count = InstallEngine.Install(target, assets.Checked, InstallEngine.ReadPayload(Assembly.GetExecutingAssembly()));
                status.Text = $"Installed {count} files successfully. Launch WITHOUT BattlEye.";
                MessageBox.Show(this,
                    "Installation complete!\r\n\r\nLaunch Unturned without BattlEye, then open the map editor.\r\n" +
                    "Look for the LevelEditorPlus toolbar in the top-right." +
                    (assets.Checked ? "\r\nSearch Objects for \"Barricade Slot\" to use the assets." : "") +
                    "\r\n\r\nYour existing settings and clipboard were preserved.",
                    "LevelEditorPlus installed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException ||
                error is InvalidOperationException || error is ArgumentException ||
                error is SecurityException || error is AggregateException ||
                error is System.ComponentModel.Win32Exception)
            {
                status.Text = "Not installed. See the error, correct the problem, and retry.";
                MessageBox.Show(this, error.ToString(), "Installation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                install.Enabled = true;
            }
        }
    }
}
