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
    internal sealed class LocalizationSettingsWindow : EditorWindow
    {
        private SerializedObject _data;
        private LocalizationSettings _currentInstance;


        [MenuItem("Tools/Localization/Localization Settings (Editor)", priority = 1)]
        private static void InitWindow()
        {
            LocalizationSettingsWindow window = (LocalizationSettingsWindow) EditorWindow.GetWindow(typeof(LocalizationSettingsWindow));
            window.titleContent = new GUIContent(PlayerSettings.productName + " Localization Settings");
            window.minSize = new Vector2(550, 300);
            window.Show();
        }

        private void CreateGUI()
        {
            _currentInstance = LocalizationSettings.Instance;
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
                    EditorUtility.SetDirty(_currentInstance);
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
    }
}