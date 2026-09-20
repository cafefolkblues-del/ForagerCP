using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ForagerCP.EditorTools
{
    /// 밸런스 표 ↔ CSV 변환. 열 목록을 인자로 받기 때문에 필드가 늘어도 이 파일은 안 고쳐도 된다.
    public static class BalanceCsv
    {
        public const string KeyColumn = "에셋";

        public class Change
        {
            public Object Target;
            public string Column;
            public string Before;
            public string After;
        }

        // ---------- 값 ↔ 문자열 ----------

        public static string Read(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: return property.intValue.ToString();
                case SerializedPropertyType.Float: return property.floatValue.ToString("R");
                case SerializedPropertyType.Boolean: return property.boolValue ? "TRUE" : "FALSE";
                case SerializedPropertyType.String: return property.stringValue ?? "";
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex >= 0 && property.enumValueIndex < property.enumNames.Length
                        ? property.enumNames[property.enumValueIndex]
                        : property.enumValueIndex.ToString();
                case SerializedPropertyType.Color: return "#" + ColorUtility.ToHtmlStringRGBA(property.colorValue);
                case SerializedPropertyType.Vector2Int: return $"{property.vector2IntValue.x}|{property.vector2IntValue.y}";
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue != null
                        ? AssetDatabase.GetAssetPath(property.objectReferenceValue)
                        : "";
                default: return property.displayName;
            }
        }

        /// 반환값: 실제로 값이 바뀌었는지. 형식이 안 맞으면 건드리지 않고 false.
        public static bool Write(SerializedProperty property, string raw)
        {
            string current = Read(property);
            if (current == raw) return false;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (!int.TryParse(raw, out int i)) return false;
                    property.intValue = i;
                    return true;

                case SerializedPropertyType.Float:
                    if (!float.TryParse(raw, out float f)) return false;
                    property.floatValue = f;
                    return true;

                case SerializedPropertyType.Boolean:
                    property.boolValue = raw.Trim().ToUpperInvariant() is "TRUE" or "1" or "Y";
                    return true;

                case SerializedPropertyType.String:
                    property.stringValue = raw;
                    return true;

                case SerializedPropertyType.Enum:
                    for (int index = 0; index < property.enumNames.Length; index++)
                    {
                        if (property.enumNames[index] != raw) continue;
                        property.enumValueIndex = index;
                        return true;
                    }
                    return false;

                case SerializedPropertyType.Color:
                    if (!ColorUtility.TryParseHtmlString(raw, out Color color)) return false;
                    property.colorValue = color;
                    return true;

                case SerializedPropertyType.Vector2Int:
                    string[] parts = raw.Split('|');
                    if (parts.Length != 2 || !int.TryParse(parts[0], out int x) || !int.TryParse(parts[1], out int y)) return false;
                    property.vector2IntValue = new Vector2Int(x, y);
                    return true;

                case SerializedPropertyType.ObjectReference:
                    if (string.IsNullOrEmpty(raw)) { property.objectReferenceValue = null; return true; }
                    Object loaded = AssetDatabase.LoadAssetAtPath<Object>(raw);
                    if (loaded == null) return false;
                    property.objectReferenceValue = loaded;
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsNumeric(SerializedProperty property) =>
            property.propertyType == SerializedPropertyType.Integer ||
            property.propertyType == SerializedPropertyType.Float;

        // ---------- 파일 ----------

        public static string Build(IReadOnlyList<Object> assets, IReadOnlyList<string> columns,
            System.Func<Object, string, string> valueGetter)
        {
            var builder = new StringBuilder();
            builder.Append(Escape(KeyColumn));
            foreach (string column in columns) builder.Append(',').Append(Escape(column));
            builder.AppendLine();

            foreach (Object asset in assets)
            {
                builder.Append(Escape(asset.name));
                foreach (string column in columns) builder.Append(',').Append(Escape(valueGetter(asset, column)));
                builder.AppendLine();
            }

            return builder.ToString();
        }

        /// 첫 줄은 헤더, 첫 열은 에셋 이름. 결과: 에셋 이름 → (열 이름 → 값)
        public static Dictionary<string, Dictionary<string, string>> Parse(string text, out List<string> columns)
        {
            columns = new List<string>();
            var rows = new Dictionary<string, Dictionary<string, string>>();

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length == 0) return rows;

            List<string> header = SplitLine(lines[0]);
            for (int i = 1; i < header.Count; i++) columns.Add(header[i]);

            for (int line = 1; line < lines.Length; line++)
            {
                if (string.IsNullOrWhiteSpace(lines[line])) continue;

                List<string> cells = SplitLine(lines[line]);
                if (cells.Count == 0) continue;

                var row = new Dictionary<string, string>();
                for (int i = 1; i < cells.Count && i < header.Count; i++) row[header[i]] = cells[i];
                rows[cells[0]] = row;
            }

            return rows;
        }

        static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool needsQuotes = value.Contains(",") || value.Contains("\"") || value.Contains("\n");
            if (!needsQuotes) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// 따옴표 안의 쉼표를 지키는 최소 파서. 엑셀이 내보낸 파일을 그대로 먹기 위해 필요하다.
        static List<string> SplitLine(string line)
        {
            var cells = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else current.Append(c);
                    continue;
                }

                if (c == '"') inQuotes = true;
                else if (c == ',') { cells.Add(current.ToString()); current.Clear(); }
                else current.Append(c);
            }

            cells.Add(current.ToString());
            return cells;
        }
    }
}
