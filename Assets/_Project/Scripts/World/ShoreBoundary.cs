using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 걸을 수 있는 땅의 바깥 테두리를 따라 세우는 투명벽. 바다로도, 아직 안 솟은 섬으로도 못 나간다.
    ///
    /// 섬 모양(몸통·모델)에 물리를 얹지 않고 구역 데이터에서 만든다 — 섬 모델이 바뀌어도 경계는 칸과 정확히 맞고,
    /// 높이는 GroundLock이 고정하므로 바닥 충돌도 필요 없다.
    ///
    /// "걸을 수 있다" = 구역이 있고, 열려 있고, 섬이 다 솟아 있다.
    /// 구역이 열리거나 섬이 다 솟으면 그때 다시 세운다(매 프레임 확인하지 않는다).
    public class ShoreBoundary : MonoBehaviour
    {
        [SerializeField] RegionAccess _access;

        [Tooltip("벽 두께(m). 걷는 칸을 파먹지 않도록 경계선 바깥쪽에 세운다.")]
        [SerializeField] float _thickness = 0.4f;

        [Tooltip("벽 아래·위 끝 높이. 높이가 고정이라 낮아도 되지만, 넉백 구체 판정이 넘지 않을 만큼은 있어야 한다.")]
        [SerializeField] float _bottom = -1f;
        [SerializeField] float _top = 3f;

        readonly List<BoxCollider> _walls = new List<BoxCollider>();
        readonly List<RegionDefinition> _regions = new List<RegionDefinition>();
        readonly List<RectInt> _areas = new List<RectInt>();
        bool _dirty = true;

        public int WallCount { get; private set; }

        void Awake()
        {
            if (_access == null) _access = FindFirstObjectByType<RegionAccess>();
        }

        void OnEnable()
        {
            RegionMap.Changed += MarkDirty;
            IslandRiser.StateChanged += OnIslandChanged;
            if (_access != null)
            {
                _access.Unlocked += OnRegionChanged;
                _access.Locked += OnRegionChanged;
            }
            _dirty = true;
        }

        void OnDisable()
        {
            RegionMap.Changed -= MarkDirty;
            IslandRiser.StateChanged -= OnIslandChanged;
            if (_access != null)
            {
                _access.Unlocked -= OnRegionChanged;
                _access.Locked -= OnRegionChanged;
            }
        }

        void MarkDirty() => _dirty = true;
        void OnRegionChanged(RegionDefinition region) => _dirty = true;
        void OnIslandChanged(IslandRiser riser) => _dirty = true;

        // 같은 프레임에 신호가 여럿 와도(열림 + 솟기 시작) 한 번만 다시 세운다.
        void LateUpdate()
        {
            if (_dirty) Rebuild();
        }

        bool IsWalkable(RegionDefinition region)
        {
            if (region == null) return false;
            if (_access != null && !_access.IsUnlocked(region)) return false;

            IslandRiser riser = IslandRiser.For(region);
            return riser == null || riser.Current == IslandRiser.State.Risen;
        }

        public void Rebuild()
        {
            _dirty = false;

            var walkable = new HashSet<Vector2Int>();
            RegionMap.CollectRegions(_regions);
            for (int r = 0; r < _regions.Count; r++)
            {
                if (!IsWalkable(_regions[r])) continue;

                RegionMap.CollectAreas(_regions[r], _areas);
                for (int a = 0; a < _areas.Count; a++)
                {
                    RectInt area = _areas[a];
                    for (int x = area.xMin; x < area.xMax; x++)
                    {
                        for (int y = area.yMin; y < area.yMax; y++) walkable.Add(new Vector2Int(x, y));
                    }
                }
            }

            // 경계 조각을 줄 단위로 모은다. 키 = (방향, 줄 번호), 값 = 그 줄에서 막아야 할 칸 위치들.
            var runs = new Dictionary<(int side, int line), List<int>>();
            foreach (Vector2Int cell in walkable)
            {
                if (!walkable.Contains(cell + Vector2Int.left)) Add(runs, 0, cell.x, cell.y);
                if (!walkable.Contains(cell + Vector2Int.right)) Add(runs, 1, cell.x, cell.y);
                if (!walkable.Contains(cell + Vector2Int.down)) Add(runs, 2, cell.y, cell.x);
                if (!walkable.Contains(cell + Vector2Int.up)) Add(runs, 3, cell.y, cell.x);
            }

            MapGrid grid = MapGrid.Active;
            float size = grid != null ? grid.CellSize : 1f;
            Vector3 origin = grid != null ? grid.Origin : Vector3.zero;

            int used = 0;
            foreach (KeyValuePair<(int side, int line), List<int>> pair in runs)
            {
                List<int> positions = pair.Value;
                positions.Sort();

                // 이어진 칸끼리 벽 하나로 합친다. 칸마다 상자를 세우면 수백 개가 된다.
                int start = positions[0];
                int previous = start;
                for (int i = 1; i <= positions.Count; i++)
                {
                    if (i < positions.Count && positions[i] == previous + 1)
                    {
                        previous = positions[i];
                        continue;
                    }

                    PlaceWall(used++, pair.Key.side, pair.Key.line, start, previous, size, origin);
                    if (i < positions.Count)
                    {
                        start = positions[i];
                        previous = start;
                    }
                }
            }

            for (int i = used; i < _walls.Count; i++) _walls[i].enabled = false;
            WallCount = used;
        }

        static void Add(Dictionary<(int, int), List<int>> runs, int side, int line, int position)
        {
            if (!runs.TryGetValue((side, line), out List<int> list))
            {
                list = new List<int>();
                runs[(side, line)] = list;
            }
            list.Add(position);
        }

        /// side: 0 왼쪽(−X) · 1 오른쪽(+X) · 2 아래(−Z) · 3 위(+Z). line은 그 칸의 X(0·1) 또는 Z(2·3).
        void PlaceWall(int index, int side, int line, int from, int to, float size, Vector3 origin)
        {
            BoxCollider wall = WallAt(index);
            wall.enabled = true;

            float length = (to - from + 1) * size;
            float along = (from + to) * 0.5f * size;
            float height = _top - _bottom;
            float centerY = (_top + _bottom) * 0.5f;

            // 칸 경계는 칸 중심에서 반 칸. 벽은 그 바깥으로 두께의 절반만큼 더 나간다.
            float edge = 0.5f * size + _thickness * 0.5f;
            Vector3 center;
            Vector3 extent;
            switch (side)
            {
                case 0: center = new Vector3(line * size - edge, centerY, along); extent = new Vector3(_thickness, height, length); break;
                case 1: center = new Vector3(line * size + edge, centerY, along); extent = new Vector3(_thickness, height, length); break;
                case 2: center = new Vector3(along, centerY, line * size - edge); extent = new Vector3(length, height, _thickness); break;
                default: center = new Vector3(along, centerY, line * size + edge); extent = new Vector3(length, height, _thickness); break;
            }

            wall.transform.position = origin + center;
            wall.size = extent;
        }

        BoxCollider WallAt(int index)
        {
            while (_walls.Count <= index)
            {
                var go = new GameObject("Wall_" + _walls.Count);
                go.transform.SetParent(transform, false);
                _walls.Add(go.AddComponent<BoxCollider>());
            }
            return _walls[index];
        }
    }
}
