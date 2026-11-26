using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace LocalizationSystem.Editor.Xlsx
{
    /// <summary>
    /// Binds a <see cref="StringTableCollection"/> to a sheet of an .xlsx file located in the project.
    /// </summary>
    [Serializable]
    [StringTableCollectionExtension]
    public sealed class XlsxExtension : CollectionExtension
    {
        [SerializeField] private DefaultAsset _file;
        [SerializeField] private string _sheet;
        [SerializeField, Min(0)] private int _headerRows = 1;
        [SerializeField] private bool _removeMissingKeys;
        [SerializeField] private List<XlsxColumn> _columns = new List<XlsxColumn>();

        public DefaultAsset File { get => _file; set => _file = value; }
        public string FilePath => _file ? AssetDatabase.GetAssetPath(_file) : null;
        public string Sheet { get => _sheet; set => _sheet = value; }
        public int HeaderRows { get => _headerRows; set => _headerRows = Mathf.Max(0, value); }
        public bool RemoveMissingKeys { get => _removeMissingKeys; set => _removeMissingKeys = value; }
        public List<XlsxColumn> Columns => _columns;
    }
}
