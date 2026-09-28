using System;
using UnityEngine;

namespace ForagerCP
{
    /// 지금 들고 있는 검. 공격력은 여기서만 나온다 —
    /// PlayerAttacker에 숫자를 박아두면 상점에서 티어를 팔 때 코드를 고쳐야 한다.
    public class PlayerWeapon : MonoBehaviour
    {
        [SerializeField] WeaponTier _equipped;

        /// 티어 에셋을 아직 안 만들었거나 비워둔 경우의 최소 데미지.
        [SerializeField] int _fallbackDamage = 4;

        public event Action<PlayerWeapon> Changed;

        public WeaponTier Equipped => _equipped;
        public int Damage => _equipped != null ? _equipped.Damage : Mathf.Max(1, _fallbackDamage);
        public string DisplayName => _equipped != null ? _equipped.DisplayName : "맨손";

        /// 상점이 호출한다. 더 낮은 티어로 되돌리는 건 막지 않는다(기획이 정할 일).
        public void Equip(WeaponTier tier)
        {
            if (tier == null || tier == _equipped) return;

            _equipped = tier;
            Changed?.Invoke(this);
        }
    }
}
