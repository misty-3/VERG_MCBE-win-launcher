using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MCLauncher
{
    public static class BfixInjector
    {
        private const string BaseUrl = "https://github.com/misty-3/VERG_MCBE-win-launcher/raw/refs/heads/main/bfix/";

        private sealed class BfixFile
        {
            public readonly string Name;
            public readonly long ExpectedSize;
            public BfixFile(string name, long expectedSize) { Name = name; ExpectedSize = expectedSize; }
        }

        private static readonly BfixFile[] Files = new[]
        {
            new BfixFile("dlllist.txt", 0),
            new BfixFile("OnlineFix.ini", 0),
            new BfixFile("OnlineFix64.dll", 10817536),
            new BfixFile("winmm.dll", 506368)
        };

        private static readonly HttpClient Http = new HttpClient();
        private static readonly SemaphoreSlim CacheLock = new SemaphoreSlim(1, 1);

        static BfixInjector()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch { }
        }

        private static string CacheDir
        {
            get { return Path.Combine(Directory.GetCurrentDirectory(), "bfix_cache"); }
        }

        public static bool IsAlreadyUnlocked(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;

            foreach (var f in Files)
            {
                if (!File.Exists(Path.Combine(directory, f.Name)))
                    return false;
            }

            return true;
        }

        public static async Task InjectToMinecraftAsync(string minecraftDirectory)
        {
            if (string.IsNullOrEmpty(minecraftDirectory) || !Directory.Exists(minecraftDirectory))
                throw new DirectoryNotFoundException("Game directory not found: " + minecraftDirectory);

            if (IsAlreadyUnlocked(minecraftDirectory))
                return;

            await EnsureCacheAsync().ConfigureAwait(false);

            foreach (var f in Files)
            {
                string dst = Path.Combine(minecraftDirectory, f.Name);
                if (File.Exists(dst))
                    continue;

                File.Copy(Path.Combine(CacheDir, f.Name), dst, false);
            }
        }

        private static async Task EnsureCacheAsync()
        {
            await CacheLock.WaitAsync().ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(CacheDir);

                foreach (var f in Files)
                {
                    string path = Path.Combine(CacheDir, f.Name);
                    if (IsValid(path, f))
                        continue;

                    await DownloadAsync(BaseUrl + f.Name, path).ConfigureAwait(false);

                    if (!IsValid(path, f))
                    {
                        TryDelete(path);
                        throw new IOException("Downloaded unlock file failed validation: " + f.Name);
                    }
                }
            }
            finally
            {
                CacheLock.Release();
            }
        }

        private static bool IsValid(string path, BfixFile f)
        {
            if (!File.Exists(path))
                return false;

            long len = new FileInfo(path).Length;
            return f.ExpectedSize > 0 ? len == f.ExpectedSize : len > 0;
        }

        private static async Task DownloadAsync(string url, string destPath)
        {
            string tmp = destPath + ".part";
            TryDelete(tmp);

            using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();

                using (var input = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await input.CopyToAsync(output).ConfigureAwait(false);
                }
            }

            TryDelete(destPath);
            File.Move(tmp, destPath);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }
    }
}
