using System.Collections.Generic;
using LocalizationSystem.Editor.Xlsx;
using UnityEditor;
using UnityEngine;

namespace LocalizationSystem.Editor
{
    internal sealed class LocalizationSettings : ScriptableObject
    {
        private const string ConfigName = "com.catnexu.localizationsystem.settings";

        // Google Sheets data is stored with plugin-agnostic types so this assembly does not depend on the Google module.
        // _sheetsProvider is a SheetsServiceProvider, _mapping contains SheetColumn instances.
        [SerializeField] private ScriptableObject _sheetsProvider;
        [SerializeReference] private object[] _mapping;
        [SerializeField] private string _sheetId;

        [SerializeField] private DefaultAsset _xlsxFile;
        [SerializeField, Min(0)] private int _xlsxHeaderRows = 1;
        [SerializeField] private List<XlsxColumn> _xlsxColumns = new List<XlsxColumn>();

        public ScriptableObject SheetsProvider { get => _sheetsProvider; set => _sheetsProvider = value; }
        public object[] Mapping { get => _mapping; set => _mapping = value; }
        public string SheetId { get => _sheetId; set => _sheetId = value; }

        public DefaultAsset XlsxFile => _xlsxFile;
        public int XlsxHeaderRows => _xlsxHeaderRows;
        public IReadOnlyList<XlsxColumn> XlsxColumns => _xlsxColumns;

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