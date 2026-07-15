using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MCLauncher {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        protected override void OnStartup(StartupEventArgs e) {
            base.OnStartup(e);

            if (e.Args != null && e.Args.Contains("--trial-unlock"))
            {
                TrialUnlockHelper.ApplyTrialUnlock();
                Shutdown();
                return;
            }

            // Setup error handling
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show(
                    $"A critical error occurred:\n\n{ex?.Message}\n\nCheck Log.txt for details.",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show(
                    $"An error occurred:\n\n{args.Exception.Message}\n\nCheck Log.txt for details.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            try
            {
                // Load preferences to check for custom data path
                // Check multiple locations for preferences.json
                string appDataPath;
                string defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MinecraftVersionLauncher");
                
                // Try to find and load preferences
                Preferences loadedPrefs = null;
                
                // First check default AppData location
                string defaultPrefsPath = Path.Combine(defaultPath, "preferences.json");
                if (File.Exists(defaultPrefsPath))
                {
                    try
                    {
                        string prefsContent = File.ReadAllText(defaultPrefsPath);
                        loadedPrefs = Newtonsoft.Json.JsonConvert.DeserializeObject<Preferences>(prefsContent);
                        System.Diagnostics.Debug.WriteLine($"Loaded preferences from default location: {defaultPrefsPath}");
                        if (loadedPrefs != null && !string.IsNullOrEmpty(loadedPrefs.LauncherDataPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"Custom launcher data path found: {loadedPrefs.LauncherDataPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load preferences from default location: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Preferences not found at default location: {defaultPrefsPath}");
                }
                
                // If not found or failed, check current directory
                if (loadedPrefs == null && File.Exists("preferences.json"))
                {
                    try
                    {
                        string prefsContent = File.ReadAllText("preferences.json");
                        loadedPrefs = Newtonsoft.Json.JsonConvert.DeserializeObject<Preferences>(prefsContent);
                        System.Diagnostics.Debug.WriteLine("Loaded preferences from current directory");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to load preferences from current directory: {ex.Message}");
                    }
                }
                
                // Determine the actual data path
                if (loadedPrefs != null && !string.IsNullOrEmpty(loadedPrefs.LauncherDataPath))
                {
                    appDataPath = loadedPrefs.LauncherDataPath;
                    System.Diagnostics.Debug.WriteLine($"Using custom data path: {appDataPath}");
                }
                else
                {
                    appDataPath = defaultPath;
                    System.Diagnostics.Debug.WriteLine($"Using default data path: {appDataPath}");
                }
                
                if (!Directory.Exists(appDataPath))
                {
                    Directory.CreateDirectory(appDataPath);
                }

                // Change working directory to the determined path
                Directory.SetCurrentDirectory(appDataPath);

                // Launch main window
                Debug.WriteLine("Creating main window");
                var mainWindow = new ModernMainWindow();
                
                // Set as main window and change shutdown mode
                Application.Current.MainWindow = mainWindow;
                Application.Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                
                Debug.WriteLine("Showing main window");
                mainWindow.Show();
                Debug.WriteLine("Main window shown successfully");
                Debug.WriteLine($"Main window IsVisible: {mainWindow.IsVisible}");
                Debug.WriteLine($"Main window IsLoaded: {mainWindow.IsLoaded}");
                Debug.WriteLine($"Application windows count: {Application.Current.Windows.Count}");
                
                // Keep the application running
                Debug.WriteLine("OnStartup completed, application should remain running");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FATAL ERROR: {ex}");
                MessageBox.Show(
                    $"Failed to start the application:\n\n{ex.Message}\n\nDetails have been written to Log.txt",
                    "Startup Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
            }
        }

    }
}
