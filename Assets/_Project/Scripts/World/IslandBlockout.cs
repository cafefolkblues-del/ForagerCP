using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 구역 모양 그대로의 임시 섬 몸통: 윗면 + 바다 밑까지 내려가는 절벽. 보이기만 한다.
    ///
    /// 충돌은 없다. 높이는 GroundLock이 고정하므로 바닥 충돌을 쓰는 곳이 없고,
    /// 바다로 나가는 것은 ShoreBoundary(투명벽)가 막는다. 임시 모양에 물리를 얹으면 모델이 바뀔 때 같이 흔들린다.
    /// 모양은 구역 볼륨(RegionMap)에서 오고, 볼륨이 바뀌면 다시 만든다. 메시는 씬에 저장하지 않는다.
    /// 전달 규격: docs/notes/섬_바닥_모델_규격.md
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class IslandBlockout : MonoBehaviour
    {
        [Tooltip("비우면 부모의 RegionLand에서 가져온다.")]
        [SerializeField] RegionDefinition _region;

        [Tooltip("걷는 면 높이. 바닥 타일이 위에 깔리는 구역은 타일 밑면(−0.06)으로 낮춘다.")]
        [SerializeField] float _top;

        [Tooltip("절벽이 내려가는 깊이. 잠긴 섬이 가라앉는 깊이(3m)보다 깊어야 솟기 전에 바닥이 안 보인다.")]
        [SerializeField] float _bottom = -4f;

        [Tooltip("섬 모델이 오면 끈다.")]
        [SerializeField] bool _visible = true;

        [SerializeField] Material _topMaterial;
        [SerializeField] Material _cliffMaterial;

        Mesh _mesh;
        bool _dirty;
        readonly List<RectInt> _areas = new List<RectInt>();

        public RegionDefinition Region
        {
            get
            {
                if (_region != null) return _region;
                RegionLand land = GetComponentInParent<RegionLand>();
                return land != null ? land.Region : null;
            }
        }

        void OnEnable()
        {
            RegionMap.Changed += MarkDirty;
            Build();
        }

        void OnDisable()
        {
            RegionMap.Changed -= MarkDirty;

            if (_mesh == null) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
            _mesh = null;
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this != null && isActiveAndEnabled) Build(); };
#endif
        }

        void MarkDirty() => _dirty = true;

        // 볼륨이 이 컴포넌트보다 늦게 켜질 수 있다. 바뀐 걸 모아 두었다가 한 번만 다시 만든다.
        void LateUpdate()
        {
            if (_dirty) Build();
        }

        void Build()
        {
            _dirty = false;

            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.enabled = _visible;
            meshRenderer.sharedMaterials = new[] { _topMaterial, _cliffMaterial };

            RegionMap.CollectAreas(Region, _areas);
            var cells = new HashSet<Vector2Int>();
            for (int i = 0; i < _areas.Count; i++)
            {
                RectInt area = _areas[i];
                for (int x = area.xMin; x < area.xMax; x++)
                {
                    for (int y = area.yMin; y < area.yMax; y++) cells.Add(new Vector2Int(x, y));
                }
            }

            MapGrid grid = MapGrid.Active;
            float size = grid != null ? grid.CellSize : 1f;
            Vector3 origin = grid != null ? grid.Origin : Vector3.zero;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tops = new List<int>();
            var cliffs = new List<int>();

            foreach (Vector2Int cell in cells)
            {
                // 칸 (0,0)의 중심이 원점이라 경계는 반 칸 밀려 있다.
                float x0 = origin.x + (cell.x - 0.5f) * size;
                float z0 = origin.z + (cell.y - 0.5f) * size;
                float x1 = x0 + size;
                float z1 = z0 + size;

                AddQuad(vertices, normals, uvs, tops, Vector3.up,
                    new Vector3(x0, _top, z0), new Vector3(x0, _top, z1), new Vector3(x1, _top, z1), new Vector3(x1, _top, z0));

                // 옆 칸이 이 구역이 아니면 그쪽은 절벽이다.
                if (!cells.Contains(cell + Vector2Int.left))
                    AddQuad(vertices, normals, uvs, cliffs, Vector3.left,
                        new Vector3(x0, _bottom, z1), new Vector3(x0, _top, z1), new Vector3(x0, _top, z0), new Vector3(x0, _bottom, z0));
                if (!cells.Contains(cell + Vector2Int.right))
                    AddQuad(vertices, normals, uvs, cliffs, Vector3.right,
                        new Vector3(x1, _bottom, z0), new Vector3(x1, _top, z0), new Vector3(x1, _top, z1), new Vector3(x1, _bottom, z1));
                if (!cells.Contains(cell + Vector2Int.down))
                    AddQuad(vertices, normals, uvs, cliffs, Vector3.back,
                        new Vector3(x0, _bottom, z0), new Vector3(x0, _top, z0), new Vector3(x1, _top, z0), new Vector3(x1, _bottom, z0));
                if (!cells.Contains(cell + Vector2Int.up))
                    AddQuad(vertices, normals, uvs, cliffs, Vector3.forward,
                        new Vector3(x1, _bottom, z1), new Vector3(x1, _top, z1), new Vector3(x0, _top, z1), new Vector3(x0, _bottom, z1));
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "IslandBlockout", hideFlags = HideFlags.DontSave };
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetNormals(normals);
            _mesh.SetUVs(0, uvs);
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(tops, 0);
            _mesh.SetTriangles(cliffs, 1);
            _mesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = _mesh;

            // 정점이 월드 좌표로 놓였으니 이 오브젝트는 부모 기준 원점에 있어야 한다.
            // (부모가 솟을 때 같이 움직이는 것은 그대로 된다.)
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        // a→b→c→d 시계 방향(바깥에서 볼 때). 면마다 정점을 따로 둬서 모서리가 각지게 칠해진다.
        static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
            Vector3 normal, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int i = 0; i < 4; i++) normals.Add(normal);

            // 월드 좌표로 UV를 깔아 칸마다 무늬가 이어지게 한다.
            bool horizontal = Mathf.Abs(normal.y) > 0.5f;
            uvs.Add(horizontal ? new Vector2(a.x, a.z) : new Vector2(a.x + a.z, a.y));
            uvs.Add(horizontal ? new Vector2(b.x, b.z) : new Vector2(b.x + b.z, b.y));
            uvs.Add(horizontal ? new Vector2(c.x, c.z) : new Vector2(c.x + c.z, c.y));
            uvs.Add(horizontal ? new Vector2(d.x, d.z) : new Vector2(d.x + d.z, d.y));

            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

#if UNITY_EDITOR
        /// 아트에 넘길 참고용 블록아웃. 월드 좌표 그대로(원점 = 월드 원점) 내보낸다.
        [ContextMenu("OBJ로 내보내기")]
        void ExportObj()
        {
            if (_mesh == null || Region == null) return;

            string key = Region.name.Replace("Region_", "");
            string folder = $"Assets/_Project/Art/Models/Island_{key}";
            System.IO.Directory.CreateDirectory(folder);
            string path = $"{folder}/Blockout_{key}.obj";

            var text = new System.Text.StringBuilder();
            text.AppendLine($"# {Region.name} blockout — 1 unit = 1 m, Y up, world origin. 윗면 y={_top}, 절벽 바닥 y={_bottom}");
            Vector3[] vertices = _mesh.vertices;
            // OBJ는 오른손 좌표계라 X를 뒤집는다(Unity로 다시 불러오면 제자리).
            for (int i = 0; i < vertices.Length; i++)
                text.AppendLine($"v {-vertices[i].x:0.###} {vertices[i].y:0.###} {vertices[i].z:0.###}");

            for (int sub = 0; sub < _mesh.subMeshCount; sub++)
            {
                text.AppendLine(sub == 0 ? "g top" : "g cliff");
                int[] triangles = _mesh.GetTriangles(sub);
                for (int i = 0; i < triangles.Length; i += 3)
                    text.AppendLine($"f {triangles[i] + 1} {triangles[i + 2] + 1} {triangles[i + 1] + 1}");
            }

            System.IO.File.WriteAllText(path, text.ToString());
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log($"블록아웃 내보냄: {path}", this);
        }
#endif
    }
}
