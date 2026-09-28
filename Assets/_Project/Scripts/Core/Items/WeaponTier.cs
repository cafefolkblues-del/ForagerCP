using UnityEngine;

namespace ForagerCP
{
    /// 검 한 티어의 수치. 나무 → 돌 → 철 → 다이아.
    /// 확정 스펙: 티어가 올라도 사거리·속도는 그대로고 데미지만 오른다.
    /// 채집력(HarvestPower)과는 별 트랙이라 서로 영향을 주지 않는다.
    [CreateAssetMenu(menuName = "ForagerCP/무기 티어", fileName = "Weapon")]
    public class WeaponTier : ScriptableObject
    {
        [SerializeField] string _displayName;
        [SerializeField] int _damage = 4;

        /// 상점 판매가. 티어 순서대로 팔린다.
        [SerializeField] int _price = 0;
        [SerializeField] int _order;

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public int Damage => Mathf.Max(1, _damage);
        public int Price => Mathf.Max(0, _price);
        public int Order => _order;
    }
}
