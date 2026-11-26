using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;

namespace LocalizationSystem.Editor.Xlsx
{
    public static class XlsxColumnMapping
    {
        private static readonly string[] s_keyTitles = {"key", "keys", "key name", "ключ"};
        private static readonly string[] s_keyIdTitles = {"id", "key id", "keyid"};
        private static readonly string[] s_keyCommentTitles =
            {"comment", "comments", "key comment", "key comments", "shared comment", "shared comments", "description", "context", "комментарий"};
        private static readonly string[] s_commentSuffixes = {" comments", " comment", " (comment)", " комментарий"};

        /// <summary>
        /// Creates a column mapping from header titles: "Key", "Id", "Comment", locale codes or names ("en", "English", "English (en)")
        /// and locale comments ("en Comment", "English Comments").
        /// </summary>
        public static List<XlsxColumn> FromHeader(IReadOnlyList<string> header, IReadOnlyList<Locale> locales, List<string> unmapped = null)
        {
            var columns = new List<XlsxColumn>();
            for (int i = 0; i < header.Count; i++)
            {
                string title = header[i]?.Trim();
                if (string.IsNullOrEmpty(title))
                    continue;

                string column = XlsxWorkbook.ColumnName(i);
                if (Contains(s_keyTitles, title))
                    columns.Add(new XlsxColumn(column, XlsxColumnType.Key));
                else if (Contains(s_keyIdTitles, title))
                    columns.Add(new XlsxColumn(column, XlsxColumnType.KeyId));
                else if (Contains(s_keyCommentTitles, title))
                    columns.Add(new XlsxColumn(column, XlsxColumnType.KeyComment));
                else if (TryMatchLocale(title, locales, out Locale locale, out bool isComment))
                    columns.Add(new XlsxColumn(column, isComment ? XlsxColumnType.LocaleComment : XlsxColumnType.Locale, locale.Identifier));
                else
                    unmapped?.Add($"{column}: {title}");
            }

            return columns;
        }

        /// <summary>
        /// Reads the first row of the sheet and creates the mapping for the project locales.
        /// </summary>
        public static List<XlsxColumn> FromSheetHeader(string path, string sheet)
        {
            using XlsxWorkbook workbook = XlsxWorkbook.Open(path);
            if (string.IsNullOrEmpty(sheet))
                sheet = workbook.SheetNames.FirstOrDefault();
            string[] header = workbook.ReadSheet(sheet, 1).FirstOrDefault() ?? Array.Empty<string>();

            var unmapped = new List<string>();
            List<XlsxColumn> columns = FromHeader(header, LocalizationEditorSettings.GetLocales(), unmapped);
            if (unmapped.Count > 0)
                Debug.LogWarning($"Could not map xlsx columns of the sheet '{sheet}': {string.Join(", ", unmapped)}");
            return columns;
        }

        public static void Write(SerializedProperty list, IReadOnlyList<XlsxColumn> columns)
        {
            list.ClearArray();
            for (int i = 0; i < columns.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_column").stringValue = columns[i].Column;
                element.FindPropertyRelative("_type").enumValueIndex = (int) columns[i].Type;
                element.FindPropertyRelative("_locale.m_Code").stringValue = columns[i].Locale.Code ?? string.Empty;
            }
        }

        private static bool TryMatchLocale(string title, IReadOnlyList<Locale> locales, out Locale result, out bool isComment)
        {
            isComment = false;
            foreach (string suffix in s_commentSuffixes)
            {
                if (title.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    title = title.Substring(0, title.Length - suffix.Length).Trim();
                    isComment = true;
                    break;
                }
            }

            foreach (Locale locale in locales)
            {
                if (locale && GetLocaleTitles(locale).Any(t => string.Equals(t, title, StringComparison.OrdinalIgnoreCase)))
                {
                    result = locale;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static IEnumerable<string> GetLocaleTitles(Locale locale)
        {
            yield return locale.Identifier.Code;
            yield return locale.LocaleName;
            CultureInfo culture = locale.Identifier.CultureInfo;
            if (culture != null)
            {
                yield return culture.Name;
                yield return culture.EnglishName;
                yield return culture.NativeName;
            }
        }

        private static bool Contains(string[] titles, string title) => titles.Any(t => string.Equals(t, title, StringComparison.OrdinalIgnoreCase));
    }
}
