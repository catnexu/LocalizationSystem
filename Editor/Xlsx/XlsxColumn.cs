using System;
using UnityEngine;
using UnityEngine.Localization;

namespace LocalizationSystem.Editor.Xlsx
{
    public enum XlsxColumnType
    {
        /// <summary>Entry key name. Required.</summary>
        Key,

        /// <summary>Entry key id. Optional, keeps ids stable when keys are renamed. Format the column as text in Excel.</summary>
        KeyId,

        /// <summary>Comment shared between all locales (stored in the shared entry metadata).</summary>
        KeyComment,

        /// <summary>Localized value for <see cref="XlsxColumn.Locale"/>.</summary>
        Locale,

        /// <summary>Comment for the localized value of <see cref="XlsxColumn.Locale"/>.</summary>
        LocaleComment,
    }

    [Serializable]
    public sealed class XlsxColumn
    {
        [SerializeField] private string _column = "A";
        [SerializeField] private XlsxColumnType _type;
        [SerializeField] private LocaleIdentifier _locale;

        public string Column => _column;
        public int ColumnIndex => XlsxWorkbook.ColumnIndex(_column);
        public XlsxColumnType Type => _type;
        public LocaleIdentifier Locale => _locale;
        public bool IsLocaleColumn => _type is XlsxColumnType.Locale or XlsxColumnType.LocaleComment;

        public XlsxColumn()
        {
        }

        public XlsxColumn(string column, XlsxColumnType type, LocaleIdentifier locale = default)
        {
            _column = column;
            _type = type;
            _locale = locale;
        }

        public XlsxColumn Clone() => new XlsxColumn(_column, _type, _locale);

        public override string ToString() => IsLocaleColumn ? $"{_column}: {_type} ({_locale.Code})" : $"{_column}: {_type}";
    }
}