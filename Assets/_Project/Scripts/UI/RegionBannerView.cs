using System.Collections;
using TMPro;
using UnityEngine;

namespace ForagerCP
{
    /// 지역이 바뀌면 이름을 잠깐 띄우고, 막혔을 때는 이유를 띄운다.
    ///
    /// 구획은 눈에 안 보이는 데이터라 플레이어는 자기가 어디에 있는지도, 왜 못 가는지도 모른다.
    /// 문구를 만드는 건 조건 쪽이고(Describe), 여기는 띄우기만 한다.
    public class RegionBannerView : MonoBehaviour
    {
        [SerializeField] RegionTracker _tracker;
        [SerializeField] RegionAccess _access;

        /// 설치 거부 중 '구역 규칙' 때문인 것만 여기서 띄운다. 이 줄은 결국
        /// '한 줄 알림'이고, 알림 띄우는 자리를 둘로 만들면 둘이 겹쳐 뜬다.
        [SerializeField] BlockPlacer _placer;
        [SerializeField] TMP_Text _label;
        [SerializeField] CanvasGroup _group;

        [SerializeField] float _holdTime = 1.6f;
        [SerializeField] float _fadeTime = 0.5f;

        [SerializeField] Color _regionColor = new Color(0.94f, 0.95f, 0.98f);
        [SerializeField] Color _refusedColor = new Color(1f, 0.62f, 0.55f);

        Coroutine _routine;

        void Awake()
        {
            if (_tracker == null) _tracker = FindFirstObjectByType<RegionTracker>();
            if (_access == null) _access = FindFirstObjectByType<RegionAccess>();
            if (_placer == null) _placer = FindFirstObjectByType<BlockPlacer>();
            if (_group != null) _group.alpha = 0f;
        }

        void OnEnable()
        {
            if (_tracker != null)
            {
                _tracker.Changed += OnRegionChanged;
                _tracker.PushedBack += OnPushedBack;
            }

            if (_placer != null) _placer.Rejected += OnPlaceRejected;

            if (_access == null) return;

            _access.Refused += OnRefused;
            _access.Unlocked += OnUnlocked;
        }

        void OnDisable()
        {
            if (_tracker != null)
            {
                _tracker.Changed -= OnRegionChanged;
                _tracker.PushedBack -= OnPushedBack;
            }

            if (_placer != null) _placer.Rejected -= OnPlaceRejected;

            if (_access == null) return;

            _access.Refused -= OnRefused;
            _access.Unlocked -= OnUnlocked;
        }

        /// 거부 사유 대부분은 미리보기가 빨개져서 보인다. 구역 규칙만은 눈에 안 보여 글로 알린다.
        void OnPlaceRejected(ItemDefinition item, PlaceResult result)
        {
            string what = item != null ? item.DisplayName : "그것";

            if (result == PlaceResult.RegionLimit) Show($"이 구역엔 {what}이 이미 있습니다", _refusedColor);
            else if (result == PlaceResult.NoRegion) Show($"{what}은 구역 안에만 놓을 수 있습니다", _refusedColor);
        }

        void OnRegionChanged(RegionDefinition region)
        {
            // 구획하지 않은 땅은 알릴 것이 없다.
            if (region == null) return;
            Show(region.DisplayName, _regionColor);
        }

        void OnUnlocked(RegionDefinition region) => Show($"{region.DisplayName} 개방", _regionColor);

        void OnRefused(RegionDefinition region, RegionRequirement blocking)
            => Show(blocking != null ? blocking.Describe() : $"{region.DisplayName} 잠김", _refusedColor);

        void OnPushedBack(RegionDefinition region)
            => Show(region != null ? $"{region.DisplayName}: 아직 갈 수 없습니다" : "아직 갈 수 없습니다", _refusedColor);

        void Show(string text, Color color)
        {
            if (_label == null || _group == null) return;

            _label.text = text;
            _label.color = color;

            // 연달아 뜨면 앞의 것을 끊는다. 안 끊으면 두 코루틴이 같은 알파를 서로 밀고 당긴다.
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine());
        }

        IEnumerator ShowRoutine()
        {
            _group.alpha = 1f;
            yield return new WaitForSeconds(_holdTime);

            for (float t = 0f; t < _fadeTime; t += Time.deltaTime)
            {
                _group.alpha = 1f - t / _fadeTime;
                yield return null;
            }

            _group.alpha = 0f;
            _routine = null;
        }
    }
}
