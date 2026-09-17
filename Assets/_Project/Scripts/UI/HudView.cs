using TMPro;
using UnityEngine;

namespace ForagerCP
{
    /// 사양 1-9. 데이터의 Changed 이벤트만 구독해서 값이 바뀐 직후 갱신한다(폴링 없음).
    public class HudView : MonoBehaviour
    {
        [SerializeField] PlayerLevel _level;
        [SerializeField] MineralWallet _minerals;
        [SerializeField] GoldWallet _gold;

        [SerializeField] TMP_Text _levelText;
        [SerializeField] TMP_Text _expText;
        [SerializeField] TMP_Text _mineralText;
        [SerializeField] TMP_Text _goldText;

        void OnEnable()
        {
            _level.Changed += OnLevelChanged;
            _minerals.Changed += OnMineralChanged;
            _gold.Changed += OnGoldChanged;

            // 이벤트 구독 전에 이미 값이 들어간 경우를 대비해 한 번 강제 동기화.
            OnLevelChanged(_level);
            OnMineralChanged(_minerals.Count);
            OnGoldChanged(_gold.Gold);
        }

        void OnDisable()
        {
            _level.Changed -= OnLevelChanged;
            _minerals.Changed -= OnMineralChanged;
            _gold.Changed -= OnGoldChanged;
        }

        void OnLevelChanged(PlayerLevel level)
        {
            if (_levelText != null) _levelText.text = $"Lv. {level.Level}";
            if (_expText == null) return;

            _expText.text = level.IsMaxLevel
                ? $"EXP {level.Exp} / -  (MAX)"
                : $"EXP {level.Exp} / {level.ExpToNext}";
        }

        void OnMineralChanged(int count)
        {
            if (_mineralText != null) _mineralText.text = $"Mineral {count}";
        }

        void OnGoldChanged(int gold)
        {
            if (_goldText != null) _goldText.text = $"Gold {gold}";
        }
    }
}
