using TMPro;
using UnityEngine;

namespace ForagerCP
{
    /// 상점 품목 한 줄.
    public class ShopRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _priceText;
        [SerializeField] TMP_Text _stateText;

        [SerializeField] Color _affordableColor = new Color(0.95f, 0.85f, 0.45f);
        [SerializeField] Color _unaffordableColor = new Color(0.65f, 0.65f, 0.7f);
        [SerializeField] Color _soldOutColor = new Color(0.45f, 0.75f, 0.55f);

        public void Show(ShopItem item, bool affordable)
        {
            if (item == null) return;

            if (_nameText != null) _nameText.text = item.DisplayName;
            if (_priceText != null) _priceText.text = item.Price + " G";

            bool soldOut = !item.CanPurchase;

            if (_stateText != null) _stateText.text = soldOut ? "구매함" : affordable ? "구매 가능" : "골드 부족";
            if (_priceText != null) _priceText.color = soldOut ? _soldOutColor : affordable ? _affordableColor : _unaffordableColor;
        }
    }
}
