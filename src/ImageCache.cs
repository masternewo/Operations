using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Operations.Client
{
    /// <summary>
    /// Disk cache for briefing images, keyed by the SHA-256 the server reports for each file. A player downloads an image once;
    /// afterwards it is read from BepInEx/cache/Operations/media and the server is only asked again when the admin replaces
    /// the file (new hash). Every cached file is re-hashed before use, so a damaged or edited file is simply ignored.
    /// All methods are safe to call from a worker thread.
    /// </summary>
    internal static class ImageCache
    {
        private static readonly Regex Hex64 = new Regex("^[a-f0-9]{64}$", RegexOptions.Compiled);
        private const int MaxFiles = 60;
        private const long MaxBytes = 80L * 1024 * 1024;

        private static string Dir
        {
            get { return Path.Combine(BepInEx.Paths.CachePath, Path.Combine("Operations", "media")); }
        }

        public static bool IsHash(string sha) { return sha != null && Hex64.IsMatch(sha); }

        public static string HashOf(byte[] bytes)
        {
            using (SHA256 h = SHA256.Create())
                return string.Concat(h.ComputeHash(bytes).Select(b => b.ToString("x2")).ToArray());
        }

        /// <summary>The cached bytes for this hash, or null (missing, unreadable or not matching the hash).</summary>
        public static byte[] TryGet(string sha)
        {
            if (!IsHash(sha)) return null;
            try
            {
                string file = Path.Combine(Dir, sha + ".img");
                if (!File.Exists(file)) return null;
                byte[] bytes = File.ReadAllBytes(file);
                if (HashOf(bytes) != sha) { TryDelete(file); return null; }
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow); // most recently used stays when the cache is trimmed
                return bytes;
            }
            catch (Exception e) { OperationsPlugin.Log.LogDebug("Image cache read failed: " + e.Message); return null; }
        }

        /// <summary>Stores downloaded bytes under their real hash and returns that hash (null when the cache is not writable).</summary>
        public static string Put(byte[] bytes)
        {
            try
            {
                string sha = HashOf(bytes);
                Directory.CreateDirectory(Dir);
                string file = Path.Combine(Dir, sha + ".img"); string tmp = file + ".tmp";
                File.WriteAllBytes(tmp, bytes);
                if (File.Exists(file)) File.Delete(file);
                File.Move(tmp, file);
                Trim();
                return sha;
            }
            catch (Exception e) { OperationsPlugin.Log.LogDebug("Image cache write failed: " + e.Message); return null; }
        }

        private static void Trim()
        {
            try
            {
                FileInfo[] files = new DirectoryInfo(Dir).GetFiles("*.img").OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
                long total = 0; int n = 0;
                foreach (FileInfo f in files)
                {
                    n++; total += f.Length;
                    if (n > MaxFiles || total > MaxBytes) TryDelete(f.FullName);
                }
            }
            catch (Exception) { /* trimming is best effort */ }
        }

        private static void TryDelete(string file) { try { File.Delete(file); } catch (Exception) { /* in use: next time */ } }
    }
}
