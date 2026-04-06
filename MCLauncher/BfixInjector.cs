using System;
using System.IO;
using System.Reflection;

namespace MCLauncher
{
    /// <summary>
    /// Handles extraction and injection of bfix files into Minecraft installations
    /// </summary>
    public static class BfixInjector
    {
        // Embedded resource names (namespace.folder.filename)
        private static readonly string[] BFIX_FILES = new[]
        {
            "MCLauncher.bfix.dlllist.txt",
            "MCLauncher.bfix.OnlineFix.ini",
            "MCLauncher.bfix.OnlineFix64.dll",
            "MCLauncher.bfix.winmm.dll"
        };
        
        /// <summary>
        /// Check if all bfix files already exist in the directory
        /// </summary>
        public static bool IsAlreadyUnlocked(string directory)
        {
            return AllFilesExist(directory);
        }
        
        /// <summary>
        /// Check if all bfix files already exist in the directory
        /// </summary>
        private static bool AllFilesExist(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;
            
            foreach (var resourceName in BFIX_FILES)
            {
                var fileName = resourceName.Replace("MCLauncher.bfix.", "");
                var targetPath = Path.Combine(directory, fileName);
                
                if (!File.Exists(targetPath))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Extract bfix files to a Minecraft installation directory
        /// </summary>
        public static void InjectToMinecraft(string minecraftDirectory)
        {
            if (string.IsNullOrEmpty(minecraftDirectory) || !Directory.Exists(minecraftDirectory))
                return;
            
            // Check if all files already exist yes
            if (AllFilesExist(minecraftDirectory))
            {
                System.Diagnostics.Debug.WriteLine($"Bfix files already exist in {minecraftDirectory}, skipping injection");
                return;
            }
            
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                
                foreach (var resourceName in BFIX_FILES)
                {
                    try
                    {
                        // Get the filename without the namespace prefix
                        var fileName = resourceName.Replace("MCLauncher.bfix.", "");
                        var targetPath = Path.Combine(minecraftDirectory, fileName);
                        
                        // Skip if file already exists
                        if (File.Exists(targetPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"File already exists, skipping: {fileName}");
                            continue;
                        }
                        
                        // Extract embedded resource
                        using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (resourceStream == null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Resource not found: {resourceName}");
                                continue;
                            }
                            
                            // Write to target file
                            using (FileStream fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                            {
                                resourceStream.CopyTo(fileStream);
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"Injected: {fileName} -> {targetPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to inject {resourceName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Bfix injection failed for {minecraftDirectory}: {ex}");
            }
        }
    }
}
