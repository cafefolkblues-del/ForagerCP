using UnityEngine;

namespace ForagerCP
{
    /// 인벤토리에 들어갈 수 있는 모든 것의 공통 정의.
    /// 광물뿐 아니라 확장 2-3의 제작 재료·장비도 이걸 상속해서 쓴다.
    [CreateAssetMenu(menuName = "ForagerCP/아이템", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] string _displayName;

        /// 인벤 탭 오른쪽 쪽에 뜨는 설명. 비워두면 설명 없이 이름만 보인다.
        [SerializeField, TextArea] string _description;

        [SerializeField] Sprite _icon;
        [SerializeField] int _maxStack = 99;
        [SerializeField] int _goldValue = 10;
        [SerializeField] int _sortOrder;

        /// 변환기에 넘길 수 있는지. 특수·엔딩 광물은 꺼둔다.
        /// 기획이 '등급'이라 했는데 등급 목록이 아직 없어 우선 둘로만 가른다 — 목록이 오면 그때 올린다.
        [SerializeField] bool _sellable = true;

        /// 아이콘이 아직 없을 때 인벤토리 칸에 쓰는 대표색.
        /// 종류마다 고정값이어야 한다 — 획득 순서로 색을 배정하면 먼저 캔 광물이 남의 색을 뒤집어쓴다(팀 리뷰).
        [SerializeField] Color _uiColor = new Color(0.78f, 0.80f, 0.85f);

        /// 바닥에 떨어질 때 쓸 모양. 비워두면 DropService의 기본 프리팹을 쓴다.
        /// 아이콘·색과 같은 이유로 여기 둔다 — 종류가 자기 겉모습을 들고 있어야 아트 교체가 에셋 수정으로 끝난다.
        [SerializeField] GameObject _dropPrefab;

        /// 설치했을 때 씬에 놓일 것. 비워두면 이 아이템은 설치 대상이 아니다
        /// (무기·재료처럼 놓을 수 없는 것에 억지로 모양을 만들지 않으려고 없음을 기본으로 둔다).
        [SerializeField] GameObject _placePrefab;

        /// 한 구역에 놓을 수 있는 개수. 0이면 제한 없음(블럭 대부분).
        /// 보관함이 '구역당 1개'라서 생긴 값인데, 종류가 아니라 수치로 둔 것은
        /// 같은 제약이 다른 설치물에 또 오면 필드 하나로 끝나기 때문이다.
        [SerializeField] int _maxPerRegion;

        /// 이름을 비워두면 에셋 파일명을 쓴다 — 디자이너가 한 군데만 고쳐도 되게.
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public string Description => _description;

        public Sprite Icon => _icon;
        public int MaxStack => Mathf.Max(1, _maxStack);

        /// 변환기에 넘겼을 때 개당 받는 골드.
        public int GoldValue => _goldValue;

        public int SortOrder => _sortOrder;
        public bool Sellable => _sellable;
        public Color UiColor => _uiColor;
        public GameObject DropPrefab => _dropPrefab;
        public GameObject PlacePrefab => _placePrefab;
        public bool CanPlace => _placePrefab != null;

        public int MaxPerRegion => Mathf.Max(0, _maxPerRegion);
        public bool HasRegionLimit => _maxPerRegion > 0;
    }
}
