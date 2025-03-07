using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LocalizationSystem.Editor
{
    [FilePath("Assets/Data/Development/" + nameof(LocalizationSettings) + ".asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class LocalizationSettings : ScriptableSingleton<LocalizationSettings>
    {
        [SerializeField] private SheetsServiceProvider _sheetsProvider;
        [SerializeReference] private SheetColumn[] _mapping;

        [SerializeField] private string _sheetId;

        public SheetsServiceProvider SheetsProvider => _sheetsProvider;
        public SheetColumn[] Mapping { get => _mapping; set => _mapping = value; }
        public string SheetId { get => _sheetId; set => _sheetId = value; }

        public void SaveExternal()
        {
            Save(true);
        }
    }

    [CustomEditor(typeof(LocalizationSettings), false)]
    internal sealed class LocalizationSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            LocalizationSettings data = target as LocalizationSettings;
            if (!data)
                return;
            base.OnInspectorGUI();
            if (GUILayout.Button("Save"))
            {
                data.SaveExternal();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
        }
    }

    internal sealed class LocalizationSettingsWindow : EditorWindow
    {
        private SerializedObject _data;
        private LocalizationSettings _currentInstance;


        [MenuItem("Tools/Dev/Localization settings", priority = 1)]
        private static void InitWindow()
        {
            LocalizationSettingsWindow window = (LocalizationSettingsWindow) EditorWindow.GetWindow(typeof(LocalizationSettingsWindow));
            window.titleContent = new GUIContent(PlayerSettings.productName + " localization settings");
            window.minSize = new Vector2(550, 300);
            window.Show();
        }

        private void CreateGUI()
        {
            _currentInstance = LocalizationSettings.instance;
            ScrollView scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.Add(new InspectorElement(_currentInstance));
            scroll.Add(new VisualElement() {style = {height = new StyleLength(20)}});
            var box = new Box();
            box.Add(new Label("Copy utility")
            {
                style =
                {
                    unityTextAlign = new StyleEnum<TextAnchor>(TextAnchor.MiddleCenter),
                    unityFontStyleAndWeight = new StyleEnum<FontStyle>(FontStyle.Bold),
                    fontSize = 20,
                }
            });
            var sourceField = new ObjectField {objectType = typeof(StringTableCollection), label = "Source table:"};
            var button = new Button(() => CopyMapping(sourceField)) {text = "Copy mapping"};
            box.Add(sourceField);
            box.Add(button);
            scroll.Add(box);
            rootVisualElement.Add(scroll);
        }

        private void CopyMapping(ObjectField field)
        {
            if (field.value is StringTableCollection collection)
            {
                CollectionExtension extension = collection.Extensions.FirstOrDefault(e => e is GoogleSheetsExtension);
                if (extension is GoogleSheetsExtension googleExtension)
                {
                    List<SheetColumn> googleExtensionColumns = googleExtension.Columns;
                    _currentInstance.Mapping = googleExtensionColumns.ToArray();
                    _currentInstance.SheetId = googleExtension.SpreadsheetId;
                    _currentInstance.SaveExternal();
                }
                else
                {
                    Debug.LogWarning($"Collection does not have a {nameof(GoogleSheetsExtension)}", collection);
                }
            }
            else
            {
                Debug.LogWarning("Collection is not selected");
            }
        }

        private void OnDestroy()
        {
            DestroyImmediate(LocalizationSettings.instance);
        }
    }
}