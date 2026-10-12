using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 상점에 올라가는 품목 하나. 새 품목 = 이 에셋 하나 추가, 코드 수정 없음.
    ///
    /// 산 적 있는지는 여기 담지 않는다 — 그건 런타임 상태라 에셋에 넣으면
    /// 에디터에서 산 것이 저장돼 다음 플레이에 그대로 남는다(지역·스킬에서 겪은 것과 같다).
    [CreateAssetMenu(menuName = "ForagerCP/상점 품목", fileName = "Good")]
    public class ShopGood : ScriptableObject
    {
        [SerializeField] string _displayName;
        [SerializeField] Sprite _icon;
        [SerializeField] Color _uiColor = new Color(0.78f, 0.80f, 0.85f);

        /// 목록에서 묶어 보여줄 분류. 지금은 표시용이고, 상점이 늘면 걸러내는 데도 쓴다.
        [SerializeField] string _category = "장비";

        [SerializeField] int _price = 100;

        /// 한 번만 살 수 있는지. 인벤 확장처럼 횟수가 정해진 것이 이쪽.
        [SerializeField] bool _oneTime;

        /// 이것을 먼저 사야 올라온다. 인벤 확장 2단계처럼 순서가 있는 품목에 쓴다.
        [SerializeField] ShopGood _requires;

        [SerializeField] List<ShopEffect> _effects = new List<ShopEffect>();

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public Sprite Icon => _icon;
        public Color UiColor => _uiColor;
        public string Category => _category;
        public int Price => Mathf.Max(0, _price);
        public bool OneTime => _oneTime;
        public ShopGood Requires => _requires;
        public IReadOnlyList<ShopEffect> Effects => _effects;

        public string Describe()
        {
            var parts = new List<string>();
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] != null) parts.Add(_effects[i].Describe());
            }
            return parts.Count == 0 ? "효과 없음" : string.Join(" · ", parts);
        }

        /// 효과 중 하나라도 막으면 못 산다.
        public bool CanBuy(ShopContext context, out string reason)
        {
            reason = null;
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] == null) continue;
                if (!_effects[i].CanBuy(context, out reason)) return false;
            }
            return true;
        }

        public void Apply(ShopContext context)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] != null) _effects[i].Apply(context);
            }
        }
    }
}
