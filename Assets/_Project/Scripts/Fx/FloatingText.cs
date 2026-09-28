using TMPro;
using UnityEngine;

namespace ForagerCP
{
    /// 맞은 자리에 튀어오르며 사라지는 숫자/문구. 데미지, 획득, 골드 전부 이걸 쓴다.
    ///
    /// 초당 수십 개가 뜨고 사라지므로 풀에서 꺼내 쓴다 — Instantiate/Destroy로 돌리면 GC가 튄다.
    /// 재사용되니 상태 초기화는 Awake가 아니라 OnSpawnedFromPool에서 한다.
    public class FloatingText : MonoBehaviour, IPooled
    {
        [SerializeField] TMP_Text _label;
        [SerializeField] CanvasGroup _group;

        [SerializeField] float _lifetime = 0.8f;
        [SerializeField] float _riseDistance = 60f;
        [SerializeField] Vector2 _horizontalJitter = new Vector2(-18f, 18f);
        [SerializeField] Vector3 _worldOffset = new Vector3(0f, 1.2f, 0f);

        RectTransform _rect;
        Camera _camera;
        Vector3 _worldAnchor;
        Vector2 _screenOffset;
        float _endTime;

        void Awake()
        {
            _rect = (RectTransform)transform;
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_label == null) _label = GetComponentInChildren<TMP_Text>(true);
        }

        /// 월드 지점에 띄운다. 화면 좌표로 따라가므로 카메라가 움직여도 자리를 지킨다.
        public void Show(Vector3 worldPosition, string text, Color color, float scale, Camera viewCamera)
        {
            _camera = viewCamera != null ? viewCamera : Camera.main;
            _worldAnchor = worldPosition + _worldOffset;

            // 같은 자리에 여러 개가 겹쳐 하나처럼 보이지 않게 좌우로 흩는다.
            _screenOffset = new Vector2(Random.Range(_horizontalJitter.x, _horizontalJitter.y), 0f);

            if (_label != null)
            {
                _label.text = text;
                _label.color = color;
            }

            _rect.localScale = Vector3.one * scale;
            _endTime = Time.time + _lifetime;
            if (_group != null) _group.alpha = 1f;

            UpdatePosition(0f);
        }

        void LateUpdate()
        {
            float remaining = _endTime - Time.time;
            if (remaining <= 0f)
            {
                PoolManager.Despawn(gameObject);
                return;
            }

            float progress = 1f - Mathf.Clamp01(remaining / Mathf.Max(0.01f, _lifetime));
            UpdatePosition(progress);

            // 끝 30%에서만 사라지게 — 처음부터 흐려지면 숫자가 안 읽힌다.
            if (_group != null) _group.alpha = Mathf.InverseLerp(1f, 0.7f, progress);
        }

        void UpdatePosition(float progress)
        {
            if (_camera == null) return;

            Vector3 screen = _camera.WorldToScreenPoint(_worldAnchor);
            if (screen.z < 0f)
            {
                if (_group != null) _group.alpha = 0f;
                return;
            }

            // 위로 갈수록 느려지게(감속) — 튀어오르는 느낌.
            float rise = _riseDistance * (1f - (1f - progress) * (1f - progress));
            _rect.position = new Vector3(screen.x + _screenOffset.x, screen.y + rise, 0f);
        }

        public void OnSpawnedFromPool()
        {
            _endTime = Time.time;
            if (_group != null) _group.alpha = 0f;
        }

        public void OnReturnedToPool()
        {
            _camera = null;
            if (_group != null) _group.alpha = 0f;
        }
    }
}
