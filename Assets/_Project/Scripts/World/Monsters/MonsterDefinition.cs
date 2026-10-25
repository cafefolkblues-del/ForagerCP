using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 몬스터 한 종류의 수치. 새 몬스터 = 이 에셋 + 프리팹 + 배치 팔레트 등록, 코드 수정 없음.
    ///
    /// 공격은 여기 수치로 들고 있지 않고 MonsterAttack 에셋 목록으로 든다.
    /// '유형'이란 결국 공격 조합이고, 조합을 수치 필드로 적으면 한 마리가 공격을 둘 이상
    /// 가지는 순간(근접 + 투사체) 필드를 통째로 복제해야 한다.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터", fileName = "Monster")]
    public class MonsterDefinition : ScriptableObject
    {
        [SerializeField] string _displayName;

        /// 이 종류를 세울 때 쓸 프리팹.
        ///
        /// 프리팹이 정의를 드는 게 아니라 **정의가 프리팹을 든다**. 방향이 이래야
        /// 같은 몸통 하나를 구역별 정의 여럿이 돌려 쓸 수 있다 —
        /// 기획이 "형태를 유지하되 광물 색으로 구분"이라 적은 그대로다.
        [SerializeField] GameObject _prefab;

        /// 몸 색. 구역이 올라갈수록 다른 색을 준다.
        /// 머티리얼을 종류×구역만큼 만들지 않으려고 MaterialPropertyBlock으로 칠한다.
        [SerializeField] Color _bodyColor = Color.white;

        [Header("전투")]
        [SerializeField] int _maxHp = 20;

        /// 쓸 수 있는 공격들. 매번 사거리에 맞고 재사용 대기가 끝난 것 중 앞쪽을 고르므로,
        /// 먼저 쓰이길 바라는 것을 위에 둔다(원거리 → 근접 순이 보통).
        [SerializeField] List<MonsterAttack> _attacks = new List<MonsterAttack>();

        /// 행동 조립표(추적·예고·빈틈·쿨다운 대기·후퇴에 어떤 구현을 쓸지).
        /// 구역별 강화판은 같은 패턴을 공유한다. 비우면 기본 구현만 쓰고 후퇴하지 않는다.
        [SerializeField] MonsterPattern _pattern;

        [Header("이동 / 인지")]
        [SerializeField] float _moveSpeed = 2.5f;
        [SerializeField] float _detectRange = 8f;

        /// 스폰 지점에서 이만큼 멀어지면 표적을 놓고 돌아간다.
        /// 확장 2-2의 구역 경계가 들어오면 이 거리 대신 구역 판정으로 교체된다.
        [SerializeField] float _leashDistance = 12f;

        [Header("피격")]
        /// 맞았을 때 밀려나는 세기와 시간. 종류마다 다르게 밀리도록 여기 둔다(Knockback 컴포넌트는 플레이어와 공용).
        [SerializeField] float _knockbackForce = 9f;
        [SerializeField] float _knockbackDuration = 0.16f;

        /// 맞으면 이만큼 공격을 못 한다(경직). 반드시 넉백 시간보다 길게 —
        /// 밀리는 동안 쌓아둔 예고가 끝나자마자 터지면 안 된다(확정 규칙, 씬 검사가 경고한다).
        [SerializeField] float _hitstunTime = 0.22f;

        /// 표적을 놓치고 집으로 돌아가는 동안 초당 회복하는 양(최대 체력의 %). 가득 찰 때까지 이어진다.
        /// 구역을 벗어나 전투가 초기화될 때도 같은 규칙으로 돌아간다.
        [SerializeField] float _returnHealPercentPerSecond = 30f;

        [Header("처치 보상 / 재등장")]
        [SerializeField] int _expReward = 2;
        [SerializeField] float _respawnDelay = 15f;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public GameObject Prefab => _prefab;
        public Color BodyColor => _bodyColor;
        public int MaxHp => Mathf.Max(1, _maxHp);
        public IReadOnlyList<MonsterAttack> Attacks => _attacks;
        public MonsterPattern Pattern => _pattern;
        public float MoveSpeed => Mathf.Max(0f, _moveSpeed);
        public float DetectRange => Mathf.Max(0f, _detectRange);
        public float LeashDistance => Mathf.Max(0f, _leashDistance);
        public float KnockbackForce => Mathf.Max(0f, _knockbackForce);
        public float KnockbackDuration => Mathf.Max(0.01f, _knockbackDuration);
        public float HitstunTime => Mathf.Max(0f, _hitstunTime);
        public float ReturnHealPercentPerSecond => Mathf.Max(0f, _returnHealPercentPerSecond);

        /// 경직이 넉백보다 길어야 한다는 규칙을 어겼는가.
        public bool HitstunTooShort => HitstunTime <= KnockbackDuration;
        public int ExpReward => Mathf.Max(0, _expReward);
        public float RespawnDelay => Mathf.Max(0f, _respawnDelay);

        /// 목록의 첫 공격. 마법사·검사기처럼 '대표 한 방'이 필요한 쪽이 쓴다.
        public MonsterAttack PrimaryAttack
        {
            get
            {
                for (int i = 0; i < _attacks.Count; i++)
                {
                    if (_attacks[i] != null) return _attacks[i];
                }
                return null;
            }
        }

        /// 모든 공격을 쉬지 않고 쓸 수 있다고 봤을 때의 초당 피해.
        /// 공격이 여럿이면 '공격력' 하나로 말할 수가 없어서 밸런스 표는 이쪽을 본다.
        public float Dps
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _attacks.Count; i++)
                {
                    if (_attacks[i] == null) continue;
                    total += _attacks[i].Damage / _attacks[i].Cooldown;
                }
                return total;
            }
        }

        /// 공격들이 요구하는 최소 사거리 중 가장 먼 값. 플레이어가 이보다 가까이 붙으면 물러난다.
        /// 따로 수치를 두지 않는 이유: "안 쏘는 거리"와 "물러나는 거리"가 어긋나면
        /// 쏘지도 물러나지도 않고 멍하니 서 있는 구간이 생긴다.
        public float RetreatRange
        {
            get
            {
                float best = 0f;
                for (int i = 0; i < _attacks.Count; i++)
                {
                    if (_attacks[i] == null) continue;
                    best = Mathf.Max(best, _attacks[i].MinRange);
                }
                return best;
            }
        }

        /// 가장 먼 공격의 사거리. AI가 '어디까지 다가가야 하는지'를 여기서 묻는다 —
        /// 공격마다 사거리가 다르므로 접근 거리를 수치로 따로 들면 둘이 어긋난다.
        public float MaxAttackRange
        {
            get
            {
                float best = 0f;
                for (int i = 0; i < _attacks.Count; i++)
                {
                    if (_attacks[i] == null) continue;
                    best = Mathf.Max(best, _attacks[i].Range);
                }
                return best;
            }
        }
    }
}
