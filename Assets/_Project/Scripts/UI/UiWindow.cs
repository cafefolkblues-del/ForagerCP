using System;
using UnityEngine;

namespace ForagerCP
{
    /// 열고 닫히는 창의 공통 동작. CanvasGroup으로 페이드하며,
    /// SetActive 대신 알파/차단만 끄는 이유는 켜질 때마다 하위 컴포넌트가 재초기화되는 걸 피하기 위해서다.
    [RequireComponent(typeof(CanvasGroup))]
    public class UiWindow : MonoBehaviour
    {
        [SerializeField] bool _openOnStart;
        [SerializeField] float _fadeDuration = 0.12f;

        /// 열릴 때 살짝 커지며 나타나는 연출. 1이면 연출 없음.
        [SerializeField] float _popScale = 0.96f;

        public event Action<UiWindow> OpenedChanged;

        public bool IsOpen { get; private set; }

        CanvasGroup _group;

        /// 에디터 스크립트가 Awake 전에 부를 수 있어서 지연 조회한다(편집 모드에선 Awake가 안 돈다).
        CanvasGroup Group => _group != null ? _group : (_group = GetComponent<CanvasGroup>());
        RectTransform _rect;
        float _velocity;

        void Awake()
        {
            _rect = (RectTransform)transform;
            SetOpenImmediate(_openOnStart);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            OpenedChanged?.Invoke(this);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            OpenedChanged?.Invoke(this);
        }

        public void SetOpenImmediate(bool open)
        {
            IsOpen = open;
            Group.alpha = open ? 1f : 0f;
            Group.blocksRaycasts = open;
            Group.interactable = open;
            if (_rect != null) _rect.localScale = Vector3.one;
        }

        void Update()
        {
            float target = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(Group.alpha, target)) return;

            // 창 연출은 게임 속도와 무관해야 한다(일시정지나 슬로모션에서도 즉각 반응).
            float step = _fadeDuration <= 0f ? 1f : Time.unscaledDeltaTime / _fadeDuration;
            Group.alpha = Mathf.MoveTowards(Group.alpha, target, step);

            Group.blocksRaycasts = IsOpen;
            Group.interactable = IsOpen;

            if (_rect == null || Mathf.Approximately(_popScale, 1f)) return;
            float scale = Mathf.Lerp(_popScale, 1f, Group.alpha);
            _rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
