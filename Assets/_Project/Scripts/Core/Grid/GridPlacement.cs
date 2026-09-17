using UnityEngine;

namespace ForagerCP
{
    /// 배치 대상이 드는 셀 좌표. 인스펙터에서 셀을 고치면 씬에서 바로 스냅된다.
    /// _size로 2칸 이상 먹는 구조물까지 표현하고, 앵커는 항상 최소 코너 셀(_cell)이다.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GridPlacement : MonoBehaviour
    {
        [SerializeField] Vector2Int _cell;
        [SerializeField] Vector2Int _size = Vector2Int.one;
        [SerializeField] float _heightOffset = 0.5f;

        /// 임시 도형(큐브 등)을 footprint 크기에 맞춰 늘려주는 스위치.
        /// 실제 아트가 들어오면 꺼둔다 — 모델 스케일을 건드리면 안 되므로 기본값 false.
        [SerializeField] bool _stretchToFootprint;

        public Vector2Int Cell => _cell;
        public Vector2Int Size => _size;
        public RectInt Footprint => new RectInt(_cell.x, _cell.y, _size.x, _size.y);

        void OnEnable() => Apply();

        void OnValidate()
        {
            _size = new Vector2Int(Mathf.Max(1, _size.x), Mathf.Max(1, _size.y));
            Apply();
        }

        public void SetCell(Vector2Int cell)
        {
            _cell = cell;
            Apply();
        }

        public void SetSize(Vector2Int size)
        {
            _size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            Apply();
        }

        /// 배치 툴이 한 번에 넣는 경로. 값마다 Apply가 돌지 않도록 마지막에 한 번만 스냅한다.
        public void Configure(Vector2Int cell, Vector2Int size, float heightOffset)
        {
            _cell = cell;
            _size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            _heightOffset = heightOffset;
            Apply();
        }

        /// 현재 월드 위치가 어느 셀인지 읽어 _cell로 흡수한다(씬에서 손으로 끌어놓은 뒤 정렬용).
        public void SnapFromWorld()
        {
            MapGrid grid = MapGrid.Active;
            if (grid == null) return;

            _cell = grid.WorldToCell(transform.position);
            Apply();
        }

        public void Apply()
        {
            MapGrid grid = MapGrid.Active;
            if (grid == null) return;

            transform.position = grid.CellToWorld(_cell, _size) + Vector3.up * _heightOffset;

            if (!_stretchToFootprint) return;
            Vector3 scale = transform.localScale;
            transform.localScale = new Vector3(_size.x * grid.CellSize, scale.y, _size.y * grid.CellSize);
        }

        public bool Overlaps(GridPlacement other)
        {
            if (other == null || other == this) return false;
            return Footprint.Overlaps(other.Footprint);
        }

        void OnDrawGizmosSelected()
        {
            MapGrid grid = MapGrid.Active;
            if (grid == null) return;

            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            Vector3 center = grid.CellToWorld(_cell, _size);
            Vector3 size = new Vector3(_size.x * grid.CellSize, 0.05f, _size.y * grid.CellSize);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
