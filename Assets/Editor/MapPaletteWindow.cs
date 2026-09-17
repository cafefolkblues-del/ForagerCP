using System.Collections.Generic;
using ForagerCP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForagerCP.EditorTools
{
    /// 씬 화면에서 클릭으로 구조물을 놓는 배치 도구.
    /// 놓인 결과는 전부 씬 오브젝트 + GridPlacement(칸 좌표)로 남는다 — 게임 실행 중 생성되는 것은 없다.
    public class MapPaletteWindow : EditorWindow
    {
        const string PaletteDefaultPath = "Assets/_Project/MapPalette.asset";

        [SerializeField] MapPalette _palette;
        [SerializeField] MapFolder _targetFolder;
        [SerializeField] int _selectedIndex;
        [SerializeField] bool _placing;
        [SerializeField] int _rotationSteps;
        [SerializeField] bool _showAdvanced;

        Vector2Int _hoveredCell;
        Vector2 _scroll;
        string _status = "";

        [MenuItem("Tools/ForagerCP/맵 배치 %#m")]
        static void Open() => GetWindow<MapPaletteWindow>("맵 배치");

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            if (_palette == null) _palette = AssetDatabase.LoadAssetAtPath<MapPalette>(PaletteDefaultPath);
        }

        void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        // ---------- 창 ----------

        void OnGUI()
        {
            if (MapGrid.Active == null)
            {
                EditorGUILayout.HelpBox("이 씬에는 격자(MapGrid)가 없습니다. 맵이 있는 씬을 열어주세요.", MessageType.Error);
                return;
            }

            if (_palette == null)
            {
                EditorGUILayout.HelpBox("구조물 목록 파일을 찾지 못했습니다. 아래 '고급'에서 직접 지정해주세요.", MessageType.Warning);
                DrawAdvanced();
                return;
            }

            DrawPlaceButton();
            EditorGUILayout.Space(6f);
            DrawStructureList();
            EditorGUILayout.Space(6f);
            DrawRotation();
            EditorGUILayout.Space(6f);
            DrawFolderPicker();
            EditorGUILayout.Space(8f);
            DrawHelp();
            EditorGUILayout.Space(6f);
            DrawAdvanced();
        }

        void DrawPlaceButton()
        {
            GUI.backgroundColor = _placing ? new Color(0.45f, 1f, 0.6f) : Color.white;
            string label = _placing ? "배치 중 — 끄려면 다시 누르세요" : "배치 시작";
            if (GUILayout.Button(label, GUILayout.Height(34f)))
            {
                _placing = !_placing;
                _status = "";
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        void DrawStructureList()
        {
            EditorGUILayout.LabelField("구조물 선택", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(90f));
            for (int i = 0; i < _palette.Entries.Count; i++)
            {
                MapPalette.Entry entry = _palette.Entries[i];
                bool selected = i == _selectedIndex;

                GUI.backgroundColor = selected ? entry.SwatchColor : Color.white;
                string size = entry.Size.x == 1 && entry.Size.y == 1 ? "1칸" : entry.Size.x + "×" + entry.Size.y + "칸";
                string label = (selected ? "● " : "  ") + entry.DisplayName + "   (" + size + ")";

                if (GUILayout.Button(label, GUILayout.Height(26f)))
                {
                    _selectedIndex = i;
                    if (!entry.AllowRotation) _rotationSteps = 0;
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawRotation()
        {
            MapPalette.Entry entry = SelectedEntry();
            using (new EditorGUI.DisabledScope(entry == null || !entry.AllowRotation))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("방향", (_rotationSteps * 90) + "°", GUILayout.Width(120f));
                if (GUILayout.Button("90° 돌리기  (R)", GUILayout.Height(22f)))
                {
                    _rotationSteps = (_rotationSteps + 1) % 4;
                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndHorizontal();
            }

            if (entry != null && !entry.AllowRotation)
                EditorGUILayout.LabelField(" ", "이 구조물은 방향이 없습니다.", EditorStyles.miniLabel);
        }

        void DrawFolderPicker()
        {
            EditorGUILayout.LabelField("넣을 그룹", EditorStyles.boldLabel);

            List<MapFolder> folders = new List<MapFolder>(FindObjectsByType<MapFolder>(FindObjectsSortMode.None));
            string[] names = new string[folders.Count + 1];
            names[0] = "자동 (구조물 종류에 맞는 그룹)";
            for (int i = 0; i < folders.Count; i++) names[i + 1] = folders[i].Label;

            int current = 0;
            if (_targetFolder != null)
            {
                int found = folders.IndexOf(_targetFolder);
                current = found >= 0 ? found + 1 : 0;
            }

            int picked = EditorGUILayout.Popup(" ", current, names);
            _targetFolder = picked == 0 ? null : folders[picked - 1];

            if (folders.Count == 0)
                EditorGUILayout.HelpBox("그룹이 없습니다. 배치하면 종류에 맞는 그룹이 자동으로 만들어집니다.", MessageType.None);
        }

        void DrawHelp()
        {
            EditorGUILayout.HelpBox(
                "씬 화면에서\n" +
                "· 왼쪽 클릭 — 놓기\n" +
                "· Ctrl + 왼쪽 클릭 — 지우기\n" +
                "· R — 90° 돌리기\n" +
                "빨간 칸은 이미 다른 구조물이 있는 자리입니다.",
                MessageType.None);

            if (GUILayout.Button("고른 오브젝트를 가까운 칸에 맞추기")) SnapSelection();
        }

        void DrawAdvanced()
        {
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "고급 (개발용)");
            if (!_showAdvanced) return;

            EditorGUI.indentLevel++;
            _palette = (MapPalette)EditorGUILayout.ObjectField("구조물 목록", _palette, typeof(MapPalette), false);
            EditorGUILayout.LabelField("커서 칸", _hoveredCell.ToString());
            if (MapGrid.Active != null) EditorGUILayout.LabelField("칸 크기", MapGrid.Active.CellSize + "m");
            EditorGUI.indentLevel--;
        }

        // ---------- 씬 뷰 ----------

        void OnSceneGUI(SceneView view)
        {
            if (!_placing) return;

            MapGrid grid = MapGrid.Active;
            if (grid == null) return;

            // 기본 선택 동작을 가로채야 클릭이 배치로 들어온다.
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            Event current = Event.current;

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.R)
            {
                MapPalette.Entry rotatingEntry = SelectedEntry();
                if (rotatingEntry != null && rotatingEntry.AllowRotation)
                {
                    _rotationSteps = (_rotationSteps + 1) % 4;
                    current.Use();
                    Repaint();
                }
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, grid.Origin);
            if (!groundPlane.Raycast(ray, out float distance)) return;

            _hoveredCell = grid.WorldToCell(ray.GetPoint(distance));
            DrawCellPreview(grid, _hoveredCell, PlacedSize());

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                if (current.control || current.shift) EraseAt(_hoveredCell);
                else PlaceAt(_hoveredCell);

                current.Use();
            }

            view.Repaint();
            Repaint();
        }

        void DrawCellPreview(MapGrid grid, Vector2Int cell, Vector2Int size)
        {
            bool blocked = FindBlocker(cell, size) != null;
            Vector3 center = grid.CellToWorld(cell, size);
            float halfX = size.x * grid.CellSize * 0.5f;
            float halfZ = size.y * grid.CellSize * 0.5f;

            Vector3[] corners =
            {
                center + new Vector3(-halfX, 0.02f, -halfZ),
                center + new Vector3(-halfX, 0.02f, halfZ),
                center + new Vector3(halfX, 0.02f, halfZ),
                center + new Vector3(halfX, 0.02f, -halfZ)
            };

            Color fill = blocked ? new Color(1f, 0.3f, 0.3f, 0.25f) : new Color(0.3f, 1f, 0.5f, 0.25f);
            Color outline = blocked ? new Color(1f, 0.3f, 0.3f, 0.9f) : new Color(0.3f, 1f, 0.5f, 0.9f);
            Handles.DrawSolidRectangleWithOutline(corners, fill, outline);

            MapPalette.Entry entry = SelectedEntry();
            string caption = entry == null ? cell.ToString() : entry.DisplayName + "  " + cell;
            Handles.Label(center + Vector3.up * 0.5f, caption);
        }

        // ---------- 동작 ----------

        MapPalette.Entry SelectedEntry()
        {
            if (_palette == null) return null;
            if (_selectedIndex < 0 || _selectedIndex >= _palette.Entries.Count) return null;
            return _palette.Entries[_selectedIndex];
        }

        /// 회전까지 반영된, 실제로 먹는 칸 수.
        Vector2Int PlacedSize()
        {
            MapPalette.Entry entry = SelectedEntry();
            if (entry == null) return Vector2Int.one;

            int steps = entry.AllowRotation ? _rotationSteps : 0;
            return steps % 2 == 0 ? entry.Size : new Vector2Int(entry.Size.y, entry.Size.x);
        }

        GridPlacement FindBlocker(Vector2Int cell, Vector2Int size)
        {
            RectInt footprint = new RectInt(cell.x, cell.y, size.x, size.y);
            GridPlacement[] placements = FindObjectsByType<GridPlacement>(FindObjectsSortMode.None);

            for (int i = 0; i < placements.Length; i++)
            {
                if (placements[i].Footprint.Overlaps(footprint)) return placements[i];
            }
            return null;
        }

        /// 지정 그룹이 없으면 항목의 Category에 맞는 그룹을 찾고, 그것도 없으면 만든다.
        Transform ResolveParent(MapPalette.Entry entry)
        {
            if (_targetFolder != null) return _targetFolder.transform;

            MapFolder[] folders = FindObjectsByType<MapFolder>(FindObjectsSortMode.None);
            for (int i = 0; i < folders.Length; i++)
            {
                if (folders[i].Category == entry.Category) return folders[i].transform;
            }

            var created = new GameObject(entry.Category);
            Undo.RegisterCreatedObjectUndo(created, "그룹 만들기");
            MapFolder folder = Undo.AddComponent<MapFolder>(created);
            folder.Setup(entry.Category, entry.Category);
            return created.transform;
        }

        void PlaceAt(Vector2Int cell)
        {
            MapPalette.Entry entry = SelectedEntry();
            if (entry == null || entry.Prefab == null)
            {
                _status = "놓을 구조물을 먼저 고르세요.";
                return;
            }

            Vector2Int size = PlacedSize();
            GridPlacement blocker = FindBlocker(cell, size);
            if (blocker != null)
            {
                _status = "그 자리에는 이미 '" + blocker.name + "' 이(가) 있습니다.";
                return;
            }

            Transform parent = ResolveParent(entry);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab, parent);
            if (instance == null) return;

            Undo.RegisterCreatedObjectUndo(instance, "구조물 놓기");

            GridPlacement placement = instance.GetComponent<GridPlacement>();
            if (placement == null) placement = Undo.AddComponent<GridPlacement>(instance);

            placement.Configure(cell, entry.Size, entry.HeightOffset, entry.AllowRotation ? _rotationSteps : 0);
            EditorUtility.SetDirty(instance);
            EditorSceneManager.MarkSceneDirty(instance.scene);

            Selection.activeGameObject = instance;
            _status = entry.DisplayName + " 놓음 " + cell;
        }

        void EraseAt(Vector2Int cell)
        {
            GridPlacement target = FindBlocker(cell, Vector2Int.one);
            if (target == null)
            {
                _status = "그 자리에는 지울 것이 없습니다.";
                return;
            }

            _status = target.name + " 지움";
            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Undo.DestroyObjectImmediate(target.gameObject);
        }

        void SnapSelection()
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                GridPlacement placement = go.GetComponent<GridPlacement>();
                if (placement == null) placement = Undo.AddComponent<GridPlacement>(go);

                Undo.RecordObject(placement, "칸에 맞추기");
                Undo.RecordObject(go.transform, "칸에 맞추기");
                placement.SnapFromWorld();
                EditorUtility.SetDirty(go);
            }

            if (Selection.gameObjects.Length > 0)
            {
                EditorSceneManager.MarkSceneDirty(Selection.gameObjects[0].scene);
                _status = Selection.gameObjects.Length + "개를 칸에 맞췄습니다.";
            }
        }
    }
}
