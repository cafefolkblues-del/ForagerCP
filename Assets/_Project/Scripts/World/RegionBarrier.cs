using UnityEngine;

namespace ForagerCP
{
    /// 지역을 막는 벽 또는 문. 자기가 지키는 지역이 열리면 스스로 사라진다.
    ///
    /// 벽으로 쓸지 문으로 쓸지는 디자이너가 배치로 정한다 — 지역 둘레를 코드가 자동으로 두르면
    /// 절차적 생성이 되고, 디자이너가 통로를 설계할 여지가 없어진다.
    ///
    /// 문으로 쓸 때는 F로 열 수 있다. 벽으로만 쓸 거면 상호작용을 꺼두면 된다.
    public class RegionBarrier : MonoBehaviour, IInteractable
    {
        [SerializeField] RegionDefinition _region;
        [SerializeField] RegionAccess _access;

        /// F로 열 수 있는 문인지. 끄면 조건이 채워질 때까지 손댈 수 없는 벽이 된다.
        [SerializeField] bool _openableByHand = true;

        public Transform Transform => transform;

        Renderer[] _renderers;
        Collider[] _colliders;

        public string Label
        {
            get
            {
                if (_region == null || _access == null) return string.Empty;
                if (!_openableByHand) return string.Empty;

                // 조건이 자기를 설명하므로 여기엔 조건 종류별 분기가 없다.
                return _access.CanEnter(_region, out RegionRequirement blocking)
                    ? $"{_region.DisplayName} 열기"
                    : blocking != null ? blocking.Describe() : $"{_region.DisplayName} 잠김";
            }
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
            if (!_openableByHand || _region == null || _access == null) return;

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
