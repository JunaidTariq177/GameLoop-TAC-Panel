using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace GameLoopTACPanel;

public partial class MainWindow : Window
{
    private readonly string _appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TACPanel");
    private readonly string _backupDir;
    private readonly string _reportPath;
    private List<string> _configCandidates = new();

    public MainWindow()
    {
        InitializeComponent();
        _backupDir = Path.Combine(_appDir, "Backups");
        _reportPath = Path.Combine(_appDir, "discovery-report.txt");
        Directory.CreateDirectory(_backupDir);
        LoadPrefs();
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sb = new StringBuilder();
            var processes = Process.GetProcesses().Where(p =>
            {
                try { return new[] { "AndroidEmulator", "aow_exe", "GameLoop", "AppMarket" }.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase); }
                catch { return false; }
            }).Select(p => p.ProcessName + " (PID " + SafePid(p) + ")").Distinct().ToList();

            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var env in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) })
            {
                if (string.IsNullOrWhiteSpace(env) || !Directory.Exists(env)) continue;
                foreach (var dir in SafeDirectories(env, 3))
                    if (dir.Contains("Tencent", StringComparison.OrdinalIgnoreCase) || dir.Contains("GameLoop", StringComparison.OrdinalIgnoreCase) || dir.Contains("TxGameAssistant", StringComparison.OrdinalIgnoreCase))
                        roots.Add(dir);
            }

            InstallList.Items.Clear();
            foreach (var r in roots.OrderBy(x => x)) InstallList.Items.Add(r);
            _configCandidates = new();
            var scanRoots = new HashSet<string>(roots, StringComparer.OrdinalIgnoreCase);
            foreach (var r in new[] { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) })
                if (Directory.Exists(r)) scanRoots.Add(r);

            var allowed = new HashSet<string>(new[] { ".ini", ".conf", ".config", ".json", ".xml", ".cfg", ".properties" }, StringComparer.OrdinalIgnoreCase);
            foreach (var root in scanRoots)
            {
                foreach (var file in SafeFiles(root, 4))
                {
                    if (!allowed.Contains(Path.GetExtension(file))) continue;
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.Length > 2_000_000) continue;
                        var name = Path.GetFileName(file);
                        if (!(name.Contains("game", StringComparison.OrdinalIgnoreCase) || name.Contains("emulator", StringComparison.OrdinalIgnoreCase) || name.Contains("android", StringComparison.OrdinalIgnoreCase) || name.Contains("config", StringComparison.OrdinalIgnoreCase) || name.Contains("setting", StringComparison.OrdinalIgnoreCase) || name.Contains("tencent", StringComparison.OrdinalIgnoreCase) || name.Contains("aow", StringComparison.OrdinalIgnoreCase))) continue;
                        var content = File.ReadAllText(file);
                        if (content.Contains("resolution", StringComparison.OrdinalIgnoreCase) || content.Contains("dpi", StringComparison.OrdinalIgnoreCase) || content.Contains("render", StringComparison.OrdinalIgnoreCase) || content.Contains("opengl", StringComparison.OrdinalIgnoreCase) || content.Contains("directx", StringComparison.OrdinalIgnoreCase))
                            _configCandidates.Add(file);
                    }
                    catch { }
                }
            }
            _configCandidates = _configCandidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(200).ToList();
            ConfigList.Items.Clear();
            foreach (var f in _configCandidates) ConfigList.Items.Add(f);

            sb.AppendLine("TAC Panel discovery report — " + DateTime.Now.ToString("O"));
            sb.AppendLine("Running GameLoop-like processes: " + (processes.Count == 0 ? "none detected" : string.Join(", ", processes)));
            sb.AppendLine("Installation candidates:");
            foreach (var r in roots) sb.AppendLine("  " + r);
            sb.AppendLine("Candidate settings files (keywords only; NOT confirmed as active):");
            foreach (var f in _configCandidates) sb.AppendLine("  " + f);
            File.WriteAllText(_reportPath, sb.ToString());
            StatusTitle.Text = $"Scan complete • {roots.Count} install candidates • {_configCandidates.Count} possible config files";
            StatusDetail.Text = processes.Count > 0 ? "Emulator-like process detected: " + string.Join(", ", processes) : "No known emulator process detected. Start GameLoop and scan again if needed.";
            ReportText.Text = "Report saved: " + _reportPath + "\nFiles are candidates only; the active settings file still needs verification.";
        }
        catch (Exception ex)
        {
            MessageBox.Show("Scan failed: " + ex.Message, "TAC Panel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static int SafePid(Process p) { try { return p.Id; } catch { return -1; } }

    private static IEnumerable<string> SafeDirectories(string root, int maxDepth)
    {
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0)
        {
            var (path, depth) = queue.Dequeue();
            yield return path;
            if (depth >= maxDepth) continue;
            IEnumerable<string> dirs;
            try { dirs = Directory.EnumerateDirectories(path).Take(150).ToArray(); } catch { continue; }
            foreach (var d in dirs)
            {
                if (new DirectoryInfo(d).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                queue.Enqueue((d, depth + 1));
            }
        }
    }

    private static IEnumerable<string> SafeFiles(string root, int maxDepth)
    {
        foreach (var dir in SafeDirectories(root, maxDepth))
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir).Take(250).ToArray(); } catch { continue; }
            foreach (var file in files) yield return file;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var prefs = new { Resolution = ((ComboBoxItem)ResolutionBox.SelectedItem).Content.ToString(), Dpi = ((ComboBoxItem)DpiBox.SelectedItem).Content.ToString(), Renderer = ((ComboBoxItem)RendererBox.SelectedItem).Content.ToString(), SavedAt = DateTime.Now };
        var path = Path.Combine(_appDir, "panel-preferences.json");
        File.WriteAllText(path, JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true }));
        MessageBox.Show("Panel preferences saved.\nThese are not yet applied to GameLoop; use Scan and share the discovery report to implement a verified adapter.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LoadPrefs()
    {
        try
        {
            var p = Path.Combine(_appDir, "panel-preferences.json");
            if (!File.Exists(p)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(p));
            var root = doc.RootElement;
            SetCombo(ResolutionBox, root.GetProperty("Resolution").GetString());
            SetCombo(DpiBox, root.GetProperty("Dpi").GetString());
            SetCombo(RendererBox, root.GetProperty("Renderer").GetString());
        }
        catch { }
    }

    private static void SetCombo(ComboBox box, string? value)
    {
        foreach (ComboBoxItem item in box.Items)
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase)) { box.SelectedItem = item; return; }
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigList.SelectedItem is not string file || !File.Exists(file))
        {
            MessageBox.Show("Select a candidate file first.", "Backup", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        try
        {
            var dest = Path.Combine(_backupDir, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "_" + Path.GetFileName(file) + ".bak");
            File.Copy(file, dest, false);
            File.WriteAllText(dest + ".source.txt", file);
            ReportText.Text = "Backup created:\n" + dest;
            MessageBox.Show("Backup created. No source file was changed.", "Backup complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show("Backup failed: " + ex.Message, "Backup", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_appDir);
        Process.Start(new ProcessStartInfo("explorer.exe", _appDir) { UseShellExecute = true });
    }
}
