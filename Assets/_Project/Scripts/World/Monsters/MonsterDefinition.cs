using UnityEngine;

namespace ForagerCP
{
    /// 몬스터 한 종류의 수치. 새 몬스터 = 이 에셋 + 프리팹 + 배치 팔레트 등록, 코드 수정 없음.
    [CreateAssetMenu(menuName = "ForagerCP/몬스터", fileName = "Monster")]
    public class MonsterDefinition : ScriptableObject
    {
        [SerializeField] string _displayName;

        [Header("전투")]
        [SerializeField] int _maxHp = 20;
        [SerializeField] int _attackPower = 2;
        [SerializeField] float _attackInterval = 1.2f;
        [SerializeField] float _attackRange = 1.2f;

        [Header("이동 / 인지")]
        [SerializeField] float _moveSpeed = 2.5f;
        [SerializeField] float _detectRange = 8f;

        /// 스폰 지점에서 이만큼 멀어지면 표적을 놓고 돌아간다.
        /// 확장 2-2의 구역 경계가 들어오면 이 거리 대신 구역 판정으로 교체된다.
        [SerializeField] float _leashDistance = 12f;

        [Header("처치 보상 / 재등장")]
        [SerializeField] int _expReward = 2;
        [SerializeField] float _respawnDelay = 15f;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public int MaxHp => Mathf.Max(1, _maxHp);
        public int AttackPower => Mathf.Max(0, _attackPower);
        public float AttackInterval => Mathf.Max(0.05f, _attackInterval);
        public float AttackRange => Mathf.Max(0.1f, _attackRange);
        public float MoveSpeed => Mathf.Max(0f, _moveSpeed);
        public float DetectRange => Mathf.Max(0f, _detectRange);
        public float LeashDistance => Mathf.Max(0f, _leashDistance);
        public int ExpReward => Mathf.Max(0, _expReward);
        public float RespawnDelay => Mathf.Max(0f, _respawnDelay);
    }
}
