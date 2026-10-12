using UnityEngine;

namespace ForagerCP
{
    /// 경계에 놓이는 것의 종류. 손댈 수 있는지가 갈린다.
    public enum BarrierKind
    {
        /// 막혀 있기만 하다. 조건이 채워져야 사라진다.
        Wall,

        /// F로 여는 문. 대가가 있는 조건(골드 지불 등)은 여기서만 치를 수 있다.
        Gate,
    }

    /// 지역을 막는 벽 또는 문. 자기가 지키는 지역이 열리면 스스로 사라진다.
    ///
    /// 보통은 지역 편집 창이 구획 경계를 따라 자동으로 두르지만, 나온 결과는 그냥 씬 오브젝트라
    /// 디자이너가 옮기고 지우고 바꿀 수 있다. 실행 중에 생기는 것은 하나도 없다.
    public class RegionBarrier : MonoBehaviour, IInteractable
    {
        [SerializeField] RegionDefinition _region;
        [SerializeField] RegionAccess _access;

        /// 벽이면 손댈 수 없고, 문이면 F로 연다.
        /// 경계를 전부 문으로 두면 아무 데서나 열려 '통로'라는 감각이 사라진다.
        [SerializeField] BarrierKind _kind = BarrierKind.Wall;

        public Transform Transform => transform;
        public BarrierKind Kind => _kind;
        public RegionDefinition Region => _region;

        Renderer[] _renderers;
        Collider[] _colliders;

        public string Label
        {
            get
            {
                if (_region == null || _access == null) return string.Empty;
                if (_kind != BarrierKind.Gate) return string.Empty;

                // 조건이 자기를 설명하므로 여기엔 조건 종류별 분기가 없다.
                return _access.CanEnter(_region, out RegionRequirement blocking)
                    ? $"{_region.DisplayName} 열기"
                    : blocking != null ? blocking.Describe() : $"{_region.DisplayName} 잠김";
            }
        }

        /// 생성 도구가 쓴다. 경계를 다시 두를 때마다 인스펙터를 거치지 않도록.
        public void Setup(RegionDefinition region, RegionAccess access, BarrierKind kind)
        {
            _region = region;
            _access = access;
            _kind = kind;
        }

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider>(true);
            if (_access == null) _access = FindFirstObjectByType<RegionAccess>();
        }

        void OnEnable()
        {
            if (_access == null) return;

            _access.Unlocked += OnUnlocked;
            Refresh();
        }

        void OnDisable()
        {
            if (_access == null) return;
            _access.Unlocked -= OnUnlocked;
        }

        public void Interact()
        {
            if (_kind != BarrierKind.Gate || _region == null || _access == null) return;

            // 열지 못하면 TryUnlock이 Refused를 쏜다. 안내는 그쪽을 구독한 UI가 한다.
            _access.TryUnlock(_region);
        }

        void OnUnlocked(RegionDefinition region)
        {
            if (region != _region) return;
            Refresh();
        }

        void Refresh()
        {
            bool blocking = _region != null && !_access.IsUnlocked(_region);
            SetPresence(blocking);
        }

        /// MineralNode와 같은 이유로 GameObject를 끄지 않고 렌더러·콜라이더만 끈다.
        /// 꺼버리면 다시 잠가야 할 때(지역을 되돌리는 기획이 오면) 이 컴포넌트가 깨어나지 못한다.
        void SetPresence(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].enabled = visible;
            for (int i = 0; i < _colliders.Length; i++) _colliders[i].enabled = visible;
        }
    }
}
