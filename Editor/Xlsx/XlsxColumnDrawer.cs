using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;

namespace LocalizationSystem.Editor.Xlsx
{
    [CustomPropertyDrawer(typeof(XlsxColumn))]
    internal sealed class XlsxColumnDrawer : PropertyDrawer
    {
        private const float ColumnWidth = 50;
        private const float TypeWidth = 110;
        private const float Space = 4;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty column = property.FindPropertyRelative("_column");
            SerializedProperty type = property.FindPropertyRelative("_type");
            SerializedProperty localeCode = property.FindPropertyRelative("_locale.m_Code");

            position.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.BeginProperty(position, label, property);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            Rect columnRect = new Rect(position.x, position.y, ColumnWidth, position.height);
            Rect typeRect = new Rect(columnRect.xMax + Space, position.y, TypeWidth, position.height);
            Rect localeRect = new Rect(typeRect.xMax + Space, position.y, Mathf.Max(0, position.xMax - typeRect.xMax - Space), position.height);

            EditorGUI.BeginChangeCheck();
            string columnName = EditorGUI.TextField(columnRect, column.stringValue);
            if (EditorGUI.EndChangeCheck())
                column.stringValue = columnName.Trim().ToUpperInvariant();

            EditorGUI.PropertyField(typeRect, type, GUIContent.none);

            XlsxColumnType columnType = (XlsxColumnType) type.enumValueIndex;
            if (columnType is XlsxColumnType.Locale or XlsxColumnType.LocaleComment)
            {
                EditorGUI.BeginChangeCheck();
                Locale current = LocalizationEditorSettings.GetLocale(new LocaleIdentifier(localeCode.stringValue ?? string.Empty));
                Locale locale = EditorGUI.ObjectField(localeRect, current, typeof(Locale), false) as Locale;
                if (EditorGUI.EndChangeCheck())
                    localeCode.stringValue = locale ? locale.Identifier.Code : string.Empty;
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
