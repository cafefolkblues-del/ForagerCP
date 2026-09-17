using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 플레이어 체력바. 실제 값은 즉시 반영하고, 게이지만 뒤따라 줄어들게 해서 맞은 양이 눈에 보이게 한다.
    public class PlayerHealthBar : MonoBehaviour
    {
        [SerializeField] PlayerHealth _health;
        [SerializeField] Image _fill;

        /// 뒤늦게 따라오는 게이지(피해량 잔상). 없으면 그냥 안 쓴다.
        [SerializeField] Image _delayedFill;
        [SerializeField] TMP_Text _label;
        [SerializeField] float _delayedSpeed = 0.6f;

        [SerializeField] Color _healthyColor = new Color(0.45f, 0.85f, 0.5f);
        [SerializeField] Color _dangerColor = new Color(0.9f, 0.35f, 0.3f);
        [SerializeField, Range(0f, 1f)] float _dangerThreshold = 0.3f;

        float _target = 1f;

        void Awake()
        {
            if (_health == null) _health = FindFirstObjectByType<PlayerHealth>();
        }

        void OnEnable()
        {
            if (_health == null) return;

            _health.Changed += OnChanged;
            OnChanged(_health);
            if (_delayedFill != null) _delayedFill.fillAmount = _target;
        }

        void OnDisable()
        {
            if (_health == null) return;
            _health.Changed -= OnChanged;
        }

        void OnChanged(PlayerHealth health)
        {
            _target = health.MaxHp <= 0 ? 0f : (float)health.CurrentHp / health.MaxHp;

            if (_fill != null)
            {
                _fill.fillAmount = _target;
                _fill.color = _target <= _dangerThreshold ? _dangerColor : _healthyColor;
            }

            if (_label != null) _label.text = health.CurrentHp + " / " + health.MaxHp;

            // 회복은 잔상이 뒤에 남을 이유가 없으니 즉시 맞춘다.
            if (_delayedFill != null && _delayedFill.fillAmount < _target) _delayedFill.fillAmount = _target;
        }

        void Update()
        {
            if (_delayedFill == null) return;
            if (_delayedFill.fillAmount <= _target) return;

            _delayedFill.fillAmount = Mathf.MoveTowards(_delayedFill.fillAmount, _target, _delayedSpeed * Time.deltaTime);
        }
    }
}
