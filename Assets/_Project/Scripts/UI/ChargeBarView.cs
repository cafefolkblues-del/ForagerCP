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

        [Header("따라붙기")]
        /// 이 대상의 발밑에 붙는다. 비우면 화면 고정(둔 자리에 그대로).
        ///
        /// 화면 아래 고정에서 캐릭터 밑으로 옮긴 이유: 차지하는 동안 시선은 캐릭터와
        /// 커서 사이에 있다. 막대가 화면 끝에 있으면 얼마나 찼는지 보려고 눈을 떼야 한다.
        [SerializeField] Transform _follow;

        /// 대상 기준 월드 오프셋. 발밑에 깔리도록 살짝 아래로.
        [SerializeField] Vector3 _worldOffset = new Vector3(0f, 0.05f, -0.9f);

        [SerializeField] Camera _viewCamera;

        /// 월드→화면 좌표를 옮길 캔버스. 렌더 모드를 보려고 든다.
        [SerializeField] Canvas _canvas;

        float _ratio;
        RectTransform _rect;
        RectTransform _canvasRect;

        void Awake()
        {
            if (_attacker == null) _attacker = FindFirstObjectByType<PlayerAttacker>();
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_group != null) _group.alpha = 0f;

            _rect = (RectTransform)transform;
            if (_viewCamera == null) _viewCamera = Camera.main;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            // 부모 기준으로 잰다. anchoredPosition이 부모 좌표이기 때문 —
            // 루트 캔버스로 재면 부모가 화면 전체를 덮지 않을 때 자리가 어긋난다.
            _canvasRect = transform.parent as RectTransform;

            // 따라붙을 대상을 안 지정했으면 플레이어를 쓴다.
            if (_follow == null)
            {
                var player = FindFirstObjectByType<PlayerHealth>();
                if (player != null) _follow = player.transform;
            }

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

        /// 카메라가 움직인 뒤에 자리를 잡아야 한 프레임 늦게 따라오는 떨림이 없다.
        void LateUpdate()
        {
            if (_follow == null || _viewCamera == null || _rect == null || _canvasRect == null) return;

            // 보이지 않을 때는 계산하지 않는다. 늘 도는 코드라 아낄 값어치가 있다.
            if (_group != null && _group.alpha <= 0f) return;

            Vector3 screen = _viewCamera.WorldToScreenPoint(_follow.position + _worldOffset);

            // 카메라 뒤로 넘어가면 좌표가 뒤집힌다. 고정 탑다운이라 날 일은 없지만
            // 한 번 나면 막대가 화면 반대편에 붙어 원인을 찾기 어렵다.
            if (screen.z < 0f) return;

            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect, screen,
                    _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _viewCamera,
                    out local))
            {
                _rect.anchoredPosition = local;
            }
        }
    }
}
