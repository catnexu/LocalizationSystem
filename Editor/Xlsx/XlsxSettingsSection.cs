using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LocalizationSystem.Editor.Xlsx
{
    internal sealed class XlsxSettingsSection : LocalizationSettingsSection
    {
        public override string Title => "XLSX";
        public override int Order => 10;

        public override VisualElement CreateGUI(SerializedObject settings)
        {
            SerializedProperty file = settings.FindProperty("_xlsxFile");
            SerializedProperty columns = settings.FindProperty("_xlsxColumns");

            VisualElement root = new VisualElement();
            HelpBox wrongFile = new HelpBox("The file must have the .xlsx extension", HelpBoxMessageType.Warning);
            Button extractButton = new Button(() => ExtractColumns(settings, file, columns)) {text = "Extract columns from first sheet header"};

            void Refresh(SerializedProperty property)
            {
                bool isXlsx = XlsxSheetCache.IsXlsx(GetPath(property));
                wrongFile.style.display = property.objectReferenceValue && !isXlsx ? DisplayStyle.Flex : DisplayStyle.None;
                extractButton.SetEnabled(isXlsx);
            }

            PropertyField fileField = new PropertyField(file, "Xlsx File");
            fileField.TrackPropertyValue(file, Refresh);
            root.Add(fileField);
            root.Add(wrongFile);
            root.Add(new PropertyField(settings.FindProperty("_xlsxHeaderRows"), "Header Rows"));
            root.Add(new PropertyField(columns, "Columns"));
            root.Add(extractButton);
            Refresh(file);
            return root;
        }

        private static void ExtractColumns(SerializedObject settings, SerializedProperty file, SerializedProperty columns)
        {
            try
            {
                settings.Update();
                XlsxColumnMapping.Write(columns, XlsxColumnMapping.FromSheetHeader(GetPath(file), null));
                settings.ApplyModifiedProperties();
            }
            catch (Exception e)
            {
                Debug.LogException(e, settings.targetObject);
            }
        }

        private static string GetPath(SerializedProperty file) =>
            file.objectReferenceValue ? AssetDatabase.GetAssetPath(file.objectReferenceValue) : null;
    }
}
