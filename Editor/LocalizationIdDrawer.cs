using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

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


        /// <summary>
        /// Keys of the string table collection, or of the asset table collection if there is no string one with this name.
        /// </summary>
        private static List<string> GetValues(string tableId)
        {
            LocalizationTableCollection tableCollection =
                FindCollection<StringTableCollection>(tableId) ?? (LocalizationTableCollection) FindCollection<AssetTableCollection>(tableId);
            if (!tableCollection)
            {
                Debug.LogError($"Table collection {tableId} not found");
                return new List<string>();
            }

            if (!tableCollection.SharedData)
            {
                Debug.LogError($"Shared table data of the collection {tableId} is missing");
                return new List<string>();
            }

            return tableCollection.SharedData.Entries.Select(e => e.Key).ToList();
        }

        private static TCollection FindCollection<TCollection>(string tableId) where TCollection : LocalizationTableCollection
        {
            TCollection[] collections = EditorExtensions.GetAllInstances<TCollection>().Where(c => c).ToArray();
            // The exact name is preferred, so "Voice" does not pick "VoiceOver".
            TCollection collection =
                collections.FirstOrDefault(c => string.Equals(c.TableCollectionName, tableId, StringComparison.OrdinalIgnoreCase));
            return collection
                ? collection
                : collections.FirstOrDefault(c => c.name.Contains(tableId, StringComparison.OrdinalIgnoreCase));
        }
    }
}