using System.Diagnostics;
using System.Security.Principal;

namespace AzarothInstaller;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        bool auto = args.Any(a => a.Equals("--auto", StringComparison.OrdinalIgnoreCase));

        // This installer needs admin rights (installs a database, writes to
        // Program Files, creates services / firewall rules). Re-launch elevated.
        if (!IsAdmin())
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))
                };
                Process.Start(psi);
                return;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show(
                    "Administrator rights are required to install Azaroth Core.\n\n" +
                    "Please right-click setup.exe and choose 'Run as administrator'.",
                    "Azaroth Installer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            catch (Exception)
            {
                // user cancelled the UAC prompt
                return;
            }
        }

        Application.Run(new WizardForm(auto));
    }

    static bool IsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        var pr = new WindowsPrincipal(id);
        return pr.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
