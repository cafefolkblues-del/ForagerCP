using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 하트로 표시하는 체력. 하트 하나가 _unitsPerHeart만큼을 맡고, 반칸까지 표현한다.
    ///
    /// 체력 값 자체는 int 그대로 두고 여기서만 나눈다 — 데미지 계산을 실수로 바꾸면
    /// 반칸 피해가 부동소수 오차로 어긋난다.
    ///
    /// 하트는 꽉 참 / 반 / 빔 세 장의 그림을 갈아 끼운다. 반 하트 그림이 왼쪽 반만 칠한 모양이라
    /// 채움 비율로 자르면 그림과 어긋난다.
    public class HeartBarView : MonoBehaviour
    {
        [SerializeField] PlayerHealth _health;
        [SerializeField] RectTransform _container;
        [SerializeField] Image _heartTemplate;

        [SerializeField] int _unitsPerHeart = 20;
        [SerializeField] Sprite _fullSprite;
        [SerializeField] Sprite _halfSprite;
        [SerializeField] Sprite _emptySprite;

        readonly List<Image> _hearts = new List<Image>();

        void Awake()
        {
            if (_health == null) _health = FindFirstObjectByType<PlayerHealth>();
            if (_heartTemplate != null) _heartTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (_health == null) return;

            _health.Changed += Redraw;
            Redraw(_health);
        }

        void OnDisable()
        {
            if (_health == null) return;
            _health.Changed -= Redraw;
        }

        void Redraw(PlayerHealth health)
        {
            int units = Mathf.Max(1, _unitsPerHeart);
            int heartCount = Mathf.CeilToInt((float)health.MaxHp / units);
            EnsureHearts(heartCount);

            for (int i = 0; i < _hearts.Count; i++)
            {
                bool used = i < heartCount;
                _hearts[i].gameObject.SetActive(used);
                if (!used) continue;

                // 이 하트가 담당하는 구간에서 남은 체력이 몇 유닛인지.
                int remaining = Mathf.Clamp(health.CurrentHp - i * units, 0, units);
                _hearts[i].sprite = SpriteFor(remaining, units);
            }
        }

        /// 조금이라도 남았으면 반 하트. 반 미만을 빈 하트로 그리면 살아 있는데 빈 하트만 보이는 순간이 생긴다.
        Sprite SpriteFor(int remaining, int units)
        {
            if (remaining >= units) return _fullSprite;
            return remaining > 0 ? _halfSprite : _emptySprite;
        }

        void EnsureHearts(int wanted)
        {
            if (_heartTemplate == null || _container == null) return;

            while (_hearts.Count < wanted)
            {
                Image heart = Instantiate(_heartTemplate, _container);
                heart.gameObject.SetActive(true);
                heart.name = "Heart_" + _hearts.Count;
                _hearts.Add(heart);
            }
        }
    }
}
