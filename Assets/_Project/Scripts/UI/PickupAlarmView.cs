using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 오른쪽 아래 획득 알림. 드랍을 주우면 [아이콘] 이름 +개수 한 줄이 떴다가 사라진다.
    ///
    /// 같은 것을 연달아 주우면 줄을 새로 만들지 않고 개수만 올린다 — 광물 한 무더기를 쓸어 담을 때
    /// "철 +1"이 열 줄 쌓이면 다른 획득이 화면 밖으로 밀려난다.
    /// 줄은 미리 만들어둔 것을 돌려 쓴다. 채집마다 Instantiate하면 쓰레기가 계속 나온다.
    public class PickupAlarmView : MonoBehaviour
    {
        [SerializeField] DropService _drops;
        [SerializeField] RectTransform _container;
        [SerializeField] RectTransform _entryTemplate;

        [SerializeField] int _maxEntries = 4;
        [SerializeField] float _showTime = 2.5f;
        [SerializeField] float _fadeTime = 0.3f;

        class Entry
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Icon;
            public TMP_Text Label;
            public ItemDefinition Item;
            public int Count;
            public float Remaining;
        }

        readonly List<Entry> _entries = new List<Entry>();

        /// 가장 최근에 띄운 줄. 같은 아이템이 이어 들어오면 여기에 더한다.
        Entry _latest;

        void Awake()
        {
            if (_drops == null) _drops = FindFirstObjectByType<DropService>();
            if (_entryTemplate != null) _entryTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (_drops != null) _drops.Picked += OnPicked;
        }

        void OnDisable()
        {
            if (_drops != null) _drops.Picked -= OnPicked;
        }

        void OnPicked(ItemDefinition item, int amount, Vector3 _)
        {
            if (item == null || amount <= 0) return;

            Entry entry = _latest != null && _latest.Item == item && _latest.Remaining > 0f ? _latest : Take();
            if (entry == null) return;

            if (entry != _latest)
            {
                entry.Item = item;
                entry.Count = 0;
                entry.Icon.sprite = item.Icon;
                entry.Icon.color = item.Icon != null ? Color.white : item.UiColor;

                // 새 줄은 맨 아래로. 레이아웃이 아래에서 위로 쌓으므로 최신이 늘 같은 자리에 뜬다.
                entry.Root.SetAsLastSibling();
                entry.Root.gameObject.SetActive(true);
                _latest = entry;
            }

            entry.Count += amount;
            entry.Label.text = $"{item.DisplayName} +{entry.Count}";
            entry.Remaining = _showTime;
            entry.Group.alpha = 1f;
        }

        /// 빈 줄을 하나 고른다. 다 차 있으면 가장 먼저 사라질 줄을 빼앗는다.
        Entry Take()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Remaining <= 0f) return _entries[i];
            }

            if (_entries.Count < Mathf.Max(1, _maxEntries)) return Create();

            Entry oldest = _entries[0];
            for (int i = 1; i < _entries.Count; i++)
            {
                if (_entries[i].Remaining < oldest.Remaining) oldest = _entries[i];
            }
            return oldest;
        }

        Entry Create()
        {
            if (_entryTemplate == null || _container == null) return null;

            RectTransform root = Instantiate(_entryTemplate, _container);
            root.name = "Entry_" + _entries.Count;

            var entry = new Entry
            {
                Root = root,
                Group = root.GetComponent<CanvasGroup>(),
                Icon = root.Find("Icon").GetComponent<Image>(),
                Label = root.Find("Label").GetComponent<TMP_Text>(),
            };
            if (entry.Group == null) entry.Group = root.gameObject.AddComponent<CanvasGroup>();

            _entries.Add(entry);
            return entry;
        }

        void Update()
        {
            // 알림은 게임 속도와 무관하게 사라져야 한다(창을 열어 멈춘 사이에도 시계가 간다).
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry.Remaining <= 0f) continue;

                entry.Remaining -= dt;
                entry.Group.alpha = _fadeTime <= 0f ? 1f : Mathf.Clamp01(entry.Remaining / _fadeTime);

                if (entry.Remaining > 0f) continue;

                entry.Root.gameObject.SetActive(false);
                if (entry == _latest) _latest = null;
            }
        }
    }
}
