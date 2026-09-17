using ForagerCP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForagerCP.EditorTools
{
    /// 씬뷰에서 셀 단위로 맵 오브젝트를 찍어 배치하는 팔레트.
    /// 배치 결과는 전부 씬 오브젝트 + GridPlacement(셀 좌표)로 남는다 — 런타임 생성 없음.
    public class MapPaletteWindow : EditorWindow
    {
        [SerializeField] MapPalette _palette;
        [SerializeField] Transform _parent;
        [SerializeField] int _selectedIndex;
        [SerializeField] bool _paintMode;

        Vector2Int _hoveredCell;
        Vector2 _scroll;

        [MenuItem("Tools/ForagerCP/Map Palette %#m")]
        static void Open() => GetWindow<MapPaletteWindow>("Map Palette");

        void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;

        void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        void OnGUI()
        {
            _palette = (MapPalette)EditorGUILayout.ObjectField("팔레트", _palette, typeof(MapPalette), false);
            _parent = (Transform)EditorGUILayout.ObjectField("부모(선택)", _parent, typeof(Transform), true);

            MapGrid grid = MapGrid.Active;
            if (grid == null)
            {
                EditorGUILayout.HelpBox("씬에 MapGrid가 없음. 빈 오브젝트에 MapGrid를 붙여줘.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("셀 크기", grid.CellSize + "m / 커서 셀 " + _hoveredCell);

            GUI.backgroundColor = _paintMode ? new Color(0.4f, 1f, 0.5f) : Color.white;
            if (GUILayout.Button(_paintMode ? "배치 모드 ON — 씬뷰 좌클릭 배치 / Ctrl+좌클릭 삭제" : "배치 모드 OFF", GUILayout.Height(28f)))
            {
                _paintMode = !_paintMode;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (_palette == null)
            {
                EditorGUILayout.HelpBox("팔레트 에셋을 지정해줘. (Create > ForagerCP > Map Palette)", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _palette.Entries.Count; i++)
            {
                MapPalette.Entry entry = _palette.Entries[i];
                bool selected = i == _selectedIndex;

                GUI.backgroundColor = selected ? entry.SwatchColor : Color.white;
                string label = entry.DisplayName + "   [" + entry.Size.x + "x" + entry.Size.y + "]";
                if (GUILayout.Button(label, GUILayout.Height(24f))) _selectedIndex = i;
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("선택한 오브젝트를 가장 가까운 셀로 스냅"))
            {
                SnapSelection();
            }
        }

        void OnSceneGUI(SceneView view)
        {
            if (!_paintMode) return;

            MapGrid grid = MapGrid.Active;
            if (grid == null) return;

            // AddDefaultControl: 씬뷰 기본 선택 동작을 가로채야 클릭이 배치로 들어온다.
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            Event current = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, grid.Origin);
            if (!groundPlane.Raycast(ray, out float distance)) return;

            _hoveredCell = grid.WorldToCell(ray.GetPoint(distance));
            Vector2Int size = SelectedSize();
            DrawCellPreview(grid, _hoveredCell, size);

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
            {
                if (current.control || current.shift) EraseAt(_hoveredCell);
                else PlaceAt(_hoveredCell);

                current.Use();
            }

            view.Repaint();
            Repaint();
        }

        Vector2Int SelectedSize()
        {
            MapPalette.Entry entry = SelectedEntry();
            return entry == null ? Vector2Int.one : entry.Size;
        }

        MapPalette.Entry SelectedEntry()
        {
            if (_palette == null) return null;
            if (_selectedIndex < 0 || _selectedIndex >= _palette.Entries.Count) return null;
            return _palette.Entries[_selectedIndex];
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
            Handles.Label(center + Vector3.up * 0.4f, cell.ToString());
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

        void PlaceAt(Vector2Int cell)
        {
            MapPalette.Entry entry = SelectedEntry();
            if (entry == null || entry.Prefab == null)
            {
                Debug.LogWarning("팔레트 항목/프리팹이 비어 있음");
                return;
            }

            GridPlacement blocker = FindBlocker(cell, entry.Size);
            if (blocker != null)
            {
                Debug.LogWarning($"셀 {cell} 이미 점유: {blocker.name}", blocker);
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab, _parent);
            if (instance == null) return;

            Undo.RegisterCreatedObjectUndo(instance, "Place Map Object");

            GridPlacement placement = instance.GetComponent<GridPlacement>();
            if (placement == null) placement = Undo.AddComponent<GridPlacement>(instance);

            placement.Configure(cell, entry.Size, entry.HeightOffset);
            EditorUtility.SetDirty(instance);
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Selection.activeGameObject = instance;
        }

        void EraseAt(Vector2Int cell)
        {
            GridPlacement target = FindBlocker(cell, Vector2Int.one);
            if (target == null) return;

            EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            Undo.DestroyObjectImmediate(target.gameObject);
        }

        void SnapSelection()
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                GridPlacement placement = go.GetComponent<GridPlacement>();
                if (placement == null) placement = Undo.AddComponent<GridPlacement>(go);

                Undo.RecordObject(placement, "Snap To Grid");
                Undo.RecordObject(go.transform, "Snap To Grid");
                placement.SnapFromWorld();
                EditorUtility.SetDirty(go);
            }

            if (Selection.gameObjects.Length > 0) EditorSceneManager.MarkSceneDirty(Selection.gameObjects[0].scene);
        }
    }
}
