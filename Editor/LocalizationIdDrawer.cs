using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace LocalizationSystem.Editor
{
    [CustomPropertyDrawer(typeof(LocalizationId), useForChildren: true)]
    public class LocalizationIdDrawer : StringIdDrawer.Editor.StringIdDrawer
    {
        protected override List<string> GetIds(SerializedProperty property)
        {
            if (attribute is LocalizationId localizationId)
            {
                return GetValues(localizationId.Table);
            }

            return new List<string>();
        }


        private static List<string> GetValues(string tableId)
        {
            StringTableCollection tableCollection = EditorExtensions.GetAllInstances<StringTableCollection>()
                .FirstOrDefault(t => t.name.Contains(tableId, StringComparison.OrdinalIgnoreCase));
            if (!tableCollection)
            {
                Debug.LogError($"Table collection {tableId} not found");
                return new List<string>();
            }

            StringTable stringTable = tableCollection.StringTables.FirstOrDefault();
            if (!stringTable)
            {
                Debug.LogError($"String table collection for table {tableId} is empty");
                return new List<string>();
            }

            return stringTable.Select(kvp => kvp.Value.Key).ToList();
        }
    }
}