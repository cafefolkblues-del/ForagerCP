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
        [SerializeField, Range(0, 3)] int _rotationSteps;
        [SerializeField] float _heightOffset = 0.5f;

        /// 임시 도형(큐브 등)을 footprint 크기에 맞춰 늘려주는 스위치.
        /// 실제 아트가 들어오면 꺼둔다 — 모델 스케일을 건드리면 안 되므로 기본값 false.
        [SerializeField] bool _stretchToFootprint;

        public Vector2Int Cell => _cell;

        /// 회전 전 원본 크기. 스케일 보정처럼 "모델 자체의 가로세로"가 필요한 곳이 쓴다.
        public Vector2Int Size => _size;

        public int RotationSteps => _rotationSteps;

        /// 90°/270° 회전이면 가로세로가 뒤집힌 칸을 먹는다. 점유 판정은 항상 이쪽을 봐야 한다.
        public Vector2Int RotatedSize => (_rotationSteps % 2 == 0)
            ? _size
            : new Vector2Int(_size.y, _size.x);

        public RectInt Footprint => new RectInt(_cell.x, _cell.y, RotatedSize.x, RotatedSize.y);

        void OnEnable() => Apply();

        void OnValidate()
        {
            _size = new Vector2Int(Mathf.Max(1, _size.x), Mathf.Max(1, _size.y));
            _rotationSteps = ((_rotationSteps % 4) + 4) % 4;
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
        public void Configure(Vector2Int cell, Vector2Int size, float heightOffset, int rotationSteps = 0)
        {
            _cell = cell;
            _size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            _heightOffset = heightOffset;
            _rotationSteps = ((rotationSteps % 4) + 4) % 4;
            Apply();
        }

        public void Rotate(int stepDelta)
        {
            _rotationSteps = (((_rotationSteps + stepDelta) % 4) + 4) % 4;
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

            transform.position = grid.CellToWorld(_cell, RotatedSize) + Vector3.up * _heightOffset;
            transform.rotation = Quaternion.Euler(0f, 90f * _rotationSteps, 0f);

            if (!_stretchToFootprint) return;

            // 스케일은 회전 전 크기로 맞춘다. 회전은 transform.rotation이 이미 처리하므로
            // 여기서 RotatedSize를 쓰면 90°마다 모양이 뒤집혀 두 번 적용된다.
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
            Vector2Int rotated = RotatedSize;
            Vector3 center = grid.CellToWorld(_cell, rotated);
            Vector3 size = new Vector3(rotated.x * grid.CellSize, 0.05f, rotated.y * grid.CellSize);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
