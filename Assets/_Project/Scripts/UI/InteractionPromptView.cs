using TMPro;
using UnityEngine;

namespace ForagerCP
{
    /// "F  광물 변환기" 같은 안내. PlayerInteractor가 잡은 대상이 바뀔 때만 갱신된다.
    public class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] PlayerInteractor _interactor;
        [SerializeField] CanvasGroup _group;
        [SerializeField] TMP_Text _label;
        [SerializeField] string _keyHint = "F";
        [SerializeField] float _fadeSpeed = 8f;

        float _targetAlpha;

        void Awake()
        {
            if (_interactor == null) _interactor = FindFirstObjectByType<PlayerInteractor>();
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_group != null) _group.alpha = 0f;
        }

        void OnEnable()
        {
            if (_interactor == null) return;

            _interactor.TargetChanged += OnTargetChanged;
            OnTargetChanged(_interactor.CurrentTarget);
        }

        void OnDisable()
        {
            if (_interactor == null) return;
            _interactor.TargetChanged -= OnTargetChanged;
        }

        void OnTargetChanged(IInteractable target)
        {
            _targetAlpha = target == null ? 0f : 1f;
            if (target == null || _label == null) return;

            _label.text = $"<b>{_keyHint}</b>   {target.Label}";
        }

        void Update()
        {
            if (_group == null) return;
            if (Mathf.Approximately(_group.alpha, _targetAlpha)) return;

            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
        }
    }
}
