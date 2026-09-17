using UnityEngine;

namespace ForagerCP
{
    /// 맵 좌표계. 셀은 XZ 평면의 정수 좌표이고, 셀 (0,0)의 중심이 이 오브젝트의 위치다.
    /// 플레이어 이동은 그리드와 무관하게 자유(WASD 물리 이동) — 그리드는 배치 전용.
    [ExecuteAlways]
    public class MapGrid : MonoBehaviour
    {
        [SerializeField] float _cellSize = 1f;
        [SerializeField] bool _drawGizmo = true;
        [SerializeField] Vector2Int _gizmoRange = new Vector2Int(30, 30);

        static MapGrid _active;

        /// 씬에 하나만 두는 전제. 도메인 리로드로 static이 날아가도 찾아서 복구한다.
        public static MapGrid Active
        {
            get
            {
                if (_active == null) _active = FindFirstObjectByType<MapGrid>();
                return _active;
            }
        }

        public float CellSize => _cellSize;
        public Vector3 Origin => transform.position;

        void OnEnable() => _active = this;

        void OnDisable()
        {
            if (_active == this) _active = null;
        }

        /// footprint(size) 기준 중심 월드 좌표.
        /// size가 짝수면 중심이 셀 경계에 걸리므로 (size-1)/2 만큼 밀어 앵커를 최소 코너 셀로 맞춘다.
        public Vector3 CellToWorld(Vector2Int cell, Vector2Int size)
        {
            float x = (cell.x + (size.x - 1) * 0.5f) * _cellSize;
            float z = (cell.y + (size.y - 1) * 0.5f) * _cellSize;
            return Origin + new Vector3(x, 0f, z);
        }

        public Vector3 CellToWorld(Vector2Int cell) => CellToWorld(cell, Vector2Int.one);

        /// RoundToInt: 셀 중심 기준 좌표계라 반올림이 곧 "가장 가까운 셀".
        public Vector2Int WorldToCell(Vector3 world)
        {
            Vector3 local = world - Origin;
            return new Vector2Int(
                Mathf.RoundToInt(local.x / _cellSize),
                Mathf.RoundToInt(local.z / _cellSize));
        }

        void OnDrawGizmos()
        {
            if (!_drawGizmo) return;

            Gizmos.color = new Color(1f, 1f, 1f, 0.12f);
            float half = _cellSize * 0.5f;
            Vector3 origin = Origin;

            for (int x = -_gizmoRange.x; x <= _gizmoRange.x + 1; x++)
            {
                Vector3 a = origin + new Vector3(x * _cellSize - half, 0f, -_gizmoRange.y * _cellSize - half);
                Vector3 b = origin + new Vector3(x * _cellSize - half, 0f, _gizmoRange.y * _cellSize + half);
                Gizmos.DrawLine(a, b);
            }

            for (int z = -_gizmoRange.y; z <= _gizmoRange.y + 1; z++)
            {
                Vector3 a = origin + new Vector3(-_gizmoRange.x * _cellSize - half, 0f, z * _cellSize - half);
                Vector3 b = origin + new Vector3(_gizmoRange.x * _cellSize + half, 0f, z * _cellSize - half);
                Gizmos.DrawLine(a, b);
            }
        }
    }
}
