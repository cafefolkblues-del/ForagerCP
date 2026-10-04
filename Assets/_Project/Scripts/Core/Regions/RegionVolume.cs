using UnityEngine;

namespace ForagerCP
{
    /// 지역이 차지하는 셀 사각형 하나. 같은 지역을 가리키는 볼륨을 여러 개 놓으면 L자든 뭐든 된다.
    ///
    /// 트리거 콜라이더가 아니라 셀 좌표인 이유: 배치도 설치도 전부 셀 단위라
    /// 경계가 연속 좌표면 경계에 걸친 블럭이 어느 지역인지 답할 수 없다.
    /// 씬 뷰 편집은 기즈모가 대신한다.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class RegionVolume : MonoBehaviour
    {
        [SerializeField] RegionDefinition _region;
        [SerializeField] Vector2Int _cell;
        [SerializeField] Vector2Int _size = new Vector2Int(10, 10);

        public RegionDefinition Region => _region;
        public RectInt Area => new RectInt(_cell.x, _cell.y, Mathf.Max(1, _size.x), Mathf.Max(1, _size.y));

        void OnEnable() => RegionMap.Register(this);
        void OnDisable() => RegionMap.Unregister(this);

        void OnValidate()
        {
            _size = new Vector2Int(Mathf.Max(1, _size.x), Mathf.Max(1, _size.y));

            // 셀을 고치면 바로 반영돼야 디자이너가 기즈모를 보며 맞출 수 있다.
            if (isActiveAndEnabled) RegionMap.Register(this);
        }

        public void Configure(RegionDefinition region, Vector2Int cell, Vector2Int size)
        {
            _region = region;
            _cell = cell;
            _size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));

            if (isActiveAndEnabled) RegionMap.Register(this);
        }

        /// 범위를 바닥에 깔린 사각형으로 그린다. 선택하지 않아도 보여야 전체 구획이 한눈에 들어온다.
        void OnDrawGizmos()
        {
            MapGrid grid = MapGrid.Active;
            if (grid == null) return;

            RectInt area = Area;
            var size = new Vector2Int(area.width, area.height);
            Vector3 center = grid.CellToWorld(new Vector2Int(area.xMin, area.yMin), size);
            var extent = new Vector3(area.width * grid.CellSize, 0.02f, area.height * grid.CellSize);

            Color color = _region != null ? _region.MapColor : new Color(1f, 0.4f, 0.4f, 0.25f);
            Gizmos.color = color;
            Gizmos.DrawCube(center, extent);

            color.a = 1f;
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, extent);
        }
    }
}
