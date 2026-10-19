using UnityEngine;

namespace ForagerCP
{
    /// "저기 있는 것을 건드려도 되는가"를 묻는 한 곳.
    ///
    /// 잠긴 구역의 것은 캐지도, F로 열지도 못한다. 벽 너머로 손이 닿으면
    /// 개방 비용을 치르지 않고 자원을 가져갈 수 있고, 실제로 벽에 붙어 캐는 일이 나왔다.
    ///
    /// 몬스터의 '자기 구역 밖 표적 제외'와 비슷해 보이지만 묻는 것이 다르다 —
    /// 저쪽은 "같은 구역인가", 이쪽은 "그 구역이 열렸는가"다. 그래서 합치지 않는다.
    public static class RegionGate
    {
        static RegionAccess _access;

        /// 씬에 하나뿐이라 찾아서 들고 있는다. 플레이 모드가 바뀌면 다시 찾는다.
        static RegionAccess Access
        {
            get
            {
                if (_access != null) return _access;
                return _access = Object.FindFirstObjectByType<RegionAccess>();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _access = null;

        /// 구획하지 않은 땅과 잠금 조건이 없는 구역은 늘 통과시킨다.
        /// 판정을 못 하는 상황(RegionAccess 없음)도 통과시킨다 — 못 건드리게 막는 쪽이
        /// 더 위험하다. 구획을 아직 안 그은 맵에서 아무것도 못 캐게 되기 때문.
        public static bool Allows(Vector3 worldPosition)
        {
            RegionDefinition region = RegionMap.At(worldPosition);
            if (region == null || !region.IsLockable) return true;

            RegionAccess access = Access;
            return access == null || access.IsUnlocked(region);
        }

        public static bool Allows(Transform target) => target == null || Allows(target.position);
    }
}
