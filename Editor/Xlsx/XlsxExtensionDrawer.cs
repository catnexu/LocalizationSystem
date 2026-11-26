using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LocalizationSystem.Editor.Xlsx
{
    [CustomPropertyDrawer(typeof(XlsxExtension))]
    internal sealed class XlsxExtensionDrawer : PropertyDrawer
    {
        private static float LineHeight => EditorGUIUtility.singleLineHeight;
        private static float Spacing => EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float columns = EditorGUI.GetPropertyHeight(property.FindPropertyRelative("_columns"), true);
            return (LineHeight + Spacing) * 6 + columns;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty file = property.FindPropertyRelative("_file");
            SerializedProperty sheet = property.FindPropertyRelative("_sheet");
            SerializedProperty columns = property.FindPropertyRelative("_columns");
            string path = file.objectReferenceValue ? AssetDatabase.GetAssetPath(file.objectReferenceValue) : null;
            bool isValidFile = XlsxSheetCache.IsXlsx(path);

            Rect rect = new Rect(position.x, position.y, position.width, LineHeight);
            EditorGUI.LabelField(rect, "XLSX", EditorStyles.boldLabel);
            NextLine(ref rect);

            EditorGUI.PropertyField(rect, file);
            NextLine(ref rect);

            DrawSheet(rect, sheet, isValidFile ? path : null);
            NextLine(ref rect);

            EditorGUI.PropertyField(rect, property.FindPropertyRelative("_headerRows"));
            NextLine(ref rect);

            EditorGUI.PropertyField(rect, property.FindPropertyRelative("_removeMissingKeys"));
            NextLine(ref rect);

            rect.height = EditorGUI.GetPropertyHeight(columns, true);
            EditorGUI.PropertyField(rect, columns, true);
            rect.y += rect.height + Spacing;
            rect.height = LineHeight;

            using (new EditorGUI.DisabledScope(!isValidFile))
            {
                Rect half = new Rect(rect.x, rect.y, rect.width / 2 - 2, rect.height);
                if (GUI.Button(half, "Extract Columns From Header"))
                {
                    try
                    {
                        XlsxColumnMapping.Write(columns, XlsxColumnMapping.FromSheetHeader(path, sheet.stringValue));
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                half.x = rect.xMax - half.width;
                if (GUI.Button(half, "Pull"))
                {
                    property.serializedObject.ApplyModifiedProperties();
                    XlsxExtension extension = property.managedReferenceValue as XlsxExtension;
                    EditorApplication.delayCall += () => PullSafe(extension);
                }
            }
        }

        private static void DrawSheet(Rect rect, SerializedProperty sheet, string path)
        {
            string[] names = XlsxSheetCache.GetSheetNames(path);
            if (names.Length == 0)
            {
                EditorGUI.PropertyField(rect, sheet);
                return;
            }

            int index = Array.IndexOf(names, sheet.stringValue);
            string[] options = index >= 0 || string.IsNullOrEmpty(sheet.stringValue)
                ? names
                : names.Prepend($"{sheet.stringValue} (missing)").ToArray();
            int offset = options.Length - names.Length;

            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(rect, sheet.displayName, index >= 0 ? index + offset : offset > 0 ? 0 : -1, options);
            if (EditorGUI.EndChangeCheck() && selected >= offset)
                sheet.stringValue = names[selected - offset];
        }

        private static void PullSafe(XlsxExtension extension)
        {
            try
            {
                XlsxImporter.Pull(extension, true);
            }
            catch (Exception e)
            {
                Debug.LogException(e, extension?.TargetCollection);
            }
        }

        private static void NextLine(ref Rect rect) => rect.y += LineHeight + Spacing;
    }
}
