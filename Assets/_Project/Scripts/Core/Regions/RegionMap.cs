using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 어느 셀이 어느 지역인지 아는 유일한 창구. GridOccupancy와 같은 방식으로 볼륨이 스스로 등록한다.
    ///
    /// 지역 수가 적으니 볼륨 목록을 훑어도 되지만, 플레이어 위치 판정이 매 프레임 후보가 되는 자리라
    /// 셀 사전으로 두어 조회를 한 번에 끝낸다.
    public static class RegionMap
    {
        static readonly Dictionary<Vector2Int, RegionVolume> _cells = new Dictionary<Vector2Int, RegionVolume>();
        static readonly Dictionary<RegionVolume, RectInt> _areas = new Dictionary<RegionVolume, RectInt>();

        /// 플레이 모드에 들어갈 때 비운다. [ExecuteAlways]라 에디터에서 쌓인 항목이 그대로 넘어온다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            _cells.Clear();
            _areas.Clear();
        }

        public static int VolumeCount => _areas.Count;

        public static void Register(RegionVolume volume)
        {
            if (volume == null) return;

            // 범위가 바뀌었을 수 있으니 옛 자리를 먼저 비운다.
            Unregister(volume);

            RectInt area = volume.Area;
            for (int x = area.xMin; x < area.xMax; x++)
            {
                for (int y = area.yMin; y < area.yMax; y++) _cells[new Vector2Int(x, y)] = volume;
            }

            _areas[volume] = area;
        }

        public static void Unregister(RegionVolume volume)
        {
            if (volume == null) return;
            if (!_areas.TryGetValue(volume, out RectInt area)) return;

            for (int x = area.xMin; x < area.xMax; x++)
            {
                for (int y = area.yMin; y < area.yMax; y++)
                {
                    var cell = new Vector2Int(x, y);

                    // 볼륨이 겹쳐 있을 수 있다. 남의 칸까지 지우지 않도록 주인을 확인한다.
                    if (_cells.TryGetValue(cell, out RegionVolume owner) && owner == volume) _cells.Remove(cell);
                }
            }

            _areas.Remove(volume);
        }

        /// 어느 지역에도 속하지 않는 셀이면 null. 맵 바깥이나 아직 구획하지 않은 땅이다.
        public static RegionDefinition At(Vector2Int cell)
        {
            if (!_cells.TryGetValue(cell, out RegionVolume volume)) return null;
            if (volume != null) return volume.Region;

            _cells.Remove(cell);
            return null;
        }

        public static RegionDefinition At(Vector3 world)
        {
            MapGrid grid = MapGrid.Active;
            return grid == null ? null : At(grid.WorldToCell(world));
        }

        /// 검사기가 쓴다 — 씬에 깔린 지역 목록.
        public static void CollectRegions(List<RegionDefinition> into)
        {
            into.Clear();
            foreach (RegionVolume volume in _areas.Keys)
            {
                if (volume == null || volume.Region == null) continue;
                if (!into.Contains(volume.Region)) into.Add(volume.Region);
            }
        }
    }
}
