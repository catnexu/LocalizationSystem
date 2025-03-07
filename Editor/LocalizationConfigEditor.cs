using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;

namespace LocalizationSystem.Editor
{
    [CustomEditor(typeof(LocalizationConfig))]
    internal sealed class LocalizationConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (GUILayout.Button("Update project data"))
            {
                InitializeLocalization(serializedObject);
            }
        }

        public void Validate()
        {
            var configs = EditorExtensions.GetAllInstances<LocalizationConfig>();
            foreach (LocalizationConfig localizationConfig in configs)
            {
                InitializeLocalization(new SerializedObject(localizationConfig));
            }
        }


        private void InitializeLocalization(SerializedObject serializedObj)
        {
            var tables = EditorExtensions.GetAllInstances<StringTableCollection>();
            var locales = EditorExtensions.GetAllInstances<Locale>();
            LocaleIdentifier[] projectLocales = locales.Select(l => l.Identifier).ToArray();
            string[] projectTables = tables.Select(c => c.name).ToArray();

            SerializedProperty localeProp = serializedObj.FindProperty("_locales");
            SerializedProperty tableProp = serializedObj.FindProperty("_tables");

            SetArrayValue(localeProp, projectLocales);
            SetArrayValue(tableProp, projectTables);
            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("Tools/Localization/Create config")]
        private static void CreateConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Localization Config", "LocalizationConfig.asset", "asset", "");
            if (string.IsNullOrEmpty(path))
                return;

            LocalizationConfig instance = CreateInstance<LocalizationConfig>();
            AssetDatabase.CreateAsset(instance, path);
            CreateAssetGroup(instance);
            EditorUtility.SetDirty(instance);
        }

        private static void CreateAssetGroup(LocalizationConfig config)
        {
            AddressableAssetGroup group = AddressableAssetSettingsDefaultObject.Settings.FindGroup("Localization-Config");
            if (!group)
            {
                group = AddressableAssetSettingsDefaultObject.Settings.CreateGroup("Localization-Config", false, true, false, null,
                    typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
                schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
            }

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(config, out var guid, out long _))
            {
                AddressableAssetEntry entry = group.Settings.CreateOrMoveEntry(guid, group);
                entry.SetAddress(LocalizationConfig.ConfigName);
                entry.ReadOnly = true;
            }

            EditorUtility.SetDirty(group);
        }


        private static void SetArrayValue<T>(SerializedProperty property, T[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                element.boxedValue = values[i];
            }
        }
    }
}