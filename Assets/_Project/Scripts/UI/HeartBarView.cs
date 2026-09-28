using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 하트로 표시하는 체력. 확정 스펙: 1하트 = 2유닛, 반칸까지 표현.
    ///
    /// 체력 값 자체는 int 그대로 두고 여기서만 2로 나눈다 — 데미지 계산을 실수로 바꾸면
    /// 반칸 피해가 부동소수 오차로 어긋난다.
    /// 아트가 아직 없어 임시 도형으로 그린다. 스프라이트가 오면 _fullSprite/_emptySprite만 갈아끼우면 된다.
    public class HeartBarView : MonoBehaviour
    {
        [SerializeField] PlayerHealth _health;
        [SerializeField] RectTransform _container;
        [SerializeField] Image _heartTemplate;

        [SerializeField] int _unitsPerHeart = 2;
        [SerializeField] Color _fullColor = new Color(0.95f, 0.35f, 0.38f);
        [SerializeField] Color _emptyColor = new Color(1f, 1f, 1f, 0.12f);

        readonly List<Image> _hearts = new List<Image>();
        readonly List<Image> _fills = new List<Image>();

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
                _fills[i].fillAmount = (float)remaining / units;
            }
        }

        void EnsureHearts(int wanted)
        {
            if (_heartTemplate == null || _container == null) return;

            while (_hearts.Count < wanted)
            {
                Image heart = Instantiate(_heartTemplate, _container);
                heart.gameObject.SetActive(true);
                heart.name = "Heart_" + _hearts.Count;
                heart.color = _emptyColor;

                // 채움 이미지는 템플릿의 첫 자식을 쓴다. 없으면 만들어 붙인다.
                Image fill = heart.transform.childCount > 0 ? heart.transform.GetChild(0).GetComponent<Image>() : null;
                if (fill == null)
                {
                    var go = new GameObject("Fill", typeof(RectTransform));
                    go.transform.SetParent(heart.transform, false);
                    var rect = (RectTransform)go.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.sizeDelta = Vector2.zero;
                    fill = go.AddComponent<Image>();
                    fill.sprite = heart.sprite;
                }

                fill.color = _fullColor;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = 0;
                fill.raycastTarget = false;

                _hearts.Add(heart);
                _fills.Add(fill);
            }
        }
    }
}
