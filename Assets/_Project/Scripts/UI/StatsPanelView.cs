using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 스탯 탭(책 화면). 왼쪽 쪽은 레벨·경험치·체력과 능력치 네 줄, 오른쪽 쪽은 고른 능력치의 설명.
    ///
    /// 보기 전용이다 — 능력치를 올리는 길(장비·버프·스킬·상점)은 이미 따로 있고, 여기는 그 결과를 모아 보여준다.
    /// 수치는 실제로 쓰는 쪽(PlayerAttacker·PlayerHealth…)의 프로퍼티를 그대로 읽는다.
    /// 화면용 계산을 여기 따로 두면 공식이 바뀔 때 화면만 옛 숫자로 남는다.
    public class StatsPanelView : MonoBehaviour
    {
        public enum StatKind { Attack, Defense, MoveSpeed, Harvest }

        /// 능력치 한 줄. 막대가 꽉 차는 값(_displayMax)은 표시용일 뿐 실제 상한이 아니다.
        [Serializable]
        public class StatRow
        {
            public StatKind Kind;
            public string DisplayName;
            [TextArea] public string Description;
            public Sprite Icon;
            public float DisplayMax = 100f;

            public Button Button;
            public RectTransform Fill;
            public TMP_Text ValueLabel;
        }

        [SerializeField] PlayerLevel _level;
        [SerializeField] PlayerHealth _health;
        [SerializeField] PlayerAttacker _attacker;
        [SerializeField] PlayerMotor _motor;
        [SerializeField] HarvestPower _harvest;
        [SerializeField] PlayerEquipment _equipment;
        [SerializeField] PlayerBuffs _buffs;

        [Header("왼쪽 — 레벨·체력")]
        [SerializeField] TMP_Text _levelLabel;
        [SerializeField] RectTransform _expFill;
        [SerializeField] TMP_Text _expLabel;
        [SerializeField] RectTransform _hpFill;
        [SerializeField] TMP_Text _hpLabel;

        [Header("왼쪽 — 능력치")]
        [SerializeField] StatRow[] _rows = Array.Empty<StatRow>();

        [Header("오른쪽 — 고른 능력치")]
        [SerializeField] Image _detailIcon;

        /// 아이콘을 원본 픽셀의 몇 배로 그릴지. 칸에 맞춰 늘리면 넓적한 그림(채집)만 유독 작아진다 —
        /// 아이콘들은 같은 축척으로 그려졌으니 같은 배율로 그려야 크기가 고르게 보인다.
        [SerializeField] float _detailIconScale = 1f;
        [SerializeField] TMP_Text _detailName;
        [SerializeField] TMP_Text _detailDescription;
        [SerializeField] TMP_Text _detailCurrent;
        [SerializeField] TMP_Text _detailBreakdown;

        int _selected;

        void Awake()
        {
            if (_level == null) _level = FindFirstObjectByType<PlayerLevel>();
            if (_health == null) _health = FindFirstObjectByType<PlayerHealth>();
            if (_attacker == null) _attacker = FindFirstObjectByType<PlayerAttacker>();
            if (_motor == null) _motor = FindFirstObjectByType<PlayerMotor>();
            if (_harvest == null) _harvest = FindFirstObjectByType<HarvestPower>();
            if (_equipment == null) _equipment = FindFirstObjectByType<PlayerEquipment>();
            if (_buffs == null) _buffs = FindFirstObjectByType<PlayerBuffs>();

            for (int i = 0; i < _rows.Length; i++)
            {
                int index = i; // 클로저가 루프 변수를 잡으면 전부 마지막 줄을 가리킨다.
                if (_rows[i].Button != null) _rows[i].Button.onClick.AddListener(() => Select(index));
            }
        }

        // 창은 닫혀도 꺼지지 않는다(UiWindow가 알파만 내린다). 그래서 구독은 늘 살아 있고,
        // 열었을 때 옛 숫자가 보이는 일이 없다.
        void OnEnable()
        {
            if (_level != null) _level.Changed += OnLevelChanged;
            if (_health != null) _health.Changed += OnHealthChanged;
            if (_harvest != null) _harvest.Changed += OnHarvestChanged;
            if (_equipment != null) _equipment.Changed += OnEquipmentChanged;
            if (_buffs != null) _buffs.Changed += OnBuffsChanged;
            Redraw();
        }

        void OnDisable()
        {
            if (_level != null) _level.Changed -= OnLevelChanged;
            if (_health != null) _health.Changed -= OnHealthChanged;
            if (_harvest != null) _harvest.Changed -= OnHarvestChanged;
            if (_equipment != null) _equipment.Changed -= OnEquipmentChanged;
            if (_buffs != null) _buffs.Changed -= OnBuffsChanged;
        }

        void OnLevelChanged(PlayerLevel _) => Redraw();
        void OnHealthChanged(PlayerHealth _) => Redraw();
        void OnHarvestChanged(int _) => Redraw();
        void OnEquipmentChanged(PlayerEquipment _) => Redraw();
        void OnBuffsChanged(PlayerBuffs _) => Redraw();

        public void Select(int index)
        {
            if (index < 0 || index >= _rows.Length) return;
            _selected = index;
            RedrawDetail();
        }

        void Redraw()
        {
            RedrawHeader();

            for (int i = 0; i < _rows.Length; i++)
            {
                StatRow row = _rows[i];
                float value = ValueOf(row.Kind);
                SetFill(row.Fill, row.DisplayMax <= 0f ? 0f : value / row.DisplayMax);
                if (row.ValueLabel != null) row.ValueLabel.text = $"{Format(row.Kind, value)}/{Format(row.Kind, row.DisplayMax)}";
            }

            RedrawDetail();
        }

        void RedrawHeader()
        {
            if (_level != null)
            {
                if (_levelLabel != null) _levelLabel.text = $"Lv. {_level.Level:00}";

                // 최대 레벨은 다음 요구치가 0이라 막대를 꽉 채우고 숫자 대신 MAX를 찍는다.
                SetFill(_expFill, _level.IsMaxLevel ? 1f : (float)_level.Exp / Mathf.Max(1, _level.ExpToNext));
                if (_expLabel != null) _expLabel.text = _level.IsMaxLevel ? "MAX" : $"{_level.Exp}/{_level.ExpToNext}";
            }

            if (_health != null)
            {
                SetFill(_hpFill, (float)_health.CurrentHp / Mathf.Max(1, _health.MaxHp));
                if (_hpLabel != null) _hpLabel.text = $"{_health.CurrentHp}/{_health.MaxHp}";
            }
        }

        void RedrawDetail()
        {
            if (_rows.Length == 0) return;

            StatRow row = _rows[Mathf.Clamp(_selected, 0, _rows.Length - 1)];

            if (_detailIcon != null)
            {
                _detailIcon.sprite = row.Icon;
                _detailIcon.enabled = row.Icon != null;
                if (row.Icon != null) _detailIcon.rectTransform.sizeDelta = row.Icon.rect.size * _detailIconScale;
            }
            if (_detailName != null) _detailName.text = row.DisplayName;
            if (_detailDescription != null) _detailDescription.text = row.Description;
            if (_detailCurrent != null) _detailCurrent.text = Format(row.Kind, ValueOf(row.Kind)) + UnitOf(row.Kind);
            if (_detailBreakdown != null) _detailBreakdown.text = BreakdownOf(row.Kind);
        }

        float ValueOf(StatKind kind)
        {
            switch (kind)
            {
                case StatKind.Attack: return _attacker != null ? _attacker.AttackPower : 0f;
                case StatKind.Defense: return _health != null ? _health.DamageReductionPercent : 0f;
                case StatKind.MoveSpeed: return _motor != null ? _motor.MoveSpeed : 0f;
                case StatKind.Harvest: return _harvest != null ? _harvest.Value : 0f;
                default: return 0f;
            }
        }

        /// 어디서 얼마가 왔는지. 숫자 하나만 보여주면 "왜 이 값인지"를 플레이어가 알 길이 없다.
        string BreakdownOf(StatKind kind)
        {
            switch (kind)
            {
                case StatKind.Attack:
                {
                    int weapon = _equipment != null ? _equipment.AttackPower : 0;
                    int buff = _buffs != null ? _buffs.AttackPower : 0;
                    return $"무기 {weapon}\n버프 +{buff}%";
                }
                case StatKind.Defense:
                {
                    int gear = _equipment != null ? _equipment.DamageReduction : 0;
                    int buff = _buffs != null ? _buffs.DamageReduction : 0;
                    return $"방어구 {gear}%\n버프 +{buff}%\n(최대 90%)";
                }
                case StatKind.MoveSpeed:
                    return "기본 속도";
                case StatKind.Harvest:
                {
                    if (_harvest == null) return string.Empty;
                    int basePower = _harvest.Value - _harvest.FromTool - _harvest.FromBuff - _harvest.Bonus;
                    return $"기본 {basePower}  도구 +{_harvest.FromTool}  강화 +{_harvest.Bonus}\n" +
                           $"채집 간격 -{_harvest.HarvestSpeedPercent}%";
                }
                default:
                    return string.Empty;
            }
        }

        static string UnitOf(StatKind kind) => kind == StatKind.Defense ? "%" : string.Empty;

        /// 이동 속도만 소수(5.5 같은 값)라 한 자리까지, 나머지는 정수로.
        static string Format(StatKind kind, float value) =>
            kind == StatKind.MoveSpeed ? value.ToString("0.#") : Mathf.RoundToInt(value).ToString();

        /// 채움은 너비로 조절한다. Image의 Filled 방식은 9-slice 둥근 끝을 무시하고 잘라내서
        /// 막대 끝이 각지게 남는다.
        static void SetFill(RectTransform fill, float ratio)
        {
            if (fill == null) return;

            ratio = Mathf.Clamp01(ratio);
            fill.anchorMax = new Vector2(ratio, fill.anchorMax.y);

            // 너비가 둥근 끝 두 개보다 좁아지면 스프라이트가 뭉개진다. 0일 때는 아예 숨긴다.
            fill.gameObject.SetActive(ratio > 0f);
        }
    }
}
