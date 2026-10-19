using TMPro;
using UnityEngine;

namespace ForagerCP
{
    /// 사양 1-9. 왼쪽 위 상태 패널 — 레벨, 경험치 막대, 골드.
    /// 데이터의 Changed 이벤트만 구독해서 값이 바뀐 직후 갱신한다(폴링 없음).
    ///
    /// 광물 수량 줄과 경험치 숫자는 뺐다. 광물은 인벤 탭, 경험치 숫자는 스탯 탭에서 본다 —
    /// 늘 떠 있는 화면에 숫자를 늘어놓으면 정작 봐야 할 체력·핫바가 묻힌다.
    public class HudView : MonoBehaviour
    {
        [SerializeField] PlayerLevel _level;
        [SerializeField] GoldWallet _gold;

        [SerializeField] TMP_Text _levelText;
        [SerializeField] RectTransform _expFill;
        [SerializeField] TMP_Text _goldText;

        void OnEnable()
        {
            _level.Changed += OnLevelChanged;
            _gold.Changed += OnGoldChanged;

            // 이벤트 구독 전에 이미 값이 들어간 경우를 대비해 한 번 강제 동기화.
            OnLevelChanged(_level);
            OnGoldChanged(_gold.Gold);
        }

        void OnDisable()
        {
            _level.Changed -= OnLevelChanged;
            _gold.Changed -= OnGoldChanged;
        }

        void OnLevelChanged(PlayerLevel level)
        {
            if (_levelText != null) _levelText.text = $"Lv.{level.Level}";
            if (_expFill == null) return;

            // 최대 레벨은 다음 요구치가 0이라 꽉 채운다.
            float ratio = level.IsMaxLevel ? 1f : Mathf.Clamp01((float)level.Exp / Mathf.Max(1, level.ExpToNext));

            // 너비로 채운다 — Filled 방식은 9-slice 둥근 끝을 잘라낸다. 0이면 뭉개지므로 숨긴다.
            _expFill.anchorMax = new Vector2(ratio, _expFill.anchorMax.y);
            _expFill.gameObject.SetActive(ratio > 0f);
        }

        void OnGoldChanged(int gold)
        {
            if (_goldText != null) _goldText.text = gold.ToString();
        }
    }
}
