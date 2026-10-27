using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Installer
{
    const string Product = "QCY OpenControls";
    const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\QCYOpenControls";
    static readonly string InstallDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "QCYOpenControls");
    static readonly string[] Payload = { "QCY OpenControls.exe", "README.md", "LICENSE" };
    static string Executable { get { return Assembly.GetExecutingAssembly().Location; } }

    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            if (args.Length == 2 && args[0] == "--self-test") { SelfTest(args[1]); return 0; }
            if (args.Length > 0 && args[0] == "--uninstall-worker")
            {
                Thread.Sleep(1200);
                Uninstall(InstallDirectory, RegistryPath, DesktopShortcut(), MenuShortcut());
                MessageBox.Show("QCY OpenControls desinstalado. Suas preferências foram preservadas.", Product, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            if (args.Length > 0 && args[0] == "--uninstall")
            {
                if (MessageBox.Show("Desinstalar o QCY OpenControls deste usuário?", Product, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
                EnsureClosed(InstallDirectory);
                // Execute the removal from a temporary copy so the installed uninstaller can be deleted.
                string temporary = Path.Combine(Path.GetTempPath(), "QCYOpenControls-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
                File.Copy(Executable, temporary);
                Process.Start(new ProcessStartInfo(temporary, "--uninstall-worker") { UseShellExecute = true });
                return 0;
            }
            Application.Run(new SetupForm());
            return 0;
        }
        catch (Exception error)
        {
            if (args.Length == 2 && args[0] == "--self-test") File.WriteAllText(args[1], error.ToString());
            else MessageBox.Show(error.Message, Product, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    static string DesktopShortcut() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Product + ".lnk"); }
    static string MenuShortcut() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Product + ".lnk"); }

    static void EnsureClosed(string directory)
    {
        foreach (Process process in Process.GetProcessesByName("QCY OpenControls"))
        {
            using (process)
            {
                string path;
                try { path = process.MainModule.FileName; }
                catch { throw new IOException("Não foi possível verificar o aplicativo aberto. Feche o QCY OpenControls pela bandeja e tente novamente."); }
                if (String.Equals(path, Path.Combine(directory, "QCY OpenControls.exe"), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Feche o QCY OpenControls pelo menu da bandeja antes de continuar.");
            }
        }
    }

    static void CreateShortcut(string path, string target, string directory)
    {
        object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        object shortcut = null;
        try
        {
            shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            Type type = shortcut.GetType();
            type.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
            type.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { directory });
            type.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "QCY H3 Pro controls" });
            type.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
        }
    }

    static void Install(string directory, string registry, string desktop, string menu)
    {
        EnsureClosed(directory);
        Directory.CreateDirectory(directory);
        foreach (string name in Payload)
        {
            using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload." + name))
            {
                if (source == null) throw new IOException("Arquivo ausente no instalador: " + name);
                using (FileStream destination = File.Create(Path.Combine(directory, name))) source.CopyTo(destination);
            }
        }
        string uninstaller = Path.Combine(directory, "Uninstall.exe");
        File.Copy(Executable, uninstaller, true);
        File.WriteAllText(Path.Combine(directory, "installation.marker"), "QCYOpenControls installer v1");
        string target = Path.Combine(directory, "QCY OpenControls.exe");
        CreateShortcut(menu, target, directory);
        if (desktop != null) CreateShortcut(desktop, target, directory);
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registry))
        {
            key.SetValue("DisplayName", Product);
            key.SetValue("DisplayVersion", "1.0.1");
            key.SetValue("InstallLocation", directory);
            key.SetValue("DisplayIcon", target);
            key.SetValue("UninstallString", "\"" + uninstaller + "\" --uninstall");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            long size = 0;
            foreach (string file in Directory.GetFiles(directory)) size += new FileInfo(file).Length;
            key.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
        }
    }

    static void RemoveShortcut(string path, string target)
    {
        if (!File.Exists(path)) return;
        object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
        object shortcut = null;
        try
        {
            shortcut = shell.GetType().InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { path });
            string actual = (string)shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null);
            if (String.Equals(actual, target, StringComparison.OrdinalIgnoreCase)) File.Delete(path);
        }
        finally
        {
            if (shortcut != null) Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
        }
    }

    static void Uninstall(string directory, string registry, string desktop, string menu)
    {
        EnsureClosed(directory);
        string marker = Path.Combine(directory, "installation.marker");
        if (!File.Exists(marker) || File.ReadAllText(marker) != "QCYOpenControls installer v1") throw new IOException("Instalação não reconhecida. Nenhum arquivo foi removido.");
        string target = Path.Combine(directory, "QCY OpenControls.exe");
        RemoveShortcut(desktop, target);
        RemoveShortcut(menu, target);
        string startup = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "QCYOpenControls.vbs");
        if (File.Exists(startup) && File.ReadAllText(startup).IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0) File.Delete(startup);
        foreach (string name in Payload) File.Delete(Path.Combine(directory, name));
        File.Delete(Path.Combine(directory, "Uninstall.exe"));
        File.Delete(marker);
        Registry.CurrentUser.DeleteSubKeyTree(registry, false);
        if (Directory.GetFileSystemEntries(directory).Length == 0) Directory.Delete(directory);
    }

    static void SelfTest(string report)
    {
        string id = Guid.NewGuid().ToString("N");
        string root = Path.Combine(Path.GetTempPath(), "QCYOpenControls-SetupTest-" + id);
        string directory = Path.Combine(root, "app");
        string registry = RegistryPath + "-Test-" + id;
        Directory.CreateDirectory(root);
        string desktop = Path.Combine(root, "Desktop.lnk");
        string menu = Path.Combine(root, "Menu.lnk");
        try
        {
            Install(directory, registry, desktop, menu);
            foreach (string name in Payload)
            {
                using (SHA256 hash = SHA256.Create())
                using (Stream embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload." + name))
                using (Stream installed = File.OpenRead(Path.Combine(directory, name)))
                    if (Convert.ToBase64String(hash.ComputeHash(embedded)) != Convert.ToBase64String(hash.ComputeHash(installed))) throw new Exception("Payload inválido: " + name);
            }
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry))
                if (key == null || (string)key.GetValue("InstallLocation") != directory) throw new Exception("Registro inválido.");
            if (!File.Exists(desktop) || !File.Exists(menu)) throw new Exception("Atalhos ausentes.");
            Install(directory, registry, desktop, menu);
            File.WriteAllText(Path.Combine(directory, "keep.txt"), "preserve unrelated files");
            Uninstall(directory, registry, desktop, menu);
            if (File.Exists(desktop) || File.Exists(menu) || File.Exists(Path.Combine(directory, "QCY OpenControls.exe"))) throw new Exception("Desinstalação incompleta.");
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registry)) if (key != null) throw new Exception("Registro não removido.");
            if (!File.Exists(Path.Combine(directory, "keep.txt"))) throw new Exception("Arquivo independente removido.");
            File.Delete(Path.Combine(directory, "keep.txt"));
            Directory.Delete(directory);
            Directory.Delete(root);
            File.WriteAllText(report, "PASS: instalação, integridade SHA-256, atalhos, registro, reinstalação e desinstalação com preservação de arquivos independentes.", Encoding.UTF8);
        }
        catch { throw; }
    }

    sealed class SetupForm : Form
    {
        readonly CheckBox desktop = new CheckBox { Text = "Criar atalho na área de trabalho", Checked = true, AutoSize = true, Location = new Point(30, 174) };
        readonly CheckBox launch = new CheckBox { Text = "Abrir ao concluir", Checked = true, AutoSize = true, Location = new Point(30, 207) };
        readonly Button install = new Button { Text = "Instalar", Location = new Point(362, 282), Size = new Size(135, 36) };
        readonly Label status = new Label { Text = "Pronto para instalar.", Location = new Point(30, 254), Size = new Size(470, 24) };
        public SetupForm()
        {
            Text = "Instalar " + Product;
            ClientSize = new Size(530, 344);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            Controls.Add(new Label { Text = Product, Font = new Font("Segoe UI", 23, FontStyle.Bold), Location = new Point(25, 22), AutoSize = true });
            Controls.Add(new Label { Text = "QCY H3 Pro controls · v1.0.1", Location = new Point(30, 75), AutoSize = true });
            Controls.Add(new Label { Text = "Instalação para seu usuário:\n" + InstallDirectory, Location = new Point(30, 112), Size = new Size(470, 52), AutoEllipsis = true });
            Controls.Add(desktop); Controls.Add(launch); Controls.Add(status); Controls.Add(install);
            AcceptButton = install;
            install.Click += delegate
            {
                install.Enabled = false;
                status.Text = "Instalando...";
                status.Refresh();
                try
                {
                    Install(InstallDirectory, RegistryPath, desktop.Checked ? DesktopShortcut() : null, MenuShortcut());
                    status.Text = "Instalação concluída.";
                    if (launch.Checked) Process.Start(new ProcessStartInfo(Path.Combine(InstallDirectory, "QCY OpenControls.exe")) { WorkingDirectory = InstallDirectory, UseShellExecute = true });
                    MessageBox.Show("QCY OpenControls instalado! Você pode removê-lo em Aplicativos instalados do Windows.", Product, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }
                catch (Exception error)
                {
                    status.Text = "Não foi possível concluir a instalação.";
                    MessageBox.Show(error.Message, Product, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    install.Enabled = true;
                }
            };
        }
    }
}
