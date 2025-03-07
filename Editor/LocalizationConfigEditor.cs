using System.Collections.Generic;
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


        [MenuItem("Tools/Localization/Config (Runtime)")]
        private static void OpenOrCreateConfig()
        {
            if (!EditorBuildSettings.TryGetConfigObject(LocalizationConfig.ConfigName, out LocalizationConfig instance))
                instance = CreateConfig();
            if (instance)
            {
                Selection.SetActiveObjectWithContext(instance, instance);
                EditorGUIUtility.PingObject(instance);
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


        private static LocalizationConfig CreateConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Localization Config", "LocalizationConfig.asset", "asset", "");
            if (string.IsNullOrEmpty(path))
                return null;

            LocalizationConfig instance = CreateInstance<LocalizationConfig>();
            AssetDatabase.CreateAsset(instance, path);
            if (CreateAssetGroup(instance))
            {
                if (EditorUtility.IsPersistent(instance))
                {
                    EditorBuildSettings.AddConfigObject(LocalizationConfig.ConfigName, instance, true);
                    Debug.Log("Localization Settings config changed to " + AssetDatabase.GetAssetPath(instance));
                }

                EditorUtility.SetDirty(instance);
            }
            else
            {
                AssetDatabase.DeleteAsset(path);
            }

            return instance;
        }


        private static bool CreateAssetGroup(LocalizationConfig config)
        {
            AddressableAssetSettings addressablesSettings = AddressableAssetSettingsDefaultObject.Settings;
            if (!addressablesSettings)
            {
                Debug.LogError($"Addressables is not specified in the project. Create {nameof(AddressableAssetSettings)}");
                return false;
            }

            AddressableAssetGroup group = addressablesSettings.FindGroup("Localization-Config");
            if (!group)
            {
                group = addressablesSettings.CreateGroup("Localization-Config", false, true, false, null, typeof(BundledAssetGroupSchema),
                    typeof(ContentUpdateGroupSchema));
                BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
                schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
            }
            else
            {
                List<AddressableAssetEntry> entries = new List<AddressableAssetEntry>(group.entries);
                for (int i = 0; i < entries.Count; i++)
                {
                    group.RemoveAssetEntry(entries[i]);
                }
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(config, out string guid, out long _))
            {
                return false;
            }

            AddressableAssetEntry entry = addressablesSettings.CreateOrMoveEntry(guid, group);
            entry.SetAddress(LocalizationConfig.ConfigName);
            entry.ReadOnly = true;

            EditorUtility.SetDirty(group);
            return true;
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