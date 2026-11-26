using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace LocalizationSystem.Editor.Xlsx
{
    public static class XlsxUtilities
    {
        private const string ContextPath = "CONTEXT/" + nameof(StringTableCollection) + "/Add Configured Xlsx Extension";

        [MenuItem(LocalizationPath.Menu + "Tables/Pull all from xlsx")]
        public static void PullAllFromXlsx()
        {
            var collections = LocalizationEditorSettings.GetStringTableCollections()
                .Select(c => (Collection: c, Extensions: c.Extensions.OfType<XlsxExtension>().ToArray()))
                .Where(c => c.Extensions.Length > 0)
                .ToArray();

            // The same file is usually shared by several collections, so it is opened only once.
            var workbooks = new Dictionary<string, XlsxWorkbook>(StringComparer.OrdinalIgnoreCase);
            try
            {
                for (int i = 0; i < collections.Length; i++)
                {
                    StringTableCollection collection = collections[i].Collection;
                    EditorUtility.DisplayProgressBar("Pull from xlsx", collection.TableCollectionName, (float) i / collections.Length);
                    foreach (XlsxExtension extension in collections[i].Extensions)
                    {
                        try
                        {
                            string path = extension.FilePath;
                            if (!XlsxSheetCache.IsXlsx(path))
                                throw new InvalidOperationException(
                                    $"The xlsx file is not assigned for the collection {collection.TableCollectionName}");

                            if (!workbooks.TryGetValue(path, out XlsxWorkbook workbook))
                                workbooks[path] = workbook = XlsxWorkbook.Open(path);

                            XlsxImporter.Pull(collection, workbook, extension.Sheet, extension.Columns, extension.HeaderRows,
                                extension.RemoveMissingKeys);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e, collection);
                        }
                    }
                }
            }
            finally
            {
                foreach (XlsxWorkbook workbook in workbooks.Values)
                    workbook.Dispose();
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem(ContextPath)]
        public static void AddAndConfigureExtension(MenuCommand command)
        {
            if (command.context is not StringTableCollection collection)
                return;
            LocalizationSettings coreSettings = LocalizationSettings.Instance;
            if (!coreSettings)
                return;

            Undo.RecordObject(collection, "Configure xlsx extension");
            XlsxExtension extension = collection.Extensions.OfType<XlsxExtension>().FirstOrDefault();
            if (extension == null)
            {
                extension = new XlsxExtension();
                collection.AddExtension(extension);
            }

            extension.File = coreSettings.XlsxFile;
            extension.HeaderRows = coreSettings.XlsxHeaderRows;
            extension.Columns.Clear();
            extension.Columns.AddRange(coreSettings.XlsxColumns.Where(c => c != null).Select(c => c.Clone()));

            string path = extension.FilePath;
            if (string.IsNullOrEmpty(extension.Sheet) || !XlsxSheetCache.GetSheetNames(path).Contains(extension.Sheet))
            {
                // By default the sheet is expected to be named the same as the collection.
                string sheet = XlsxSheetCache.FindSheet(path, collection.TableCollectionName);
                if (sheet != null)
                    extension.Sheet = sheet;
                else
                    Debug.LogWarning($"The sheet `{collection.TableCollectionName}` is not found in `{path}`, select the sheet manually", collection);
            }

            EditorUtility.SetDirty(collection);
        }
    }
}