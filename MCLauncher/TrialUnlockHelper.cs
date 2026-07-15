using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Principal;
using Microsoft.Win32;

namespace MCLauncher
{
    public static class TrialUnlockHelper
    {
        private const string MC_BYPASS_DIR = @"C:\Program Files\MCBypass";
        private const string BACKUP_DIR = @"C:\Program Files\MCBypass\backup";
        private const string SYSTEM_STORE_DLL = @"C:\Windows\system32\Windows.ApplicationModel.Store.dll";
        private const string MARKER_DLL = @"C:\Program Files\MCBypass\Windows.ApplicationModel.Store.dll";
        private const string CRACKED_DLL_URL = "https://raw.githubusercontent.com/rhuda21/mcbypass/main/Windows.ApplicationModel.Store.dll";

        public static bool IsAdministrator()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsTrialUnlocked()
        {
            return File.Exists(MARKER_DLL);
        }

        public static void ApplyTrialUnlock()
        {
            try
            {
                if (IsTrialUnlocked())
                {
                    System.Windows.MessageBox.Show(
                        Localization.Get("TrialAlreadyUnlockedMessage"),
                        Localization.Get("TrialAlreadyUnlocked"),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }

                Directory.CreateDirectory(MC_BYPASS_DIR);
                Directory.CreateDirectory(BACKUP_DIR);

                var crackedPath = MARKER_DLL;
                using (var wc = new WebClient())
                    wc.DownloadFile(CRACKED_DLL_URL, crackedPath);

                KillStoreProcesses();

                if (File.Exists(SYSTEM_STORE_DLL))
                {
                    var backupPath = Path.Combine(BACKUP_DIR, "Windows.ApplicationModel.Store.dll");
                    if (!File.Exists(backupPath))
                        File.Copy(SYSTEM_STORE_DLL, backupPath, true);

                    RunCmd($"TAKEOWN /F \"{SYSTEM_STORE_DLL}\"");
                    RunCmd($"icacls \"{SYSTEM_STORE_DLL}\" /grant {Environment.UserName}:F");
                    File.Delete(SYSTEM_STORE_DLL);
                }

                File.Copy(crackedPath, SYSTEM_STORE_DLL, true);

                System.Windows.MessageBox.Show(
                    Localization.Get("TrialUnlockSuccessMessage"),
                    Localization.Get("TrialUnlockSuccess"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"{Localization.Get("TrialUnlockFailedMessage")}\n\n{ex.Message}",
                    Localization.Get("Error"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        public static void RunElevatedAndWait()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule.FileName,
                    Arguments = "--trial-unlock",
                    Verb = "runas",
                    UseShellExecute = true
                };
                using (var p = Process.Start(psi))
                {
                    p?.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"{Localization.Get("TrialUnlockFailedMessage")}\n\n{ex.Message}",
                    Localization.Get("Error"),
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private static void KillStoreProcesses()
        {
            var processes = new[]
            {
                "Gamebar.exe",
                "RuntimeBroker.exe",
                "Minecraft.Windows.exe",
                "WinStore.App.exe",
                "PhoneExperienceHost.exe",
                "NanaZip.Modern.exe",
                "StoreExperienceHost.exe"
            };
            foreach (var name in processes)
                RunCmd($"taskkill /F /IM {name}");
        }

        private static void RunCmd(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + arguments)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var p = Process.Start(psi))
                {
                    p?.WaitForExit();
                }
            }
            catch
            {
            }
        }
    }
}
