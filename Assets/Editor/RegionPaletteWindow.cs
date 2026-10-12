using System.Collections.Generic;
using System.IO;
using ForagerCP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ForagerCP.EditorTools
{
    /// 씬 화면에서 드래그로 땅을 구획하는 도구.
    /// 맵 배치 창과 같은 방식으로 움직인다 — 결과는 전부 씬 오브젝트(RegionVolume)로 남고,
    /// 게임 실행 중 만들어지는 것은 없다.
    public class RegionPaletteWindow : EditorWindow
    {
        const string RegionFolder = "Assets/_Project/Regions";
        const string RegionsRootName = "Regions";

        enum Mode { Off, Draw, Erase }

        [SerializeField] RegionDefinition _selectedRegion;
        [SerializeField] Mode _mode = Mode.Off;
        [SerializeField] string _newRegionName = "";

        readonly List<RegionDefinition> _regions = new List<RegionDefinition>();
        Vector2 _scroll;
        string _status = "";

        Vector2Int _hoveredCell;
        Vector2Int _dragStart;
        bool _dragging;

        [MenuItem("Tools/ForagerCP/지역 편집 %#r")]
        static void Open() => GetWindow<RegionPaletteWindow>("지역 편집");

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            RefreshRegions();
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.RepaintAll();
        }

        void OnSelectionChange() => Repaint();

        // ------------------------------------------------------------------ 창

        void OnGUI()
        {
            if (MapGrid.Active == null)
            {
                EditorGUILayout.HelpBox("이 씬에는 격자(MapGrid)가 없습니다. 맵이 있는 씬을 열어주세요.", MessageType.Error);
                return;
            }

            DrawModeButtons();
            EditorGUILayout.Space(6f);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawRegionList();
            EditorGUILayout.Space(8f);
            DrawPlacedList();
            EditorGUILayout.Space(8f);
            DrawWarnings();
            EditorGUILayout.Space(8f);
            DrawHelp();
            EditorGUILayout.EndScrollView();
        }

        void DrawModeButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawModeButton(Mode.Draw, "구획 그리기", new Color(0.45f, 1f, 0.6f));
                DrawModeButton(Mode.Erase, "구획 지우기", new Color(1f, 0.55f, 0.5f));
            }

            if (_mode == Mode.Draw && _selectedRegion == null)
            {
                EditorGUILayout.HelpBox("아래에서 어느 지역을 그릴지 먼저 골라주세요.", MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);
        }

        void DrawModeButton(Mode mode, string label, Color onColor)
        {
            bool active = _mode == mode;
            GUI.backgroundColor = active ? onColor : Color.white;

            if (GUILayout.Button(active ? label + " 중" : label, GUILayout.Height(32f)))
            {
                _mode = active ? Mode.Off : mode;
                _status = "";
                _dragging = false;
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = Color.white;
        }

        void DrawRegionList()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("지역", EditorStyles.boldLabel);
                if (GUILayout.Button("새로고침", GUILayout.Width(70f))) RefreshRegions();
            }

            foreach (RegionDefinition region in _regions)
            {
                if (region == null) continue;

                bool selected = region == _selectedRegion;
                using (new EditorGUILayout.VerticalScope(selected ? EditorStyles.helpBox : GUIStyle.none))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Rect swatch = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f));
                        Color color = region.MapColor;
                        color.a = 1f;
                        EditorGUI.DrawRect(swatch, color);

                        if (GUILayout.Button(region.DisplayName, selected ? EditorStyles.boldLabel : EditorStyles.label))
                        {
                            _selectedRegion = region;
                            SceneView.RepaintAll();
                        }

                        if (GUILayout.Button("열기", GUILayout.Width(44f)))
                        {
                            Selection.activeObject = region;
                            EditorGUIUtility.PingObject(region);
                        }
                    }

                    EditorGUILayout.LabelField("   " + DescribeConditions(region), EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                _newRegionName = EditorGUILayout.TextField(_newRegionName);
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_newRegionName)))
                {
                    if (GUILayout.Button("새 지역 만들기", GUILayout.Width(110f))) CreateRegion(_newRegionName.Trim());
                }
            }
        }

        static string DescribeConditions(RegionDefinition region)
        {
            if (!region.IsLockable) return "누구나 드나들 수 있음";

            var parts = new List<string>();
            foreach (RegionRequirement requirement in region.Requirements)
            {
                if (requirement != null) parts.Add(requirement.Describe());
            }
            return string.Join(" + ", parts);
        }

        void DrawPlacedList()
        {
            EditorGUILayout.LabelField("씬에 깔린 구획", EditorStyles.boldLabel);

            RegionVolume[] volumes = FindObjectsByType<RegionVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (volumes.Length == 0)
            {
                EditorGUILayout.LabelField("   아직 없습니다.", EditorStyles.miniLabel);
                return;
            }

            foreach (RegionVolume volume in volumes)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    RectInt area = volume.Area;
                    string regionName = volume.Region != null ? volume.Region.DisplayName : "(지역 없음)";
                    string label = $"{regionName}  {area.width}x{area.height}  ({area.xMin},{area.yMin})";

                    if (GUILayout.Button(label, EditorStyles.label))
                    {
                        Selection.activeGameObject = volume.gameObject;
                        EditorGUIUtility.PingObject(volume.gameObject);
                        SceneView.RepaintAll();
                    }

                    if (GUILayout.Button("지우기", GUILayout.Width(56f)))
                    {
                        Undo.DestroyObjectImmediate(volume.gameObject);
                        MarkDirty();
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }

        void DrawWarnings()
        {
            RegionVolume[] volumes = FindObjectsByType<RegionVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (volumes.Length == 0) return;

            var uncovered = new List<GameObject>();
            foreach (GridPlacement placement in FindObjectsByType<GridPlacement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!Covered(volumes, placement.Cell)) uncovered.Add(placement.gameObject);
            }

            if (uncovered.Count == 0)
            {
                EditorGUILayout.HelpBox("놓인 것이 전부 어느 지역 안에 들어 있습니다.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                $"{uncovered.Count}개가 어느 지역에도 들어 있지 않습니다. 지역 밖은 조건 없이 드나들 수 있습니다.",
                MessageType.Warning);

            if (GUILayout.Button("그것들만 골라보기")) Selection.objects = uncovered.ToArray();
        }

        static void DrawHelp()
        {
            EditorGUILayout.HelpBox(
                "구획 그리기: 지역을 고르고 씬 화면에서 드래그하면 그 사각형이 지역이 됩니다.\n" +
                "한 지역을 여러 번 그리면 ㄱ자든 ㄷ자든 됩니다.\n" +
                "구획 지우기: 지울 칸을 클릭하세요.\n" +
                "크기 고치기: 구획을 고르면 네 변에 손잡이가 생깁니다. 끌면 칸 단위로 맞춰집니다.",
                MessageType.None);
        }

        // ------------------------------------------------------------------ 씬 화면

        void OnSceneGUI(SceneView view)
        {
            MapGrid grid = MapGrid.Active;
            if (grid == null) return;

            RegionVolume[] volumes = FindObjectsByType<RegionVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            DrawVolumes(grid, volumes);

            RegionVolume selected = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<RegionVolume>()
                : null;

            if (selected != null) DrawResizeHandles(grid, selected);

            if (_mode == Mode.Off) return;

            // 기본 선택 동작을 가로채야 드래그가 구획으로 들어온다.
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            Event current = Event.current;
            if (current.alt) return;

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            var ground = new Plane(Vector3.up, grid.Origin);
            if (!ground.Raycast(ray, out float distance)) return;

            _hoveredCell = grid.WorldToCell(ray.GetPoint(distance));

            if (_mode == Mode.Draw) HandleDraw(grid, volumes, current);
            else HandleErase(grid, volumes, current);

            view.Repaint();
            Repaint();
        }

        void HandleDraw(MapGrid grid, RegionVolume[] volumes, Event current)
        {
            if (current.type == EventType.MouseDown && current.button == 0 && _selectedRegion != null)
            {
                _dragStart = _hoveredCell;
                _dragging = true;
                current.Use();
            }

            RectInt preview = _dragging ? FromCorners(_dragStart, _hoveredCell) : FromCorners(_hoveredCell, _hoveredCell);
            bool clash = Clashes(volumes, preview, null);

            Color fill = clash ? new Color(1f, 0.35f, 0.3f, 0.25f) : SelectedFill();
            DrawRect(grid, preview, fill, Opaque(fill));

            string caption = _selectedRegion != null ? _selectedRegion.DisplayName : "지역을 먼저 고르세요";
            if (clash) caption += " — 다른 지역과 겹칩니다";
            Handles.Label(Center(grid, preview) + Vector3.up * 0.6f, $"{caption}  {preview.width}x{preview.height}");

            if (!_dragging) return;
            if (current.type != EventType.MouseUp || current.button != 0) return;

            _dragging = false;
            current.Use();

            if (clash)
            {
                _status = "다른 지역과 겹쳐서 만들지 않았습니다.";
                return;
            }

            CreateVolume(preview);
        }

        void HandleErase(MapGrid grid, RegionVolume[] volumes, Event current)
        {
            RegionVolume hit = VolumeAt(volumes, _hoveredCell);
            if (hit != null)
            {
                DrawRect(grid, hit.Area, new Color(1f, 0.35f, 0.3f, 0.3f), new Color(1f, 0.35f, 0.3f, 0.9f));
                Handles.Label(Center(grid, hit.Area) + Vector3.up * 0.6f, "지울 구획");
            }

            if (current.type != EventType.MouseDown || current.button != 0) return;
            current.Use();

            if (hit == null)
            {
                _status = "그 칸에는 구획이 없습니다.";
                return;
            }

            string regionName = hit.Region != null ? hit.Region.DisplayName : "(지역 없음)";
            Undo.DestroyObjectImmediate(hit.gameObject);
            MarkDirty();

            _status = $"{regionName} 구획 하나를 지웠습니다.";
        }

        /// 네 변 가운데에 손잡이를 놓는다. 변을 따로 잡아야 한쪽만 늘릴 수 있다.
        void DrawResizeHandles(MapGrid grid, RegionVolume volume)
        {
            RectInt area = volume.Area;
            float cell = grid.CellSize;
            Vector3 origin = grid.Origin;

            float midX = origin.x + ((area.xMin + area.xMax) * 0.5f - 0.5f) * cell;
            float midZ = origin.z + ((area.yMin + area.yMax) * 0.5f - 0.5f) * cell;

            int xMin = EdgeHandle(new Vector3(EdgeWorld(origin.x, area.xMin, cell), 0.1f, midZ), Vector3.right, origin.x, cell, area.xMin);
            int xMax = EdgeHandle(new Vector3(EdgeWorld(origin.x, area.xMax, cell), 0.1f, midZ), Vector3.right, origin.x, cell, area.xMax);
            int yMin = EdgeHandle(new Vector3(midX, 0.1f, EdgeWorld(origin.z, area.yMin, cell)), Vector3.forward, origin.z, cell, area.yMin);
            int yMax = EdgeHandle(new Vector3(midX, 0.1f, EdgeWorld(origin.z, area.yMax, cell)), Vector3.forward, origin.z, cell, area.yMax);

            // 끌다가 뒤집히면 폭이 0이나 음수가 된다. 최소 한 칸은 남긴다.
            if (xMax <= xMin) xMax = xMin + 1;
            if (yMax <= yMin) yMax = yMin + 1;

            var next = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
            if (next.Equals(area)) return;

            Undo.RecordObject(volume, "구획 크기 고치기");
            volume.Configure(volume.Region, new Vector2Int(next.xMin, next.yMin), new Vector2Int(next.width, next.height));
            MarkDirty();
        }

        /// 경계선 b는 칸 중심 좌표계에서 (b - 0.5)칸 자리에 있다.
        static float EdgeWorld(float origin, int boundary, float cellSize) => origin + (boundary - 0.5f) * cellSize;

        static int EdgeHandle(Vector3 position, Vector3 direction, float origin, float cellSize, int value)
        {
            float size = HandleUtility.GetHandleSize(position) * 0.11f;

            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.Slider(position, direction, size, Handles.DotHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck()) return value;

            float local = (direction == Vector3.right ? moved.x : moved.z) - origin;
            return Mathf.RoundToInt(local / cellSize + 0.5f);
        }

        void DrawVolumes(MapGrid grid, RegionVolume[] volumes)
        {
            foreach (RegionVolume volume in volumes)
            {
                Color color = volume.Region != null ? volume.Region.MapColor : new Color(1f, 0.4f, 0.4f, 0.2f);
                DrawRect(grid, volume.Area, color, Opaque(color));

                string label = volume.Region != null ? volume.Region.DisplayName : "지역 없음";
                Handles.Label(Center(grid, volume.Area) + Vector3.up * 0.3f, label);
            }
        }

        static void DrawRect(MapGrid grid, RectInt area, Color fill, Color outline)
        {
            var size = new Vector2Int(area.width, area.height);
            Vector3 center = grid.CellToWorld(new Vector2Int(area.xMin, area.yMin), size);

            float halfX = area.width * grid.CellSize * 0.5f;
            float halfZ = area.height * grid.CellSize * 0.5f;

            Vector3[] corners =
            {
                center + new Vector3(-halfX, 0.03f, -halfZ),
                center + new Vector3(-halfX, 0.03f, halfZ),
                center + new Vector3(halfX, 0.03f, halfZ),
                center + new Vector3(halfX, 0.03f, -halfZ)
            };

            Handles.DrawSolidRectangleWithOutline(corners, fill, outline);
        }

        static Vector3 Center(MapGrid grid, RectInt area)
            => grid.CellToWorld(new Vector2Int(area.xMin, area.yMin), new Vector2Int(area.width, area.height));

        Color SelectedFill()
        {
            if (_selectedRegion == null) return new Color(0.6f, 0.6f, 0.6f, 0.2f);

            Color color = _selectedRegion.MapColor;
            color.a = 0.3f;
            return color;
        }

        static Color Opaque(Color color)
        {
            color.a = 0.9f;
            return color;
        }

        // ------------------------------------------------------------------ 동작

        void RefreshRegions()
        {
            _regions.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:RegionDefinition"))
            {
                var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (region != null) _regions.Add(region);
            }

            if (_selectedRegion == null && _regions.Count > 0) _selectedRegion = _regions[0];
        }

        void CreateRegion(string displayName)
        {
            Directory.CreateDirectory(RegionFolder);

            string path = AssetDatabase.GenerateUniqueAssetPath($"{RegionFolder}/Region_{displayName}.asset");
            var region = CreateInstance<RegionDefinition>();
            AssetDatabase.CreateAsset(region, path);

            var serialized = new SerializedObject(region);
            serialized.FindProperty("_displayName").stringValue = displayName;

            // 지역마다 색이 달라야 씬에서 구분된다. 처음 색을 골라주지 않으면 전부 같은 파랑이 된다.
            serialized.FindProperty("_mapColor").colorValue = NextColor(_regions.Count);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();

            RefreshRegions();
            _selectedRegion = region;
            _newRegionName = "";
            _status = $"{displayName} 을(를) 만들었습니다. 조건은 '열기'에서 넣어주세요.";
        }

        static Color NextColor(int index)
        {
            float hue = (index * 0.17f) % 1f;
            Color color = Color.HSVToRGB(hue, 0.55f, 0.95f);
            color.a = 0.18f;
            return color;
        }

        void CreateVolume(RectInt area)
        {
            Transform parent = ResolveRoot();

            var go = new GameObject(_selectedRegion.DisplayName);
            Undo.RegisterCreatedObjectUndo(go, "구획 만들기");
            go.transform.SetParent(parent, false);

            RegionVolume volume = Undo.AddComponent<RegionVolume>(go);
            volume.Configure(_selectedRegion, new Vector2Int(area.xMin, area.yMin), new Vector2Int(area.width, area.height));

            Selection.activeGameObject = go;
            MarkDirty();

            _status = $"{_selectedRegion.DisplayName} {area.width}x{area.height} 구획을 만들었습니다.";
        }

        static Transform ResolveRoot()
        {
            var existing = GameObject.Find(RegionsRootName);
            if (existing != null) return existing.transform;

            var created = new GameObject(RegionsRootName);
            Undo.RegisterCreatedObjectUndo(created, "지역 그룹 만들기");
            return created.transform;
        }

        /// 드래그한 두 칸을 양 끝으로 보는 사각형. 끝 칸도 포함해야 한 칸을 콕 집어 그릴 수 있다.
        static RectInt FromCorners(Vector2Int a, Vector2Int b)
        {
            int xMin = Mathf.Min(a.x, b.x);
            int yMin = Mathf.Min(a.y, b.y);
            int xMax = Mathf.Max(a.x, b.x);
            int yMax = Mathf.Max(a.y, b.y);

            return new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        static bool Covered(RegionVolume[] volumes, Vector2Int cell)
        {
            foreach (RegionVolume volume in volumes)
            {
                if (volume.Area.Contains(cell)) return true;
            }
            return false;
        }

        static RegionVolume VolumeAt(RegionVolume[] volumes, Vector2Int cell)
        {
            foreach (RegionVolume volume in volumes)
            {
                if (volume.Area.Contains(cell)) return volume;
            }
            return null;
        }

        /// 같은 지역끼리는 겹쳐도 된다(ㄱ자를 만들 때 자연스럽게 겹친다).
        /// 다른 지역과 겹치면 그 칸의 주인이 배치 순서로 정해져버리므로 막는다.
        bool Clashes(RegionVolume[] volumes, RectInt area, RegionVolume ignore)
        {
            foreach (RegionVolume volume in volumes)
            {
                if (volume == ignore) continue;
                if (volume.Region == _selectedRegion) continue;
                if (volume.Area.Overlaps(area)) return true;
            }
            return false;
        }

        static void MarkDirty() => EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
