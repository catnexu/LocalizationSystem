using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace LocalizationSystem.Editor.Xlsx
{
    /// <summary>
    /// Minimal read-only .xlsx (Office Open XML) reader. Reads cell values only, without styles and formulas evaluation.
    /// </summary>
    public sealed class XlsxWorkbook : IDisposable
    {
        private const string DefaultWorkbookPath = "xl/workbook.xml";

        private static readonly XmlReaderSettings s_xmlSettings = new XmlReaderSettings
        {
            CloseInput = true,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        private static readonly Regex s_escapeRegex = new Regex("_x([0-9A-Fa-f]{4})_", RegexOptions.Compiled);

        private readonly ZipArchive _archive;
        private readonly List<string> _sheetNames = new List<string>();
        private readonly Dictionary<string, string> _sheetPaths = new Dictionary<string, string>();
        private readonly List<string> _sharedStrings = new List<string>();

        public IReadOnlyList<string> SheetNames => _sheetNames;

        private XlsxWorkbook(ZipArchive archive)
        {
            _archive = archive;
            string workbookPath = FindWorkbookPath();
            string workbookDirectory = GetDirectory(workbookPath);
            var relations = ReadRelations(workbookDirectory + "_rels/" + Path.GetFileName(workbookPath) + ".rels");

            ReadSheets(workbookPath, workbookDirectory, relations);

            string sharedStringsPath = relations.Values
                .Where(r => r.Type.EndsWith("/sharedStrings", StringComparison.Ordinal))
                .Select(r => ResolvePath(workbookDirectory, r.Target))
                .FirstOrDefault() ?? workbookDirectory + "sharedStrings.xml";
            ReadSharedStrings(sharedStringsPath);
        }

        public static XlsxWorkbook Open(string path)
        {
            // FileShare.ReadWrite allows reading a file that is currently opened in Excel.
            FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            try
            {
                return new XlsxWorkbook(new ZipArchive(stream, ZipArchiveMode.Read, false));
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        public void Dispose() => _archive.Dispose();

        public bool HasSheet(string name) => name != null && _sheetPaths.ContainsKey(name);

        /// <summary>
        /// Returns the sheet rows. Index in the list is the zero based row index, index in the array is the zero based column index.
        /// Missing cells are null.
        /// </summary>
        public List<string[]> ReadSheet(string name, int maxRows = int.MaxValue)
        {
            if (name == null || !_sheetPaths.TryGetValue(name, out string path))
                throw new ArgumentException($"Sheet '{name}' is not found. Available sheets: {string.Join(", ", _sheetNames)}");

            var rows = new List<string[]>();
            using XmlReader reader = OpenXml(path) ?? throw new InvalidDataException($"Sheet '{name}' data ({path}) is missing");
            var cells = new List<string>();
            int rowIndex = -1;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "row")
                    continue;

                rowIndex = ParseRowIndex(reader.GetAttribute("r"), rowIndex + 1);
                if (rowIndex >= maxRows)
                    break;

                ReadRow(reader, cells);
                while (rows.Count < rowIndex)
                    rows.Add(Array.Empty<string>());

                if (rowIndex < rows.Count)
                    rows[rowIndex] = cells.ToArray();
                else
                    rows.Add(cells.ToArray());
            }

            return rows;
        }

        /// <summary>
        /// Converts a column name ("A", "AB") or a cell reference ("AB12") into a zero based column index. Returns -1 if invalid.
        /// </summary>
        public static int ColumnIndex(string column)
        {
            if (string.IsNullOrEmpty(column))
                return -1;

            int index = 0;
            int i = 0;
            for (; i < column.Length; i++)
            {
                char c = char.ToUpperInvariant(column[i]);
                if (c < 'A' || c > 'Z')
                    break;
                index = index * 26 + (c - 'A' + 1);
            }

            return i == 0 ? -1 : index - 1;
        }

        public static string ColumnName(int index)
        {
            var builder = new StringBuilder();
            for (index++; index > 0; index = (index - 1) / 26)
                builder.Insert(0, (char) ('A' + (index - 1) % 26));
            return builder.ToString();
        }

        private void ReadRow(XmlReader reader, List<string> cells)
        {
            cells.Clear();
            if (reader.IsEmptyElement)
                return;

            int depth = reader.Depth;
            int column = -1;
            reader.Read();
            while (!reader.EOF && reader.Depth > depth)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "c")
                {
                    int parsed = ColumnIndex(reader.GetAttribute("r"));
                    column = parsed >= 0 ? parsed : column + 1;
                    string value = ReadCell(reader, reader.GetAttribute("t"));
                    while (cells.Count < column)
                        cells.Add(null);
                    if (column < cells.Count)
                        cells[column] = value;
                    else
                        cells.Add(value);
                    continue;
                }

                reader.Read();
            }
        }

        /// <summary>
        /// Reads the &lt;c&gt; element and moves the reader to the next node after it.
        /// </summary>
        private string ReadCell(XmlReader reader, string type)
        {
            if (reader.IsEmptyElement)
            {
                reader.Read();
                return null;
            }

            int depth = reader.Depth;
            string raw = null;
            string inline = null;
            reader.Read();
            while (!reader.EOF && reader.Depth > depth)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.LocalName)
                    {
                        case "v":
                            raw = reader.ReadElementContentAsString();
                            continue;
                        case "f":
                            reader.Skip();
                            continue;
                        case "is":
                            inline = ReadStringItem(reader);
                            break;
                    }
                }

                reader.Read();
            }

            reader.Read();

            switch (type)
            {
                case "s":
                    return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 0 &&
                        index < _sharedStrings.Count
                            ? _sharedStrings[index]
                            : null;
                case "inlineStr":
                    return inline;
                case "b":
                    return raw == "1" ? "TRUE" : raw == "0" ? "FALSE" : raw;
                default:
                    return raw != null ? Unescape(raw) : inline;
            }
        }

        /// <summary>
        /// Reads a string item (&lt;si&gt; or &lt;is&gt;): plain text or rich text runs. Phonetic runs are ignored.
        /// Leaves the reader on the end element.
        /// </summary>
        private static string ReadStringItem(XmlReader reader)
        {
            if (reader.IsEmptyElement)
                return string.Empty;

            int depth = reader.Depth;
            var builder = new StringBuilder();
            reader.Read();
            while (!reader.EOF && reader.Depth > depth)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.LocalName == "t")
                    {
                        builder.Append(reader.ReadElementContentAsString());
                        continue;
                    }

                    if (reader.LocalName == "rPh")
                    {
                        reader.Skip();
                        continue;
                    }
                }

                reader.Read();
            }

            return Unescape(builder.ToString());
        }

        private void ReadSharedStrings(string path)
        {
            using XmlReader reader = OpenXml(path);
            if (reader == null)
                return;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si")
                    _sharedStrings.Add(ReadStringItem(reader));
            }
        }

        private void ReadSheets(string workbookPath, string workbookDirectory, Dictionary<string, (string Type, string Target)> relations)
        {
            using XmlReader reader = OpenXml(workbookPath) ?? throw new InvalidDataException("The file is not a valid xlsx workbook");
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "sheet")
                    continue;

                string name = reader.GetAttribute("name");
                string relationId = GetRelationshipId(reader);
                if (name == null || relationId == null || !relations.TryGetValue(relationId, out var relation))
                    continue;

                _sheetNames.Add(name);
                _sheetPaths[name] = ResolvePath(workbookDirectory, relation.Target);
            }
        }

        private string FindWorkbookPath()
        {
            return ReadRelations("_rels/.rels").Values
                .Where(r => r.Type.EndsWith("/officeDocument", StringComparison.Ordinal))
                .Select(r => ResolvePath(string.Empty, r.Target))
                .FirstOrDefault() ?? DefaultWorkbookPath;
        }

        private Dictionary<string, (string Type, string Target)> ReadRelations(string path)
        {
            var relations = new Dictionary<string, (string Type, string Target)>();
            using XmlReader reader = OpenXml(path);
            if (reader == null)
                return relations;

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Relationship")
                    continue;

                string id = reader.GetAttribute("Id");
                string target = reader.GetAttribute("Target");
                if (id != null && target != null)
                    relations[id] = (reader.GetAttribute("Type") ?? string.Empty, target);
            }

            return relations;
        }

        private static string GetRelationshipId(XmlReader reader)
        {
            string id = null;
            for (bool hasAttribute = reader.MoveToFirstAttribute(); hasAttribute; hasAttribute = reader.MoveToNextAttribute())
            {
                // r:id, the relationships namespace differs between transitional and strict formats.
                if (reader.LocalName == "id" && reader.NamespaceURI.Length > 0)
                {
                    id = reader.Value;
                    break;
                }
            }

            reader.MoveToElement();
            return id;
        }

        private XmlReader OpenXml(string path)
        {
            ZipArchiveEntry entry = _archive.GetEntry(path)
                ?? _archive.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase));
            return entry != null ? XmlReader.Create(entry.Open(), s_xmlSettings) : null;
        }

        private static int ParseRowIndex(string reference, int fallback) =>
            int.TryParse(reference, NumberStyles.Integer, CultureInfo.InvariantCulture, out int row) && row > 0 ? row - 1 : fallback;

        private static string GetDirectory(string path)
        {
            int index = path.LastIndexOf('/');
            return index >= 0 ? path.Substring(0, index + 1) : string.Empty;
        }

        private static string ResolvePath(string directory, string target)
        {
            target = target.Replace('\\', '/');
            string combined = target.StartsWith("/", StringComparison.Ordinal) ? target.Substring(1) : directory + target;
            var segments = new List<string>();
            foreach (string segment in combined.Split('/'))
            {
                if (segment == "..")
                {
                    if (segments.Count > 0)
                        segments.RemoveAt(segments.Count - 1);
                }
                else if (segment.Length > 0 && segment != ".")
                {
                    segments.Add(segment);
                }
            }

            return string.Join("/", segments);
        }

        /// <summary>
        /// Excel escapes some characters as _xHHHH_ (e.g. _x000D_ for carriage return).
        /// </summary>
        private static string Unescape(string value) =>
            value.IndexOf("_x", StringComparison.Ordinal) < 0
                ? value
                : s_escapeRegex.Replace(value,
                    m => ((char) int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());
    }
}