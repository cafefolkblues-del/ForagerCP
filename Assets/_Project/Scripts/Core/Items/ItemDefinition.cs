using UnityEngine;

namespace ForagerCP
{
    /// 인벤토리에 들어갈 수 있는 모든 것의 공통 정의.
    /// 광물뿐 아니라 확장 2-3의 제작 재료·장비도 이걸 상속해서 쓴다.
    [CreateAssetMenu(menuName = "ForagerCP/아이템", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] string _displayName;
        [SerializeField] Sprite _icon;
        [SerializeField] int _maxStack = 99;
        [SerializeField] int _goldValue = 10;
        [SerializeField] int _sortOrder;

        /// 이름을 비워두면 에셋 파일명을 쓴다 — 디자이너가 한 군데만 고쳐도 되게.
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;

        public Sprite Icon => _icon;
        public int MaxStack => Mathf.Max(1, _maxStack);

        /// 변환기에 넘겼을 때 개당 받는 골드.
        public int GoldValue => _goldValue;

        public int SortOrder => _sortOrder;
    }
}
