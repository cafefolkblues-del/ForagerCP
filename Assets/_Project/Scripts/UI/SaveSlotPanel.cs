using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 세이브 슬롯 목록. 한 줄에 슬롯 하나씩 놓고 불러오기와 지우기를 붙인다.
    ///
    /// 줄은 저장 파일의 머리말(요약·시각)만 읽는다 — 목록을 보려고 전부 복원할 수는 없으므로
    /// SaveService.Peek이 머리말만 돌려준다.
    public class SaveSlotPanel : MonoBehaviour
    {
        [SerializeField] SaveService _service;
        [SerializeField] UiWindow _window;
        [SerializeField] UiRouter _router;

        [Header("줄")]
        [SerializeField] RectTransform _rowParent;
        [SerializeField] RectTransform _rowTemplate;

        [SerializeField] TMP_Text _notice;

        [Header("칸 색")]
        /// 슬롯마다 다른 색을 줘서 어느 줄을 보고 있는지 한눈에 알게 한다.
        [SerializeField] Color[] _slotColors =
        {
            new Color(0.55f, 0.80f, 0.98f),
            new Color(0.60f, 0.90f, 0.62f),
            new Color(0.95f, 0.75f, 0.45f),
        };

        class Row
        {
            public RectTransform Rect;
            public Image Icon;
            public TMP_Text Title;
            public TMP_Text Summary;
            public TMP_Text Stamp;
            public Button Main;
            public TMP_Text MainLabel;
            public Button Erase;
            public Image EraseImage;
        }

        readonly List<Row> _rows = new List<Row>();

        void Awake()
        {
            if (_service == null) _service = FindFirstObjectByType<SaveService>();
            if (_window == null) _window = GetComponent<UiWindow>();
            if (_router == null) _router = FindFirstObjectByType<UiRouter>();
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (_window != null) _window.OpenedChanged += OnOpenedChanged;
            if (_service == null) return;

            _service.Failed += OnFailed;

            // 창이 열려 있는 동안 자동 저장이 돌면 목록이 낡는다.
            _service.Saved += OnSaved;

            Build();
            Refresh();
        }

        void OnDisable()
        {
            if (_window != null) _window.OpenedChanged -= OnOpenedChanged;
            if (_service == null) return;

            _service.Failed -= OnFailed;
            _service.Saved -= OnSaved;
        }

        /// 열 때마다 다시 읽는다. 창이 닫힌 동안 자동 저장이 돌았을 수 있다.
        void OnOpenedChanged(UiWindow window)
        {
            if (!window.IsOpen) return;

            if (_notice != null) _notice.text = string.Empty;
            Refresh();
        }

        void OnSaved(int slot)
        {
            if (_window != null && _window.IsOpen) Refresh();
        }

        void Build()
        {
            if (_rows.Count > 0 || _service == null || _rowTemplate == null || _rowParent == null) return;

            for (int slot = 0; slot < _service.SlotCount; slot++)
            {
                RectTransform rect = Instantiate(_rowTemplate, _rowParent);
                rect.gameObject.SetActive(true);
                rect.name = "Slot" + slot;

                var row = new Row
                {
                    Rect = rect,
                    Icon = rect.Find("Icon").GetComponent<Image>(),
                    Title = rect.Find("Title").GetComponent<TMP_Text>(),
                    Summary = rect.Find("Summary").GetComponent<TMP_Text>(),
                    Stamp = rect.Find("Stamp").GetComponent<TMP_Text>(),
                    Main = rect.Find("Main").GetComponent<Button>(),
                    MainLabel = rect.Find("Main/Text").GetComponent<TMP_Text>(),
                    Erase = rect.Find("Erase").GetComponent<Button>(),
                    EraseImage = rect.Find("Erase").GetComponent<Image>(),
                };

                // 람다가 바깥 변수를 잡으면 마지막 슬롯만 가리킨다. 지역 변수로 복사해 고정한다.
                int captured = slot;
                row.Main.onClick.AddListener(() => OnMain(captured));
                row.Erase.onClick.AddListener(() => OnErase(captured));

                row.Icon.color = _slotColors.Length > 0 ? _slotColors[slot % _slotColors.Length] : Color.white;
                _rows.Add(row);
            }
        }

        void Refresh()
        {
            if (_service == null) return;

            for (int slot = 0; slot < _rows.Count; slot++)
            {
                Row row = _rows[slot];
                SaveDocument document = _service.Peek(slot);

                // 자동 저장이 어느 칸을 쓰는지 알려줘야 덮어쓰고 나서 놀라지 않는다.
                string auto = slot == _service.AutoSlot ? "  (자동 저장)" : string.Empty;
                row.Title.text = $"슬롯 {slot + 1}{auto}";

                if (document == null)
                {
                    row.Summary.text = "비어 있음";
                    row.Stamp.text = string.Empty;
                    row.MainLabel.text = "여기에 저장";
                    SetErase(row, false);
                    continue;
                }

                row.Summary.text = string.IsNullOrEmpty(document.Summary) ? "-" : document.Summary;
                row.Stamp.text = Stamp(document.SavedAtUtc);
                row.MainLabel.text = "불러오기";
                SetErase(row, true);
            }
        }

        /// 버튼이 꺼진 것을 색으로도 알린다. SpriteSwap은 꺼진 상태 그림을 따로 주지 않으면
        /// 눌리지 않는데도 똑같이 보인다.
        static void SetErase(Row row, bool on)
        {
            row.Erase.interactable = on;

            Color color = row.EraseImage.color;
            color.a = on ? 1f : 0.3f;
            row.EraseImage.color = color;
        }

        /// 저장은 UTC로 적힌다(기기 시간대가 바뀌어도 순서가 안 뒤집히게). 보여줄 때만 지역 시간으로.
        static string Stamp(string savedAtUtc)
        {
            if (!DateTime.TryParse(savedAtUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
            {
                return string.Empty;
            }

            return parsed.ToLocalTime().ToString("MM/dd HH:mm");
        }

        void OnMain(int slot)
        {
            if (_service.Exists(slot))
            {
                if (!_service.Load(slot)) return;

                Say($"슬롯 {slot + 1} 저장을 불러왔습니다.");

                // 불러온 뒤에는 창을 닫는다 — 바뀐 세계를 봐야 하므로.
                if (_router != null) _router.CloseAll();
                return;
            }

            if (!_service.Save(slot)) return;

            Say($"슬롯 {slot + 1}에 저장했습니다.");
            Refresh();
        }

        void OnErase(int slot)
        {
            _service.Delete(slot);

            Say($"슬롯 {slot + 1} 저장을 지웠습니다.");
            Refresh();
        }

        void OnFailed(string reason) => Say(reason);

        void Say(string text)
        {
            if (_notice != null) _notice.text = text;
        }
    }
}
