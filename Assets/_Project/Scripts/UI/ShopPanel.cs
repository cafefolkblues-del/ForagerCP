using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 상점 창. ⚠️ 품목 구성이 기획 미확정(3장)이라 목록·구매 흐름만 세워둔 껍데기다.
    /// 확정되면 바뀌는 건 ShopStation의 품목 데이터이고, 이 화면은 그대로 쓸 수 있게
    /// "품목을 받아 줄로 그리고, 누르면 station.Interact()" 수준까지만 짠다.
    public class ShopPanel : MonoBehaviour
    {
        [SerializeField] ShopStation _station;
        [SerializeField] GoldWallet _gold;
        [SerializeField] Transform _rowParent;
        [SerializeField] ShopRowView _rowTemplate;
        [SerializeField] TMP_Text _goldText;
        [SerializeField] Button _buyButton;

        readonly List<ShopRowView> _rows = new List<ShopRowView>();

        void Awake()
        {
            if (_station == null) _station = FindFirstObjectByType<ShopStation>();
            if (_gold == null) _gold = FindFirstObjectByType<GoldWallet>();
            if (_rowTemplate != null) _rowTemplate.gameObject.SetActive(false);
            if (_buyButton != null) _buyButton.onClick.AddListener(Buy);
        }

        void OnEnable()
        {
            if (_gold != null) _gold.Changed += OnGoldChanged;
            if (_station != null) _station.Purchased += OnPurchased;

            Redraw();
        }

        void OnDisable()
        {
            if (_gold != null) _gold.Changed -= OnGoldChanged;
            if (_station != null) _station.Purchased -= OnPurchased;
        }

        void OnGoldChanged(int gold) => Redraw();

        void OnPurchased(ShopItem item) => Redraw();

        void Buy()
        {
            if (_station == null) return;

            // 구매 판정·차감은 전부 ShopStation이 한다. 화면은 요청만 보낸다.
            _station.Interact();
        }

        void Redraw()
        {
            if (_goldText != null && _gold != null) _goldText.text = $"보유 골드 {_gold.Gold}";
            if (_station == null || _rowTemplate == null || _rowParent == null) return;

            IReadOnlyList<ShopItem> items = _station.Items;

            while (_rows.Count < items.Count)
            {
                ShopRowView row = Instantiate(_rowTemplate, _rowParent);
                row.gameObject.SetActive(true);
                _rows.Add(row);
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < items.Count;
                _rows[i].gameObject.SetActive(used);
                if (!used) continue;

                bool affordable = _gold != null && _gold.Gold >= items[i].Price;
                _rows[i].Show(items[i], affordable);
            }
        }
    }
}
