using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 몬스터 머리 위 체력 게이지. 화면 좌표로 따라붙는다(월드 캔버스를 쓰면 몬스터마다 캔버스가 생겨 비싸다).
    /// 풀에서 재사용되므로 상태 초기화는 Awake가 아니라 OnSpawnedFromPool에서 한다.
    public class MonsterHealthBar : MonoBehaviour, IPooled
    {
        [SerializeField] Image _fill;
        [SerializeField] CanvasGroup _group;
        [SerializeField] Vector3 _worldOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] float _fadeSpeed = 6f;

        Monster _monster;
        Camera _camera;
        RectTransform _rect;
        float _targetAlpha;

        void Awake()
        {
            _rect = (RectTransform)transform;
            if (_group == null) _group = GetComponent<CanvasGroup>();
        }

        public void Bind(Monster monster, Camera viewCamera)
        {
            Unbind();

            _monster = monster;
            _camera = viewCamera != null ? viewCamera : Camera.main;

            if (_monster == null) return;
            _monster.HealthChanged += OnHealthChanged;
            OnHealthChanged(_monster);
        }

        public void Unbind()
        {
            if (_monster == null) return;
            _monster.HealthChanged -= OnHealthChanged;
            _monster = null;
        }

        void OnHealthChanged(Monster monster)
        {
            if (monster.Definition == null) return;

            float ratio = Mathf.Clamp01((float)monster.CurrentHp / monster.Definition.MaxHp);
            if (_fill != null) _fill.fillAmount = ratio;

            // 멀쩡한 몬스터 머리 위까지 게이지가 깔리면 화면이 지저분해진다. 다치면 그때 보여준다.
            _targetAlpha = monster.IsAlive && ratio < 1f ? 1f : 0f;
        }

        void LateUpdate()
        {
            if (_monster == null || _camera == null) return;

            if (!_monster.IsAlive) _targetAlpha = 0f;

            Vector3 world = _monster.transform.position + _worldOffset;
            Vector3 screen = _camera.WorldToScreenPoint(world);

            // 카메라 뒤로 넘어가면 화면 반대편에 유령처럼 찍힌다.
            if (screen.z < 0f)
            {
                if (_group != null) _group.alpha = 0f;
                return;
            }

            _rect.position = screen;

            if (_group == null) return;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
        }

        public void OnSpawnedFromPool()
        {
            _targetAlpha = 0f;
            if (_group != null) _group.alpha = 0f;
            if (_fill != null) _fill.fillAmount = 1f;
        }

        public void OnReturnedToPool() => Unbind();
    }
}
