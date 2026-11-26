using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;
using SharedTableEntry = UnityEngine.Localization.Tables.SharedTableData.SharedTableEntry;

namespace LocalizationSystem.Editor.Xlsx
{
    /// <summary>
    /// Pulls .xlsx sheet data into a <see cref="StringTableCollection"/>. Merge rules follow the Google Sheets pull:
    /// keys are matched by id (if the id column is mapped) or by name, missing keys are kept or removed, entries are sorted as in the sheet.
    /// </summary>
    public static class XlsxImporter
    {
        public static void Pull(XlsxExtension extension, bool createUndo = false)
        {
            if (extension?.TargetCollection is not StringTableCollection collection)
                throw new InvalidOperationException("The xlsx extension is not attached to a string table collection");

            string path = extension.FilePath;
            if (!XlsxSheetCache.IsXlsx(path))
                throw new InvalidOperationException($"The xlsx file is not assigned for the collection {collection.TableCollectionName}");

            using XlsxWorkbook workbook = XlsxWorkbook.Open(path);
            Pull(collection, workbook, extension.Sheet, extension.Columns, extension.HeaderRows, extension.RemoveMissingKeys, createUndo);
        }

        public static void Pull(StringTableCollection collection,
            XlsxWorkbook workbook,
            string sheet,
            IReadOnlyList<XlsxColumn> columns,
            int headerRows,
            bool removeMissingKeys,
            bool createUndo = false)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));
            if (workbook == null)
                throw new ArgumentNullException(nameof(workbook));
            ValidateColumns(columns, out XlsxColumn keyColumn, out XlsxColumn idColumn);

            List<string[]> rows = workbook.ReadSheet(sheet);
            SharedTableData sharedData = collection.SharedData;
            var modifiedAssets = new List<Object>(collection.StringTables) {sharedData};
            if (createUndo)
                Undo.RegisterCompleteObjectUndo(modifiedAssets.ToArray(), $"Pull `{collection.TableCollectionName}` from xlsx");

            var messages = new StringBuilder();
            StringTable[] tables = ResolveTables(collection, columns, messages);
            int keyIndex = keyColumn.ColumnIndex;
            int idIndex = idColumn?.ColumnIndex ?? -1;

            var processed = new HashSet<long>();
            var sortedEntries = new List<SharedTableEntry>(rows.Count);
            for (int row = Mathf.Max(0, headerRows); row < rows.Count; row++)
            {
                string[] cells = rows[row];
                string key = GetCell(cells, keyIndex)?.Trim();
                string id = GetCell(cells, idIndex)?.Trim();
                if (string.IsNullOrEmpty(key) && string.IsNullOrEmpty(id))
                    continue;

                SharedTableEntry entry = ResolveEntry(sharedData, key, id, row + 1, messages);
                if (entry == null)
                    continue;

                if (!processed.Add(entry.Id))
                {
                    messages.AppendLine($"Row {row + 1}: the entry `{entry.Key}` ({entry.Id}) has already been processed, the row is ignored.");
                    continue;
                }

                sortedEntries.Add(entry);
                for (int i = 0; i < columns.Count; i++)
                {
                    string value = GetCell(cells, columns[i].ColumnIndex);
                    switch (columns[i].Type)
                    {
                        case XlsxColumnType.KeyComment:
                            SetComment(entry.Metadata, value);
                            break;
                        case XlsxColumnType.Locale:
                            PullValue(tables[i], entry.Id, value);
                            break;
                        case XlsxColumnType.LocaleComment:
                            PullComment(tables[i], entry.Id, value);
                            break;
                    }
                }
            }

            MergeEntries(collection, processed, sortedEntries, removeMissingKeys, messages);

            foreach (Object asset in modifiedAssets)
                EditorUtility.SetDirty(asset);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            foreach (Object asset in modifiedAssets)
                AssetDatabase.SaveAssetIfDirty(asset);

            string log = $"The table {collection.TableCollectionName} pulled from xlsx sheet `{sheet}`: {sortedEntries.Count} entries.";
            if (messages.Length > 0)
                Debug.LogWarning(log + "\n" + messages, collection);
            else
                Debug.Log(log, collection);
        }

        private static void ValidateColumns(IReadOnlyList<XlsxColumn> columns, out XlsxColumn keyColumn, out XlsxColumn idColumn)
        {
            if (columns == null || columns.Count == 0)
                throw new ArgumentException("Xlsx column mapping is empty", nameof(columns));

            keyColumn = null;
            idColumn = null;
            var usedColumns = new HashSet<int>();
            var usedLocales = new HashSet<(string, XlsxColumnType)>();
            foreach (XlsxColumn column in columns)
            {
                if (column == null)
                    throw new ArgumentException("Xlsx column mapping contains an empty element", nameof(columns));
                if (column.ColumnIndex < 0)
                    throw new ArgumentException($"Invalid xlsx column name `{column.Column}`", nameof(columns));
                if (!usedColumns.Add(column.ColumnIndex))
                    throw new ArgumentException($"Xlsx column {column.Column} is mapped more than once", nameof(columns));

                switch (column.Type)
                {
                    case XlsxColumnType.Key when keyColumn != null:
                    case XlsxColumnType.KeyId when idColumn != null:
                        throw new ArgumentException($"Xlsx mapping must contain only one {column.Type} column", nameof(columns));
                    case XlsxColumnType.Key:
                        keyColumn = column;
                        break;
                    case XlsxColumnType.KeyId:
                        idColumn = column;
                        break;
                    case XlsxColumnType.Locale:
                    case XlsxColumnType.LocaleComment:
                        if (string.IsNullOrEmpty(column.Locale.Code))
                            throw new ArgumentException($"Locale is not set for xlsx column {column.Column}", nameof(columns));
                        if (!usedLocales.Add((column.Locale.Code, column.Type)))
                            throw new ArgumentException($"Xlsx mapping contains more than one {column.Type} column for {column.Locale.Code}",
                                nameof(columns));
                        break;
                }
            }

            if (keyColumn == null)
                throw new ArgumentException($"Xlsx mapping must contain a {XlsxColumnType.Key} column", nameof(columns));
        }

        private static StringTable[] ResolveTables(StringTableCollection collection, IReadOnlyList<XlsxColumn> columns, StringBuilder messages)
        {
            var tables = new StringTable[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                if (!columns[i].IsLocaleColumn)
                    continue;

                tables[i] = collection.GetTable(columns[i].Locale) as StringTable;
                if (tables[i] == null)
                    messages.AppendLine(
                        $"Column {columns[i].Column}: the collection has no table for the locale {columns[i].Locale.Code}, the column is ignored.");
            }

            return tables;
        }

        private static SharedTableEntry ResolveEntry(SharedTableData sharedData, string key, string idText, int row, StringBuilder messages)
        {
            if (!string.IsNullOrEmpty(idText))
            {
                if (long.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) && id != SharedTableData.EmptyId)
                {
                    SharedTableEntry byId = sharedData.GetEntry(id);
                    if (byId != null)
                    {
                        if (!string.IsNullOrEmpty(key) && byId.Key != key)
                        {
                            if (sharedData.Contains(key))
                                messages.AppendLine($"Row {row}: can not rename `{byId.Key}` to `{key}`, the key is already used by another entry.");
                            else
                                sharedData.RenameKey(id, key);
                        }

                        return byId;
                    }

                    if (string.IsNullOrEmpty(key))
                    {
                        messages.AppendLine($"Row {row}: the entry with id {id} is not found and the key is empty, the row is ignored.");
                        return null;
                    }

                    SharedTableEntry byKey = sharedData.GetEntry(key);
                    if (byKey == null)
                        return sharedData.AddKey(key, id);

                    messages.AppendLine($"Row {row}: the key `{key}` already exists with id {byKey.Id}, the id {id} is ignored.");
                    return byKey;
                }

                // Excel stores long numbers as doubles (15 significant digits), so the id column should be formatted as text.
                messages.AppendLine($"Row {row}: invalid key id `{idText}`. Format the id column as text.");
            }

            if (string.IsNullOrEmpty(key))
                return null;
            return sharedData.GetEntry(key) ?? sharedData.AddKey(key);
        }

        private static void PullValue(StringTable table, long id, string value)
        {
            if (table == null)
                return;
            // Ignore empty cells for entries that do not exist yet.
            if (string.IsNullOrEmpty(value) && table.GetEntry(id) == null)
                return;
            table.AddEntry(id, value ?? string.Empty);
        }

        private static void PullComment(StringTable table, long id, string value)
        {
            if (table == null)
                return;

            StringTableEntry entry = table.GetEntry(id);
            if (entry == null)
            {
                if (string.IsNullOrEmpty(value))
                    return;
                entry = table.AddEntry(id, string.Empty);
            }

            SetComment(entry, value);
        }

        private static void SetComment(IMetadataCollection metadata, string value)
        {
            Comment comment = metadata.GetMetadata<Comment>();
            if (string.IsNullOrEmpty(value))
            {
                if (comment != null)
                    metadata.RemoveMetadata(comment);
            }
            else if (comment != null)
            {
                comment.CommentText = value;
            }
            else
            {
                metadata.AddMetadata(new Comment {CommentText = value});
            }
        }

        private static void MergeEntries(StringTableCollection collection,
            HashSet<long> processed,
            List<SharedTableEntry> sortedEntries,
            bool removeMissingKeys,
            StringBuilder messages)
        {
            var removed = new List<string>();
            foreach (SharedTableEntry entry in collection.SharedData.Entries)
            {
                if (processed.Contains(entry.Id))
                    continue;

                if (!removeMissingKeys || entry.Metadata.HasMetadata<ExcludeEntryFromExport>())
                {
                    // Entries that are missing in the sheet go to the end.
                    sortedEntries.Add(entry);
                    continue;
                }

                removed.Add(entry.Key);
                foreach (StringTable table in collection.StringTables)
                    table.Remove(entry.Id);
            }

            collection.SharedData.Entries = sortedEntries;
            if (removed.Count > 0)
                messages.AppendLine($"Removed missing entries: {string.Join(", ", removed)}");
        }

        private static string GetCell(string[] cells, int index) => index >= 0 && index < cells.Length ? cells[index] : null;
    }
}