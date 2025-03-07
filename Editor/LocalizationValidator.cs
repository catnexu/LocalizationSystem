using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEngine;

namespace LocalizationSystem.Editor
{
    public static class LocalizationValidator
    {
        private static StringTableCollection[] s_tables;

        public static void Validate()
        {
            StringTableCollection[] tables = EditorExtensions.GetAllInstances<StringTableCollection>().ToArray();
            foreach (StringTableCollection table in tables)
            {
                table.StringTables[0].CreateTableEntry();
                foreach (CollectionExtension collectionExtension in table.Extensions)
                {
                    if (collectionExtension is GoogleSheetsExtension google)
                    {
                        table.ClearAllEntries();
                        GoogleSheets googleSheets = new(google.SheetsServiceProvider) {SpreadSheetId = google.SpreadsheetId};
                        googleSheets.PullIntoStringTableCollection(google.SheetId, table, google.Columns);
                        Debug.Log($"The table of the {table.name} updated successfully");
                    }
                }
            }
        }
        

        [MenuItem("CONTEXT/StringTableCollection/Add Configured Google Sheet Extension")]
        public static void AddAndConfigureExtension(MenuCommand command)
        {
            if(command.context is not StringTableCollection collection)
                return;
            LocalizationSettings coreSettings = LocalizationSettings.Instance;
            if(!coreSettings)
                return;

            GoogleSheetsExtension googleExtension = collection.Extensions.FirstOrDefault(e => e is GoogleSheetsExtension) as GoogleSheetsExtension;
            if (googleExtension == null)
            {
                googleExtension = new GoogleSheetsExtension();
                collection.AddExtension(googleExtension);
            }

            if (!googleExtension.SheetsServiceProvider)
            {
                googleExtension.SheetsServiceProvider = coreSettings.SheetsProvider;
            }
            
            googleExtension.Columns.Clear();
            googleExtension.Columns.AddRange(coreSettings.Mapping);
            googleExtension.SpreadsheetId = coreSettings.SheetId;
            EditorUtility.SetDirty(collection);
        }
    }
}