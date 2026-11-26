using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine;

namespace LocalizationSystem.Editor.Google
{
    public static class GoogleSheetsUtilities
    {
        private const string ContextPath = "CONTEXT/" + nameof(StringTableCollection) + "/Add Configured Google Sheet Extension";

        [MenuItem(LocalizationPath.Menu + "Tables/Pull all from google")]
        public static void PullAllFromGoogle()
        {
            foreach (StringTableCollection table in LocalizationEditorSettings.GetStringTableCollections())
            {
                GoogleSheetsExtension[] extensions = table.Extensions.OfType<GoogleSheetsExtension>().ToArray();
                if (extensions.Length == 0)
                    continue;

                // Clear once per collection, otherwise every next extension wipes the data pulled by the previous one.
                table.ClearAllEntries();
                foreach (GoogleSheetsExtension google in extensions)
                {
                    try
                    {
                        GoogleSheets googleSheets = new(google.SheetsServiceProvider) {SpreadSheetId = google.SpreadsheetId};
                        googleSheets.PullIntoStringTableCollection(google.SheetId, table, google.Columns);
                        Debug.Log($"The table of the {table.name} updated successfully", table);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, table);
                    }
                }
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

            GoogleSheetsExtension googleExtension = collection.Extensions.OfType<GoogleSheetsExtension>().FirstOrDefault();
            if (googleExtension == null)
            {
                googleExtension = new GoogleSheetsExtension();
                collection.AddExtension(googleExtension);
            }

            if (!googleExtension.SheetsServiceProvider)
            {
                googleExtension.SheetsServiceProvider = coreSettings.SheetsProvider as SheetsServiceProvider;
            }

            googleExtension.Columns.Clear();
            googleExtension.Columns.AddRange(CloneColumns(coreSettings.Mapping));
            googleExtension.SpreadsheetId = coreSettings.SheetId;
            EditorUtility.SetDirty(collection);
        }

        /// <summary>
        /// Columns are stored by [SerializeReference], so the same instances must not be shared between different assets.
        /// </summary>
        internal static IEnumerable<SheetColumn> CloneColumns(IEnumerable<object> columns) =>
            columns == null
                ? Enumerable.Empty<SheetColumn>()
                : columns.OfType<SheetColumn>().Select(c => (SheetColumn) JsonUtility.FromJson(JsonUtility.ToJson(c), c.GetType()));
    }
}