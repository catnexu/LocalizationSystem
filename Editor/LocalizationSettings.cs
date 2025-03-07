using UnityEditor;
using UnityEditor.Localization.Plugins.Google;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine;

namespace LocalizationSystem.Editor
{
    internal sealed class LocalizationSettings : ScriptableObject
    {
        private const string ConfigName = "com.catnexu.localizationsystem.settings";
        
        [SerializeField] private SheetsServiceProvider _sheetsProvider;
        [SerializeReference] private SheetColumn[] _mapping;

        [SerializeField] private string _sheetId;

        public SheetsServiceProvider SheetsProvider => _sheetsProvider;
        public SheetColumn[] Mapping { get => _mapping; set => _mapping = value; }
        public string SheetId { get => _sheetId; set => _sheetId = value; }
        
        public static LocalizationSettings Instance
        {
            get
            {
                if (s_instance == null && !EditorBuildSettings.TryGetConfigObject(ConfigName, out s_instance))
                    s_instance = CreateConfig();
                return s_instance;
            }
            set
            {
                if (s_instance == value)
                    return;

                if (EditorUtility.IsPersistent(value))
                {
                    EditorBuildSettings.AddConfigObject(ConfigName, value, true);
                    Debug.Log("Localization Settings changed to " + AssetDatabase.GetAssetPath(value));
                }

                s_instance = value;
            }
        }

        private static LocalizationSettings s_instance;
        

        private static LocalizationSettings CreateConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Localization Settings", "LocalizationSettings.asset", "asset", "");
            if (string.IsNullOrEmpty(path))
                return null;

            LocalizationSettings instance = CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(instance, path);
            EditorUtility.SetDirty(instance);
            Instance = instance;
            return instance;
        }
    }
}