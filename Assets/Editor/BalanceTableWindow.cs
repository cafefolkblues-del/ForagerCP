using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ForagerCP.EditorTools
{
    /// 밸런스 수치를 표로 모아 보고 고치는 창.
    ///
    /// 핵심: 열을 코드에 박지 않는다. 대상 타입의 SerializedProperty를 훑어서 열을 만들기 때문에
    /// 정의(SO)에 필드를 추가하면 표에 열이 저절로 생긴다 — 이 파일은 안 고쳐도 된다.
    /// 계산 열(타수·초당 골드 등)만 BalanceMetrics에 식을 적어야 한다.
    public class BalanceTableWindow : EditorWindow
    {
        enum BulkOperation { Set, Add, Multiply }

        class Column
        {
            public string Path;
            public string Label;
            public SerializedPropertyType Type;
            public float Width;
            public bool IsMetric;
            public BalanceMetrics.Metric Metric;
        }

        const float LeftWidth = 210f;
        const float RowHeight = 20f;

        [SerializeField] int _typeIndex;
        [SerializeField] string _search = "";
        [SerializeField] string _sortColumn = "";
        [SerializeField] bool _sortAscending = true;

        readonly List<Type> _types = new List<Type>();
        readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();
        readonly List<Column> _columns = new List<Column>();
        readonly Dictionary<UnityEngine.Object, SerializedObject> _serialized = new Dictionary<UnityEngine.Object, SerializedObject>();
        readonly HashSet<UnityEngine.Object> _selected = new HashSet<UnityEngine.Object>();
        readonly HashSet<string> _hidden = new HashSet<string>();

        Vector2 _bodyScroll;
        Vector2 _leftScroll;
        Vector2 _headerScroll;

        int _bulkColumn;
        BulkOperation _bulkOperation = BulkOperation.Set;
        float _bulkValue = 1f;

        List<BalanceCsv.Change> _pendingChanges;
        Vector2 _previewScroll;
        string _message = "";
        MessageType _messageType = MessageType.None;

        [MenuItem("Tools/ForagerCP/밸런스 표")]
        static void Open() => GetWindow<BalanceTableWindow>("밸런스 표");

        void OnEnable() => Reload();

        // ---------------- 수집 ----------------

        void Reload()
        {
            CollectTypes();
            CollectAssets();
            CollectColumns();
            LoadHiddenColumns();
        }

        /// 우리 네임스페이스의 ScriptableObject 파생을 전부 찾는다.
        /// 나중에 제작 레시피·장비 SO가 생겨도 목록에 저절로 올라온다.
        void CollectTypes()
        {
            _types.Clear();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (type.IsAbstract) continue;
                if (type.Namespace != "ForagerCP") continue;
                if (typeof(EditorWindow).IsAssignableFrom(type)) continue;

                _types.Add(type);
            }

            _types.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            _typeIndex = Mathf.Clamp(_typeIndex, 0, Mathf.Max(0, _types.Count - 1));
        }

        Type CurrentType => _types.Count == 0 ? null : _types[Mathf.Clamp(_typeIndex, 0, _types.Count - 1)];

        void CollectAssets()
        {
            _assets.Clear();
            _serialized.Clear();
            _selected.Clear();

            Type type = CurrentType;
            if (type == null) return;

            foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, type);
                if (asset == null) continue;

                _assets.Add(asset);
                _serialized.Add(asset, new SerializedObject(asset));
            }

            SortAssets();
        }

        void CollectColumns()
        {
            _columns.Clear();

            Type type = CurrentType;
            if (type == null || _assets.Count == 0) return;

            SerializedObject probe = _serialized[_assets[0]];
            SerializedProperty property = probe.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script") continue;

                // 배열/구조체 하위까지 펼치면 표가 감당이 안 된다. 최상위 필드만 열로 쓴다.
                if (property.depth > 0) continue;

                _columns.Add(new Column
                {
                    Path = property.propertyPath,
                    Label = property.displayName,
                    Type = property.propertyType,
                    Width = WidthFor(property.propertyType)
                });
            }

            foreach (BalanceMetrics.Metric metric in BalanceMetrics.For(type))
            {
                _columns.Add(new Column
                {
                    Path = "metric:" + metric.Name,
                    Label = metric.Name,
                    Width = 96f,
                    IsMetric = true,
                    Metric = metric
                });
            }
        }

        static float WidthFor(SerializedPropertyType type)
        {
            switch (type)
            {
                case SerializedPropertyType.Boolean: return 60f;
                case SerializedPropertyType.String: return 150f;
                case SerializedPropertyType.ObjectReference: return 170f;
                case SerializedPropertyType.Color: return 80f;
                case SerializedPropertyType.Vector2Int: return 120f;
                default: return 92f;
            }
        }

        string HiddenPrefKey => "ForagerCP.Balance." + (CurrentType != null ? CurrentType.Name : "none") + ".hidden";

        void LoadHiddenColumns()
        {
            _hidden.Clear();
            foreach (string path in EditorPrefs.GetString(HiddenPrefKey, "").Split('|'))
            {
                if (!string.IsNullOrEmpty(path)) _hidden.Add(path);
            }
        }

        void SaveHiddenColumns() => EditorPrefs.SetString(HiddenPrefKey, string.Join("|", _hidden));

        IEnumerable<Column> VisibleColumns => _columns.Where(c => !_hidden.Contains(c.Path));

        // ---------------- 그리기 ----------------

        void OnGUI()
        {
            if (_types.Count == 0)
            {
                EditorGUILayout.HelpBox("표시할 수 있는 데이터 타입을 찾지 못했습니다.", MessageType.Warning);
                if (GUILayout.Button("다시 찾기")) Reload();
                return;
            }

            DrawToolbar();

            if (_assets.Count == 0)
            {
                EditorGUILayout.HelpBox($"'{CurrentType.Name}' 에셋이 아직 없습니다. 새 콘텐츠 만들기로 추가해주세요.", MessageType.Info);
                return;
            }

            DrawBulkEditBar();
            DrawHeaderRow();
            DrawBody();
            DrawFooter();

            if (_pendingChanges != null) DrawImportPreview();
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, _messageType);
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            int newIndex = EditorGUILayout.Popup(_typeIndex, _types.Select(t => t.Name).ToArray(),
                EditorStyles.toolbarPopup, GUILayout.Width(190f));
            if (newIndex != _typeIndex)
            {
                _typeIndex = newIndex;
                _sortColumn = "";
                Reload();
            }

            _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(180f));

            if (GUILayout.Button("열 보기", EditorStyles.toolbarDropDown, GUILayout.Width(70f))) ShowColumnMenu();
            if (GUILayout.Button("새로고침", EditorStyles.toolbarButton, GUILayout.Width(70f))) Reload();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("CSV 내보내기", EditorStyles.toolbarButton, GUILayout.Width(90f))) ExportCsv();
            if (GUILayout.Button("CSV 가져오기", EditorStyles.toolbarButton, GUILayout.Width(90f))) ImportCsv();

            EditorGUILayout.EndHorizontal();
        }

        void ShowColumnMenu()
        {
            var menu = new GenericMenu();
            foreach (Column column in _columns)
            {
                Column captured = column;
                menu.AddItem(new GUIContent(column.Label + (column.IsMetric ? "  (계산)" : "")),
                    !_hidden.Contains(column.Path), () =>
                    {
                        if (!_hidden.Remove(captured.Path)) _hidden.Add(captured.Path);
                        SaveHiddenColumns();
                        Repaint();
                    });
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("전부 보이기"), false, () => { _hidden.Clear(); SaveHiddenColumns(); Repaint(); });
            menu.ShowAsContext();
        }

        void DrawBulkEditBar()
        {
            List<Column> numeric = _columns
                .Where(c => !c.IsMetric && (c.Type == SerializedPropertyType.Integer || c.Type == SerializedPropertyType.Float))
                .ToList();

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"선택 {_selected.Count}개", GUILayout.Width(70f));

            using (new EditorGUI.DisabledScope(numeric.Count == 0 || _selected.Count == 0))
            {
                _bulkColumn = EditorGUILayout.Popup(_bulkColumn,
                    numeric.Select(c => c.Label).DefaultIfEmpty("-").ToArray(), GUILayout.Width(140f));
                _bulkOperation = (BulkOperation)EditorGUILayout.Popup((int)_bulkOperation,
                    new[] { "값으로 지정", "더하기", "곱하기" }, GUILayout.Width(100f));
                _bulkValue = EditorGUILayout.FloatField(_bulkValue, GUILayout.Width(70f));

                if (GUILayout.Button("적용", GUILayout.Width(60f)) && numeric.Count > 0)
                {
                    ApplyBulk(numeric[Mathf.Clamp(_bulkColumn, 0, numeric.Count - 1)]);
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("전체 선택", GUILayout.Width(80f)))
            {
                foreach (UnityEngine.Object asset in FilteredAssets()) _selected.Add(asset);
            }
            if (GUILayout.Button("선택 해제", GUILayout.Width(80f))) _selected.Clear();

            EditorGUILayout.EndHorizontal();
        }

        void DrawHeaderRow()
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("이름", EditorStyles.miniBoldLabel, GUILayout.Width(LeftWidth));

            _headerScroll = EditorGUILayout.BeginScrollView(_headerScroll, GUIStyle.none, GUIStyle.none,
                GUILayout.Height(20f));
            EditorGUILayout.BeginHorizontal();

            foreach (Column column in VisibleColumns)
            {
                bool sorted = _sortColumn == column.Path;
                string label = column.Label + (sorted ? (_sortAscending ? " ▲" : " ▼") : "");
                var content = new GUIContent(label, column.IsMetric ? column.Metric.Tooltip : column.Path);

                if (GUILayout.Button(content, EditorStyles.miniBoldLabel, GUILayout.Width(column.Width)))
                {
                    if (column.IsMetric) continue; // 계산 열은 정렬 기준으로 안 쓴다(문자열이라 의미가 흐려짐)
                    _sortAscending = _sortColumn == column.Path ? !_sortAscending : true;
                    _sortColumn = column.Path;
                    SortAssets();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();
        }

        void DrawBody()
        {
            List<UnityEngine.Object> rows = FilteredAssets().ToList();

            EditorGUILayout.BeginHorizontal();

            // 왼쪽 고정 열: 세로 스크롤만 본체와 맞춘다.
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUIStyle.none, GUIStyle.none,
                GUILayout.Width(LeftWidth + 6f));
            foreach (UnityEngine.Object asset in rows)
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));

                bool selected = _selected.Contains(asset);
                bool next = EditorGUILayout.Toggle(selected, GUILayout.Width(18f));
                if (next != selected)
                {
                    if (next) _selected.Add(asset);
                    else _selected.Remove(asset);
                }

                if (GUILayout.Button(asset.name, EditorStyles.label, GUILayout.Width(LeftWidth - 24f)))
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            // 본체
            _bodyScroll = EditorGUILayout.BeginScrollView(_bodyScroll);
            foreach (UnityEngine.Object asset in rows)
            {
                SerializedObject serialized = _serialized[asset];
                serialized.Update();

                EditorGUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
                foreach (Column column in VisibleColumns) DrawCell(asset, serialized, column);
                EditorGUILayout.EndHorizontal();

                serialized.ApplyModifiedProperties();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndHorizontal();

            // 스크롤 동기화: 본체가 기준이다.
            _leftScroll.y = _bodyScroll.y;
            _headerScroll.x = _bodyScroll.x;
        }

        void DrawCell(UnityEngine.Object asset, SerializedObject serialized, Column column)
        {
            if (column.IsMetric)
            {
                string value;
                try { value = column.Metric.Evaluate(asset); }
                catch { value = "-"; }

                GUI.color = new Color(0.75f, 0.85f, 1f);
                GUILayout.Label(value, EditorStyles.miniLabel, GUILayout.Width(column.Width));
                GUI.color = Color.white;
                return;
            }

            SerializedProperty property = serialized.FindProperty(column.Path);
            if (property == null)
            {
                GUILayout.Label("-", GUILayout.Width(column.Width));
                return;
            }

            EditorGUILayout.PropertyField(property, GUIContent.none, GUILayout.Width(column.Width));
        }

        void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{CurrentType.Name}  ·  {_assets.Count}개  ·  열 {VisibleColumns.Count()}/{_columns.Count}",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("값을 고치면 바로 저장됩니다 (Ctrl+Z 가능)", EditorStyles.miniLabel, GUILayout.Width(250f));
            EditorGUILayout.EndHorizontal();
        }

        // ---------------- 동작 ----------------

        IEnumerable<UnityEngine.Object> FilteredAssets()
        {
            if (string.IsNullOrWhiteSpace(_search)) return _assets;
            return _assets.Where(a => a.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        void SortAssets()
        {
            if (string.IsNullOrEmpty(_sortColumn)) return;

            _assets.Sort((a, b) =>
            {
                SerializedProperty pa = _serialized[a].FindProperty(_sortColumn);
                SerializedProperty pb = _serialized[b].FindProperty(_sortColumn);
                if (pa == null || pb == null) return 0;

                int result;
                if (BalanceCsv.IsNumeric(pa))
                {
                    float va = pa.propertyType == SerializedPropertyType.Integer ? pa.intValue : pa.floatValue;
                    float vb = pb.propertyType == SerializedPropertyType.Integer ? pb.intValue : pb.floatValue;
                    result = va.CompareTo(vb);
                }
                else result = string.CompareOrdinal(BalanceCsv.Read(pa), BalanceCsv.Read(pb));

                return _sortAscending ? result : -result;
            });
        }

        void ApplyBulk(Column column)
        {
            int changed = 0;

            foreach (UnityEngine.Object asset in _selected)
            {
                if (!_serialized.TryGetValue(asset, out SerializedObject serialized)) continue;

                serialized.Update();
                SerializedProperty property = serialized.FindProperty(column.Path);
                if (property == null) continue;

                if (property.propertyType == SerializedPropertyType.Integer)
                {
                    int before = property.intValue;
                    property.intValue = Mathf.RoundToInt(Combine(before, _bulkValue));
                    if (property.intValue != before) changed++;
                }
                else if (property.propertyType == SerializedPropertyType.Float)
                {
                    float before = property.floatValue;
                    property.floatValue = Combine(before, _bulkValue);
                    if (!Mathf.Approximately(property.floatValue, before)) changed++;
                }

                // ApplyModifiedProperties가 Undo까지 등록해준다.
                serialized.ApplyModifiedProperties();
            }

            AssetDatabase.SaveAssets();
            _message = $"{changed}개 값을 바꿨습니다. ({column.Label})";
            _messageType = MessageType.Info;
        }

        float Combine(float before, float operand)
        {
            switch (_bulkOperation)
            {
                case BulkOperation.Add: return before + operand;
                case BulkOperation.Multiply: return before * operand;
                default: return operand;
            }
        }

        // ---------------- CSV ----------------

        void ExportCsv()
        {
            string path = EditorUtility.SaveFilePanel("CSV로 내보내기", "", CurrentType.Name + ".csv", "csv");
            if (string.IsNullOrEmpty(path)) return;

            List<Column> columns = VisibleColumns.ToList();
            string csv = BalanceCsv.Build(_assets, columns.Select(c => c.Label).ToList(), (asset, label) =>
            {
                Column column = columns.First(c => c.Label == label);
                if (column.IsMetric)
                {
                    try { return column.Metric.Evaluate(asset); }
                    catch { return "-"; }
                }

                SerializedProperty property = _serialized[asset].FindProperty(column.Path);
                return property != null ? BalanceCsv.Read(property) : "";
            });

            // 엑셀에서 한글이 깨지지 않도록 BOM을 붙인다.
            File.WriteAllText(path, csv, new System.Text.UTF8Encoding(true));

            _message = "내보냈습니다: " + path;
            _messageType = MessageType.Info;
        }

        void ImportCsv()
        {
            string path = EditorUtility.OpenFilePanel("CSV 가져오기", "", "csv");
            if (string.IsNullOrEmpty(path)) return;

            Dictionary<string, Dictionary<string, string>> rows =
                BalanceCsv.Parse(File.ReadAllText(path), out List<string> csvColumns);

            var changes = new List<BalanceCsv.Change>();
            int unknownRows = 0;

            foreach (KeyValuePair<string, Dictionary<string, string>> row in rows)
            {
                UnityEngine.Object asset = _assets.FirstOrDefault(a => a.name == row.Key);
                if (asset == null) { unknownRows++; continue; }

                SerializedObject serialized = _serialized[asset];
                serialized.Update();

                foreach (string csvColumn in csvColumns)
                {
                    Column column = _columns.FirstOrDefault(c => c.Label == csvColumn && !c.IsMetric);
                    if (column == null) continue; // 계산 열이나 모르는 열은 무시

                    SerializedProperty property = serialized.FindProperty(column.Path);
                    if (property == null) continue;

                    string before = BalanceCsv.Read(property);
                    string after = row.Value[csvColumn];
                    if (before == after) continue;

                    changes.Add(new BalanceCsv.Change
                    {
                        Target = asset,
                        Column = column.Path,
                        Before = before,
                        After = after
                    });
                }
            }

            _pendingChanges = changes;
            _message = unknownRows > 0
                ? $"{changes.Count}건 변경 예정. 프로젝트에 없는 행 {unknownRows}개는 건너뜁니다."
                : $"{changes.Count}건 변경 예정입니다. 확인 후 적용하세요.";
            _messageType = MessageType.Info;
        }

        /// 덮어쓰기 사고를 막으려고 무엇이 어떻게 바뀌는지 먼저 보여준다.
        void DrawImportPreview()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"가져오기 미리보기 — {_pendingChanges.Count}건", EditorStyles.boldLabel);

            _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.Height(140f));
            foreach (BalanceCsv.Change change in _pendingChanges)
            {
                EditorGUILayout.LabelField($"{change.Target.name}  ·  {change.Column}   {change.Before}  →  {change.After}",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.45f, 1f, 0.6f);
            if (GUILayout.Button("적용", GUILayout.Height(26f))) ApplyImport();
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("취소", GUILayout.Height(26f))) _pendingChanges = null;
            EditorGUILayout.EndHorizontal();
        }

        void ApplyImport()
        {
            int applied = 0;
            int rejected = 0;

            foreach (BalanceCsv.Change change in _pendingChanges)
            {
                SerializedObject serialized = _serialized[change.Target];
                serialized.Update();

                SerializedProperty property = serialized.FindProperty(change.Column);
                if (property == null) { rejected++; continue; }

                if (BalanceCsv.Write(property, change.After))
                {
                    serialized.ApplyModifiedProperties();
                    applied++;
                }
                else rejected++;
            }

            AssetDatabase.SaveAssets();
            _pendingChanges = null;

            _message = rejected > 0
                ? $"{applied}건 적용, {rejected}건은 형식이 맞지 않아 건너뛰었습니다."
                : $"{applied}건 적용했습니다.";
            _messageType = rejected > 0 ? MessageType.Warning : MessageType.Info;
        }
    }
}
