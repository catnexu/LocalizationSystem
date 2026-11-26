using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LocalizationSystem.Editor.Xlsx
{
    /// <summary>
    /// Caches sheet names for inspectors, the file is re-read only when it is changed.
    /// </summary>
    internal static class XlsxSheetCache
    {
        private static readonly Dictionary<string, (DateTime Time, string[] Names)> s_cache = new Dictionary<string, (DateTime, string[])>();

        public static bool IsXlsx(string path) => !string.IsNullOrEmpty(path) && path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);

        public static string[] GetSheetNames(string path)
        {
            if (!IsXlsx(path) || !File.Exists(path))
                return Array.Empty<string>();

            DateTime time = File.GetLastWriteTimeUtc(path);
            if (s_cache.TryGetValue(path, out var cached) && cached.Time == time)
                return cached.Names;

            string[] names;
            try
            {
                using XlsxWorkbook workbook = XlsxWorkbook.Open(path);
                names = workbook.SheetNames.ToArray();
            }
            catch (Exception)
            {
                names = Array.Empty<string>();
            }

            s_cache[path] = (time, names);
            return names;
        }

        public static string FindSheet(string path, string name)
        {
            string[] names = GetSheetNames(path);
            return names.FirstOrDefault(n => n == name) ?? names.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}