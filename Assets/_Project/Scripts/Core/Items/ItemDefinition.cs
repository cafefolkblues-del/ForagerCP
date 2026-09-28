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

        /// 아이콘이 아직 없을 때 인벤토리 칸에 쓰는 대표색.
        /// 종류마다 고정값이어야 한다 — 획득 순서로 색을 배정하면 먼저 캔 광물이 남의 색을 뒤집어쓴다(팀 리뷰).
        [SerializeField] Color _uiColor = new Color(0.78f, 0.80f, 0.85f);

        /// 바닥에 떨어질 때 쓸 모양. 비워두면 DropService의 기본 프리팹을 쓴다.
        /// 아이콘·색과 같은 이유로 여기 둔다 — 종류가 자기 겉모습을 들고 있어야 아트 교체가 에셋 수정으로 끝난다.
        [SerializeField] GameObject _dropPrefab;

        /// 이름을 비워두면 에셋 파일명을 쓴다 — 디자이너가 한 군데만 고쳐도 되게.
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;

        public Sprite Icon => _icon;
        public int MaxStack => Mathf.Max(1, _maxStack);

        /// 변환기에 넘겼을 때 개당 받는 골드.
        public int GoldValue => _goldValue;

        public int SortOrder => _sortOrder;
        public Color UiColor => _uiColor;
        public GameObject DropPrefab => _dropPrefab;
    }
}
