using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 상점 화면. 품목을 줄로 그리고 누르면 산다.
    ///
    /// 선행 품목을 안 산 것은 줄 자체를 만들지 않는다 — '언젠가 살 것'까지 띄우면
    /// 목록이 길어지고 지금 뭘 살 수 있는지가 묻힌다.
    public class ShopPanelView : MonoBehaviour
    {
        [SerializeField] UiWindow _window;
        [SerializeField] UiRouter _router;
        [SerializeField] GoldWallet _gold;

        [SerializeField] RectTransform _rowParent;
        [SerializeField] RectTransform _rowTemplate;
        [SerializeField] TMP_Text _goldLabel;
        [SerializeField] TMP_Text _notice;

        [SerializeField] Color _ready = new Color(0.86f, 0.88f, 0.93f);
        [SerializeField] Color _blocked = new Color(1f, 0.62f, 0.55f);

        class Row
        {
            public RectTransform Rect;
            public Image Icon;
            public TMP_Text Title;
            public TMP_Text Detail;
            public Button Buy;
            public TMP_Text BuyLabel;
            public Image BuyImage;
        }

        readonly List<Row> _rows = new List<Row>();
        readonly List<ShopGood> _listed = new List<ShopGood>();

        ShopStation _station;

        void Awake()
        {
            if (_window == null) _window = GetComponent<UiWindow>();
            if (_router == null) _router = FindFirstObjectByType<UiRouter>();
            if (_gold == null) _gold = FindFirstObjectByType<GoldWallet>();
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            foreach (ShopStation station in FindObjectsByType<ShopStation>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                station.OpenRequested += Open;
            }
        }

        void OnDisable()
        {
            foreach (ShopStation station in FindObjectsByType<ShopStation>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                station.OpenRequested -= Open;
            }

            Unbind();
        }


        /// 같은 역에 다시 F를 누르면 닫는다. 창을 닫는 길이 ESC 하나뿐이면
        /// 열던 키로 닫지 못해 손이 한 번 더 움직인다(3차 프로토타입 피드백 2번).
        bool ToggledOff(ShopStation station)
        {
            if (_window == null || !_window.IsOpen || _station != station) return false;

            _window.Close();
            return true;
        }

        void Open(ShopStation station)
        {
            if (ToggledOff(station)) return;

            Bind(station);

            if (_notice != null) _notice.text = string.Empty;
            if (_router != null) _router.OpenPanel(_window);
        }

        void Bind(ShopStation station)
        {
            if (_station != station)
            {
                Unbind();
                _station = station;

                _station.Purchased += OnPurchased;
                _station.Refused += Say;
            }

            Build();
            Redraw();
        }

        void Unbind()
        {
            if (_station == null) return;

            _station.Purchased -= OnPurchased;
            _station.Refused -= Say;
            _station = null;
        }

        /// 산 뒤에 선행 품목이 풀릴 수 있으므로 목록을 다시 짓는다.
        void OnPurchased(ShopGood good)
        {
            Build();
            Redraw();
        }

        void Build()
        {
            // 올라올 품목이 바뀌었을 때만 다시 짓는다. 매번 지우고 만들면 누르던 버튼이 사라진다.
            var wanted = new List<ShopGood>();
            for (int i = 0; i < _station.Goods.Count; i++)
            {
                ShopGood good = _station.Goods[i];
                if (_station.IsListed(good)) wanted.Add(good);
            }

            if (Same(wanted)) return;

            for (int i = 0; i < _rows.Count; i++) Destroy(_rows[i].Rect.gameObject);
            _rows.Clear();

            _listed.Clear();
            _listed.AddRange(wanted);

            for (int i = 0; i < _listed.Count; i++) BuildRow(_listed[i]);
        }

        bool Same(List<ShopGood> wanted)
        {
            if (wanted.Count != _listed.Count) return false;
            for (int i = 0; i < wanted.Count; i++)
            {
                if (wanted[i] != _listed[i]) return false;
            }
            return true;
        }

        void BuildRow(ShopGood good)
        {
            RectTransform rect = Instantiate(_rowTemplate, _rowParent);
            rect.gameObject.SetActive(true);
            rect.name = good.DisplayName;

            var row = new Row
            {
                Rect = rect,
                Icon = rect.Find("Icon").GetComponent<Image>(),
                Title = rect.Find("Title").GetComponent<TMP_Text>(),
                Detail = rect.Find("Detail").GetComponent<TMP_Text>(),
                Buy = rect.Find("Buy").GetComponent<Button>(),
                BuyLabel = rect.Find("Buy/Text").GetComponent<TMP_Text>(),
                BuyImage = rect.Find("Buy").GetComponent<Image>(),
            };

            ShopGood captured = good;
            row.Buy.onClick.AddListener(() => _station.TryBuy(captured));

            _rows.Add(row);
        }

        void Redraw()
        {
            if (_station == null) return;

            if (_goldLabel != null) _goldLabel.text = $"골드 {_gold.Gold}";

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                ShopGood good = _listed[i];

                row.Title.text = $"{good.DisplayName}  ·  {good.Category}";

                bool can = _station.CanBuy(good, out string reason);

                // 못 살 때는 효과 설명 대신 이유를 보여준다 — 무엇이 막는지가 더 급하다.
                row.Detail.color = can ? _ready : _blocked;
                row.Detail.text = can ? good.Describe() : reason;

                row.Buy.interactable = can;
                row.BuyLabel.text = $"{good.Price}G";

                Color tint = row.BuyImage.color;
                tint.a = can ? 1f : 0.35f;
                row.BuyImage.color = tint;

                Color icon = good.UiColor;
                icon.a = can ? 1f : 0.35f;
                row.Icon.sprite = good.Icon;
                row.Icon.color = icon;
            }
        }

        /// 골드는 밖에서도 바뀐다(판매·제작). 열려 있는 동안은 따라가게 둔다.
        void Update()
        {
            if (_station == null || _window == null || !_window.IsOpen) return;
            Redraw();
        }

        void Say(string text)
        {
            if (_notice != null) _notice.text = text;
        }
    }
}
