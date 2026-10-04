using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 지역 한 곳의 정체성. 새 지역 = 이 에셋 하나 추가 + 씬에 범위 깔기, 코드 수정 없음.
    ///
    /// 지역끼리의 '차이'는 대부분 배치로 표현된다 — 그 구역에 어떤 광물과 몹을 놓았는지가 곧 차이다.
    /// 여기 들어갈 것은 배치로 표현되지 않는 것(구역 전체에 걸리는 수치, 연출)뿐이고,
    /// 기획이 확정되면 필드를 늘리면 된다. 밸런스 표는 필드를 리플렉션으로 훑으므로 알아서 따라온다.
    [CreateAssetMenu(menuName = "ForagerCP/지역", fileName = "Region")]
    public class RegionDefinition : ScriptableObject
    {
        [SerializeField] string _displayName;

        /// 씬 기즈모와 미니맵에서 이 지역을 구분하는 색.
        [SerializeField] Color _mapColor = new Color(0.4f, 0.8f, 1f, 0.25f);

        [Header("들어갈 조건")]
        /// 비어 있으면 누구나 들어간다. 조건이 여럿이면 전부 만족해야 한다.
        [SerializeField] List<RegionRequirement> _requirements = new List<RegionRequirement>();

        /// 한 번 열면 계속 열려 있을지. 끄면 들어갈 때마다 조건을 다시 치른다(통행료 같은 것).
        /// 기획이 어느 쪽을 고르든 이 스위치 하나로 흡수되도록 둔다.
        [SerializeField] bool _permanentUnlock = true;

        /// 이름을 비워두면 에셋 파일명을 쓴다 — ItemDefinition과 같은 규칙.
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;

        public Color MapColor => _mapColor;
        public IReadOnlyList<RegionRequirement> Requirements => _requirements;
        public bool PermanentUnlock => _permanentUnlock;

        /// 조건이 하나도 없으면 애초에 잠긴 곳이 아니다. 시작 지역이 이쪽.
        public bool IsLockable
        {
            get
            {
                for (int i = 0; i < _requirements.Count; i++)
                {
                    if (_requirements[i] != null) return true;
                }
                return false;
            }
        }
    }
}
