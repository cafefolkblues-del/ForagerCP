using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 구역 하나의 "땅 덩어리". 섬이 솟아오를 때 무엇을 같이 들어 올릴지 답한다.
    ///
    /// 씬을 구역별로 다시 묶지 않는다. 배치 팔레트가 "카테고리 = 폴더 하나"를 전제로 해서,
    /// 구역마다 광물 폴더를 두면 새 광물이 엉뚱한 구역 폴더로 들어간다(docs/notes/구역_소속_기법.md).
    /// 대신 소속은 위치로 묻는다 — 몸통(바닥)은 여기 연결하고, 배치물은 칸 명부(GridOccupancy)에서 찾는다.
    [DisallowMultipleComponent]
    public class RegionLand : MonoBehaviour
    {
        static readonly Dictionary<RegionDefinition, RegionLand> _byRegion = new Dictionary<RegionDefinition, RegionLand>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _byRegion.Clear();

        [SerializeField] RegionDefinition _region;

        [Tooltip("섬 몸통·바닥. 구역 배치물이 아닌, 땅 그 자체(지금은 바닥 타일 폴더, 나중에 섬 모델).")]
        [SerializeField] Transform _ground;

        [Tooltip("섬이 다 올라온 뒤에 켜는 스포너.")]
        [SerializeField] List<MonsterSpawner> _spawners = new List<MonsterSpawner>();

        public RegionDefinition Region => _region;
        public Transform Ground => _ground;
        public IReadOnlyList<MonsterSpawner> Spawners => _spawners;

        public static RegionLand For(RegionDefinition region) =>
            region != null && _byRegion.TryGetValue(region, out RegionLand land) ? land : null;

        void OnEnable()
        {
            if (_region != null) _byRegion[_region] = this;
        }

        void OnDisable()
        {
            if (_region != null && For(_region) == this) _byRegion.Remove(_region);
        }

        readonly List<GridPlacement> _placements = new List<GridPlacement>();

        /// 지금 이 구역에 속한 것 전부: 몸통 + 칸 명부에 등록된 배치물.
        /// 섬이 솟는 순간 한 번 묻는 용도. 매 프레임 부르지 말 것.
        public void CollectMembers(List<Transform> into)
        {
            into.Clear();
            if (_ground != null) into.Add(_ground);

            GridOccupancy.CollectInRegion(_region, _placements);
            for (int i = 0; i < _placements.Count; i++)
            {
                Transform member = _placements[i].transform;

                // 몸통 밑에 이미 들어 있으면 몸통과 같이 움직이므로 두 번 넣지 않는다.
                if (_ground != null && member.IsChildOf(_ground)) continue;
                into.Add(member);
            }
        }
    }
}
