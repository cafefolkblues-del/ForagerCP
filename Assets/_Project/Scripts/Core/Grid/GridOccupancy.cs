using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 어느 칸이 차 있는지 아는 유일한 창구. GridPlacement가 켜지고 꺼질 때 스스로 등록한다.
    ///
    /// 물리(OverlapBox)로 대신하지 않는 이유: 캐고 나서 리스폰을 기다리는 광물은 콜라이더가 꺼져 있다.
    /// 물리로 물으면 빈 칸으로 보여서 그 위에 블럭이 놓이고, 광물이 되살아날 때 서로를 뚫는다.
    /// 점유는 '지금 보이는 모양'이 아니라 그리드가 기억해야 할 사실이라 여기 모은다.
    ///
    /// 살아있는 것(플레이어·몬스터)은 여기 없다 — 그리드에 매이지 않고 자유롭게 움직이므로
    /// 그쪽은 놓는 쪽이 물리로 확인한다.
    public static class GridOccupancy
    {
        static readonly Dictionary<Vector2Int, GridPlacement> _cells = new Dictionary<Vector2Int, GridPlacement>();

        /// 어느 칸을 쥐고 있었는지 기억해둔다. 없으면 해제할 때 사전 전체를 훑어야 한다.
        static readonly Dictionary<GridPlacement, RectInt> _footprints = new Dictionary<GridPlacement, RectInt>();

        /// 플레이 모드에 들어갈 때 비운다.
        /// static이라 에디터에서 [ExecuteAlways]로 쌓인 항목이 그대로 넘어오기 때문.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            _cells.Clear();
            _footprints.Clear();
        }

        public static int Count => _footprints.Count;

        public static void Register(GridPlacement placement)
        {
            if (placement == null) return;

            // 셀이 바뀌었을 수 있으므로 옛 자리를 먼저 비운다. Apply()가 바뀔 때마다 이리로 온다.
            Unregister(placement);

            RectInt footprint = placement.Footprint;
            for (int x = footprint.xMin; x < footprint.xMax; x++)
            {
                for (int y = footprint.yMin; y < footprint.yMax; y++)
                {
                    _cells[new Vector2Int(x, y)] = placement;
                }
            }

            _footprints[placement] = footprint;
        }

        public static void Unregister(GridPlacement placement)
        {
            if (placement == null) return;
            if (!_footprints.TryGetValue(placement, out RectInt footprint)) return;

            for (int x = footprint.xMin; x < footprint.xMax; x++)
            {
                for (int y = footprint.yMin; y < footprint.yMax; y++)
                {
                    var cell = new Vector2Int(x, y);

                    // 디자이너가 두 구조물을 겹쳐 놓았을 수도 있다. 남의 칸까지 지우지 않도록 주인을 확인한다.
                    if (_cells.TryGetValue(cell, out GridPlacement owner) && owner == placement) _cells.Remove(cell);
                }
            }

            _footprints.Remove(placement);
        }

        /// 빈 칸이면 null. 파괴된 오브젝트가 남아 있으면 그 자리에서 정리하고 빈 칸으로 답한다.
        public static GridPlacement At(Vector2Int cell)
        {
            if (!_cells.TryGetValue(cell, out GridPlacement owner)) return null;
            if (owner != null) return owner;

            _cells.Remove(cell);
            return null;
        }

        /// ignore는 자기 자신을 옮길 때 쓴다 — 지금 자리와 겹친다고 거부당하면 한 칸도 못 움직인다.
        public static bool IsFree(RectInt footprint, GridPlacement ignore = null)
        {
            for (int x = footprint.xMin; x < footprint.xMax; x++)
            {
                for (int y = footprint.yMin; y < footprint.yMax; y++)
                {
                    GridPlacement owner = At(new Vector2Int(x, y));
                    if (owner != null && owner != ignore) return false;
                }
            }
            return true;
        }
    }
}
