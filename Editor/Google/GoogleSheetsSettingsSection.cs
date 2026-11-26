using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace LocalizationSystem.Editor.Google
{
    internal sealed class GoogleSheetsSettingsSection : LocalizationSettingsSection
    {
        public override string Title => "Google Sheets";

        public override VisualElement CreateGUI(SerializedObject settings)
        {
            VisualElement root = new VisualElement();
            root.Add(new ObjectField("Sheets Provider") {objectType = typeof(SheetsServiceProvider), bindingPath = "_sheetsProvider"});
            root.Add(new TextField("Spreadsheet Id") {bindingPath = "_sheetId"});

            ReorderableList mappingList = CreateMappingList(settings);
            root.Add(new IMGUIContainer(() =>
            {
                settings.Update();
                mappingList.DoLayoutList();
                settings.ApplyModifiedProperties();
            }));

            ObjectField sourceField = new ObjectField("Copy from table") {objectType = typeof(StringTableCollection)};
            root.Add(sourceField);
            root.Add(new Button(() => CopyMapping(settings, sourceField.value as StringTableCollection)) {text = "Copy mapping"});
            return root;
        }

        private static ReorderableList CreateMappingList(SerializedObject settings)
        {
            SerializedProperty mapping = settings.FindProperty("_mapping");
            return new ReorderableList(settings, mapping, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Mapped Columns"),
                elementHeightCallback = i => EditorGUI.GetPropertyHeight(mapping.GetArrayElementAtIndex(i), true),
                drawElementCallback = (rect, i, _, _) =>
                {
                    SerializedProperty element = mapping.GetArrayElementAtIndex(i);
                    rect.xMin += 10;
                    EditorGUI.PropertyField(rect, element, new GUIContent(GetColumnLabel(element)), true);
                },
                onAddDropdownCallback = (_, _) =>
                {
                    GenericMenu menu = new GenericMenu();
                    foreach (Type type in TypeCache.GetTypesDerivedFrom<SheetColumn>().Where(t => !t.IsAbstract && !t.IsGenericType))
                    {
                        menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false, () =>
                        {
                            settings.Update();
                            int index = mapping.arraySize++;
                            mapping.GetArrayElementAtIndex(index).managedReferenceValue = Activator.CreateInstance(type);
                            settings.ApplyModifiedProperties();
                        });
                    }

                    menu.ShowAsContext();
                }
            };
        }

        private static string GetColumnLabel(SerializedProperty element)
        {
            string typeName = element.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(typeName))
                return "None";
            typeName = typeName.Substring(typeName.LastIndexOf('.') + 1);
            string column = element.FindPropertyRelative("m_Column")?.stringValue;
            return string.IsNullOrEmpty(column) ? typeName : $"{column}: {typeName}";
        }

        private static void CopyMapping(SerializedObject settings, StringTableCollection collection)
        {
            if (!collection)
            {
                Debug.LogWarning("Collection is not selected");
                return;
            }

            GoogleSheetsExtension googleExtension = collection.Extensions.OfType<GoogleSheetsExtension>().FirstOrDefault();
            if (googleExtension == null)
            {
                Debug.LogWarning($"Collection does not have a {nameof(GoogleSheetsExtension)}", collection);
                return;
            }

            LocalizationSettings target = (LocalizationSettings) settings.targetObject;
            Undo.RecordObject(target, "Copy Google Sheets mapping");
            target.Mapping = GoogleSheetsUtilities.CloneColumns(googleExtension.Columns).Cast<object>().ToArray();
            target.SheetId = googleExtension.SpreadsheetId;
            if (googleExtension.SheetsServiceProvider)
                target.SheetsProvider = googleExtension.SheetsServiceProvider;
            EditorUtility.SetDirty(target);
            settings.Update();
        }
    }
}