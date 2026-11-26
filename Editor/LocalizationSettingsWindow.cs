using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LocalizationSystem.Editor
{
    internal sealed class LocalizationSettingsWindow : EditorWindow
    {
        [MenuItem(LocalizationPath.Menu + "Localization Settings (Editor)", priority = 1)]
        private static void InitWindow()
        {
            LocalizationSettingsWindow window = (LocalizationSettingsWindow) EditorWindow.GetWindow(typeof(LocalizationSettingsWindow));
            window.titleContent = new GUIContent(PlayerSettings.productName + " Localization Settings");
            window.minSize = new Vector2(550, 300);
            window.Show();
        }

        private void CreateGUI()
        {
            LocalizationSettings settings = LocalizationSettings.Instance;
            if (!settings)
            {
                rootVisualElement.Add(new HelpBox("Localization Settings asset is not created", HelpBoxMessageType.Warning));
                return;
            }

            ScrollView scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.Add(new InspectorElement(settings));
            rootVisualElement.Add(scroll);
        }
    }
}
