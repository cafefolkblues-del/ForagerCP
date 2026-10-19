using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 차지가 얼마나 찼는지 보여주는 막대.
    ///
    /// 이게 없던 동안 플레이어는 차지가 되는지조차 몰랐다. 실제로는 광물을 캐는 동안
    /// 차지가 안 되는 것이 맞는데(채집과 차지는 서로 배타적이다), 보이지 않으니
    /// "캘 때도 충전되는 것 같다"는 오해가 나왔다(3차 프로토타입 피드백 8번).
    ///
    /// 값은 PlayerAttacker가 이미 쏘고 있었다 — 듣는 쪽이 없었을 뿐이다.
    public class ChargeBarView : MonoBehaviour
    {
        [SerializeField] PlayerAttacker _attacker;
        [SerializeField] CanvasGroup _group;

        /// 0~1을 가로 길이로 바꿀 채움. Image.fillAmount 대신 앵커를 쓰는 이유는
        /// 9분할 스프라이트에서 fillAmount가 끝 조각을 잘라먹기 때문.
        [SerializeField] RectTransform _fill;

        [SerializeField] Image _fillImage;

        [SerializeField] Color _charging = new Color(0.96f, 0.78f, 0.30f);

        /// 다 찼을 때 색. 언제 놓아야 하는지가 색으로 먼저 보인다.
        [SerializeField] Color _full = new Color(1f, 0.95f, 0.55f);

        [SerializeField] float _fadeDuration = 0.12f;

        float _ratio;

        void Awake()
        {
            if (_attacker == null) _attacker = FindFirstObjectByType<PlayerAttacker>();
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_group != null) _group.alpha = 0f;

            Apply(0f);
        }

        void OnEnable()
        {
            if (_attacker != null) _attacker.ChargeRatioChanged += Apply;
        }

        void OnDisable()
        {
            if (_attacker != null) _attacker.ChargeRatioChanged -= Apply;
        }

        void Apply(float ratio)
        {
            _ratio = Mathf.Clamp01(ratio);

            if (_fill != null)
            {
                // 가로만 줄인다. 세로는 틀에 맞춰 늘려둔 채로 둔다.
                _fill.anchorMin = new Vector2(0f, _fill.anchorMin.y);
                _fill.anchorMax = new Vector2(_ratio, _fill.anchorMax.y);
            }

            if (_fillImage != null) _fillImage.color = _ratio >= 1f ? _full : _charging;
        }

        void Update()
        {
            bool charging = _attacker != null && _attacker.IsCharging;

            // 차지가 끝나면 여기서 직접 0으로 되돌린다. 이벤트에만 기대면 안 된다 —
            // PlayerAttacker는 '차지 중이었을 때만' 0을 쏘기 때문에, 다른 경로로
            // 차지가 풀리면 막대가 찬 채로 남고 다음 차지가 꽉 찬 모습으로 시작한다.
            if (!charging && _ratio > 0f) Apply(0f);

            if (_group == null) return;

            // 차지 중일 때만 보인다. 늘 띄워두면 평소 화면이 지저분해지고,
            // 막대가 하나 더 있는 것처럼 보여 체력과 헷갈린다.
            float target = charging ? 1f : 0f;
            if (Mathf.Approximately(_group.alpha, target)) return;

            // 창 연출과 같은 이유로 게임 속도와 무관하게 움직인다.
            float step = _fadeDuration <= 0f ? 1f : Time.unscaledDeltaTime / _fadeDuration;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, step);
        }
    }
}
