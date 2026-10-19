using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 판매 화면. 팔 수 있는 광물을 줄로 놓고, 줄마다 수량을 골라 판다.
    ///
    /// 장바구니를 두지 않는 이유: 기획이 요구한 것은 '종류별 수량 선택과 예상 골드'이고,
    /// 줄마다 바로 파는 것으로 충족된다. 합계·확정 단계를 넣으면 누르는 횟수만 늘어난다.
    public class SellPanelView : MonoBehaviour
    {
        [SerializeField] UiWindow _window;
        [SerializeField] UiRouter _router;
        [SerializeField] Inventory _inventory;
        [SerializeField] GoldWallet _gold;

        [SerializeField] RectTransform _rowParent;
        [SerializeField] RectTransform _rowTemplate;
        [SerializeField] TMP_Text _goldLabel;
        [SerializeField] TMP_Text _notice;

        class Row
        {
            public RectTransform Rect;
            public Image Icon;
            public TMP_Text Title;
            public TMP_Text Detail;
            public TMP_Text Amount;
            public Button Less;
            public Button More;
            public Button All;
            public Button Sell;
            public TMP_Text SellLabel;
        }

        readonly List<Row> _rows = new List<Row>();
        readonly List<ItemDefinition> _items = new List<ItemDefinition>();
        readonly List<ItemStack> _stacks = new List<ItemStack>();

        /// 줄마다 고른 수량. 아이템으로 기억해야 목록이 다시 그려져도 고른 값이 남는다.
        readonly Dictionary<ItemDefinition, int> _chosen = new Dictionary<ItemDefinition, int>();

        ConverterStation _station;

        void Awake()
        {
            if (_window == null) _window = GetComponent<UiWindow>();
            if (_router == null) _router = FindFirstObjectByType<UiRouter>();
            if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
            if (_gold == null) _gold = FindFirstObjectByType<GoldWallet>();
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            foreach (ConverterStation station in FindObjectsByType<ConverterStation>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                station.OpenRequested += Open;
            }

            if (_inventory != null) _inventory.Changed += OnInventoryChanged;
        }

        void OnDisable()
        {
            foreach (ConverterStation station in FindObjectsByType<ConverterStation>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                station.OpenRequested -= Open;
            }

            if (_inventory != null) _inventory.Changed -= OnInventoryChanged;
            _station = null;
        }

        void OnInventoryChanged(Inventory inventory)
        {
            if (_station == null || _window == null || !_window.IsOpen) return;

            Build();
            Redraw();
        }


        /// 같은 역에 다시 F를 누르면 닫는다. 창을 닫는 길이 ESC 하나뿐이면
        /// 열던 키로 닫지 못해 손이 한 번 더 움직인다(3차 프로토타입 피드백 2번).
        bool ToggledOff(ConverterStation station)
        {
            if (_window == null || !_window.IsOpen || _station != station) return false;

            _window.Close();
            return true;
        }

        void Open(ConverterStation station)
        {
            if (ToggledOff(station)) return;

            _station = station;
            _chosen.Clear();

            if (_notice != null) _notice.text = string.Empty;

            Build();
            Redraw();

            if (_router != null) _router.OpenPanel(_window);
        }

        void Build()
        {
            _station.CollectSellable(_stacks);

            var wanted = new List<ItemDefinition>();
            for (int i = 0; i < _stacks.Count; i++) wanted.Add(_stacks[i].Definition);

            // 종류가 그대로면 줄을 다시 만들지 않는다. 매번 지우면 고르던 수량 버튼이 사라진다.
            if (Same(wanted)) return;

            for (int i = 0; i < _rows.Count; i++) Destroy(_rows[i].Rect.gameObject);
            _rows.Clear();

            _items.Clear();
            _items.AddRange(wanted);

            for (int i = 0; i < _items.Count; i++) BuildRow(_items[i]);
        }

        bool Same(List<ItemDefinition> wanted)
        {
            if (wanted.Count != _items.Count) return false;
            for (int i = 0; i < wanted.Count; i++)
            {
                if (wanted[i] != _items[i]) return false;
            }
            return true;
        }

        void BuildRow(ItemDefinition item)
        {
            RectTransform rect = Instantiate(_rowTemplate, _rowParent);
            rect.gameObject.SetActive(true);
            rect.name = item.DisplayName;

            var row = new Row
            {
                Rect = rect,
                Icon = rect.Find("Icon").GetComponent<Image>(),
                Title = rect.Find("Title").GetComponent<TMP_Text>(),
                Detail = rect.Find("Detail").GetComponent<TMP_Text>(),
                Amount = rect.Find("Amount").GetComponent<TMP_Text>(),
                Less = rect.Find("Less").GetComponent<Button>(),
                More = rect.Find("More").GetComponent<Button>(),
                All = rect.Find("All").GetComponent<Button>(),
                Sell = rect.Find("Sell").GetComponent<Button>(),
                SellLabel = rect.Find("Sell/Text").GetComponent<TMP_Text>(),
            };

            ItemDefinition captured = item;
            row.Less.onClick.AddListener(() => Nudge(captured, -1));
            row.More.onClick.AddListener(() => Nudge(captured, 1));
            row.All.onClick.AddListener(() => Choose(captured, _inventory.CountOf(captured)));
            row.Sell.onClick.AddListener(() => SellOne(captured));

            _rows.Add(row);
        }

        int Chosen(ItemDefinition item)
        {
            int held = _inventory.CountOf(item);
            if (held <= 0) return 0;

            // 처음 보는 줄은 1개로 시작한다. 보유보다 많이 고른 상태로 남지 않게 늘 좁혀둔다.
            if (!_chosen.TryGetValue(item, out int picked)) picked = 1;
            return Mathf.Clamp(picked, 1, held);
        }

        void Choose(ItemDefinition item, int amount)
        {
            _chosen[item] = Mathf.Max(1, amount);
            Redraw();
        }

        void Nudge(ItemDefinition item, int delta) => Choose(item, Chosen(item) + delta);

        void SellOne(ItemDefinition item)
        {
            int amount = Chosen(item);
            if (!_station.Sell(item, amount)) return;

            // 다 팔았으면 다음에 열었을 때 1부터 시작하게 기억을 지운다.
            if (_inventory.CountOf(item) <= 0) _chosen.Remove(item);

            Say($"{item.DisplayName} {amount}개를 팔았습니다.");
        }

        void Redraw()
        {
            if (_station == null) return;

            if (_goldLabel != null) _goldLabel.text = $"골드 {_gold.Gold}";

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                ItemDefinition item = _items[i];

                int held = _inventory.CountOf(item);
                int unit = _station.UnitPrice(item);
                int amount = Chosen(item);

                row.Title.text = item.DisplayName;
                row.Detail.text = $"보유 {held} · 개당 {unit}G";
                row.Amount.text = amount.ToString();

                // 받을 골드를 버튼에 적는다. 따로 칸을 두는 것보다 누르는 손에 바로 붙는다.
                row.SellLabel.text = $"팔기 {_station.PriceFor(item, amount)}G";

                row.Less.interactable = amount > 1;
                row.More.interactable = amount < held;
                row.All.interactable = amount < held;
                row.Sell.interactable = held > 0;

                row.Icon.sprite = item.Icon;
                row.Icon.color = item.Icon != null ? Color.white : item.UiColor;
            }
        }

        void Say(string text)
        {
            if (_notice != null) _notice.text = text;
        }
    }
}
