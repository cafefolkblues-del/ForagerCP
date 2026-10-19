using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 제작대 화면. 왼쪽은 만들 수 있는 것, 오른쪽은 만드는 중인 자리.
    ///
    /// 제작대가 여럿일 수 있으므로(분류가 다른 전용 제작대) 어느 제작대를 연 것인지 들고 있는다.
    /// 줄은 연 제작대에 맞춰 다시 짓는다 — 제작법 수가 제작대마다 다르기 때문.
    public class CraftPanelView : MonoBehaviour
    {
        [SerializeField] UiWindow _window;
        [SerializeField] UiRouter _router;
        [SerializeField] PlayerEquipment _equipment;

        [Header("제작법")]
        [SerializeField] RectTransform _recipeParent;
        [SerializeField] RectTransform _recipeTemplate;

        [Header("자리")]
        [SerializeField] RectTransform _slotParent;
        [SerializeField] RectTransform _slotTemplate;

        [Header("아래")]
        [SerializeField] TMP_Text _gearLabel;
        [SerializeField] TMP_Text _notice;

        [Header("색")]
        [SerializeField] Color _ready = new Color(0.90f, 0.93f, 0.98f);
        [SerializeField] Color _blocked = new Color(1f, 0.62f, 0.55f);
        [SerializeField] Color _doneTint = new Color(0.98f, 0.84f, 0.42f);
        [SerializeField] Color _busyTint = new Color(0.45f, 0.72f, 0.95f);

        class RecipeRow
        {
            public RectTransform Rect;
            public Image Icon;
            public TMP_Text Title;
            public TMP_Text Detail;
            public Button Make;
            public Image MakeImage;
        }

        class SlotRow
        {
            public RectTransform Rect;
            public Image Icon;
            public TMP_Text Title;
            public TMP_Text Time;
            public RectTransform Fill;
            public Image FillImage;
            public Button Action;
            public TMP_Text ActionLabel;
        }

        readonly List<RecipeRow> _recipeRows = new List<RecipeRow>();
        readonly List<SlotRow> _slotRows = new List<SlotRow>();

        CraftingStation _station;

        void Awake()
        {
            if (_window == null) _window = GetComponent<UiWindow>();
            if (_router == null) _router = FindFirstObjectByType<UiRouter>();
            if (_equipment == null) _equipment = FindFirstObjectByType<PlayerEquipment>();

            if (_recipeTemplate != null) _recipeTemplate.gameObject.SetActive(false);
            if (_slotTemplate != null) _slotTemplate.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            // 제작대는 씬에 여럿일 수 있다. 누가 열자고 했는지 받아야 그 제작대를 그린다.
            foreach (CraftingStation station in FindObjectsByType<CraftingStation>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                station.OpenRequested += Open;
            }
        }

        void OnDisable()
        {
            foreach (CraftingStation station in FindObjectsByType<CraftingStation>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                station.OpenRequested -= Open;
            }

            Unbind();
        }


        /// 같은 역에 다시 F를 누르면 닫는다. 창을 닫는 길이 ESC 하나뿐이면
        /// 열던 키로 닫지 못해 손이 한 번 더 움직인다(3차 프로토타입 피드백 2번).
        bool ToggledOff(CraftingStation station)
        {
            if (_window == null || !_window.IsOpen || _station != station) return false;

            _window.Close();
            return true;
        }

        void Open(CraftingStation station)
        {
            if (ToggledOff(station)) return;

            Bind(station);

            if (_notice != null) _notice.text = string.Empty;
            if (_router != null) _router.OpenPanel(_window);
        }

        void Bind(CraftingStation station)
        {
            if (_station == station) { Redraw(); return; }

            Unbind();
            _station = station;

            _station.Changed += OnStationChanged;
            _station.Refused += Say;

            Build();
            Redraw();
        }

        void Unbind()
        {
            if (_station == null) return;

            _station.Changed -= OnStationChanged;
            _station.Refused -= Say;
            _station = null;
        }

        void OnStationChanged(CraftingStation station) => Redraw();

        /// 남은 시간이 매 프레임 줄어드는데 Changed는 그때마다 오므로, 열려 있을 때만 그린다.
        void Update()
        {
            if (_station == null || _window == null || !_window.IsOpen) return;
            RedrawSlots();
        }

        // ------------------------------------------------------------------ 짓기

        void Build()
        {
            // 제작대마다 제작법 수가 다르다. 바꿔 열면 줄도 다시 짓는다.
            Clear(_recipeRows);
            Clear(_slotRows);

            for (int i = 0; i < _station.Recipes.Count; i++) BuildRecipeRow(_station.Recipes[i]);
            for (int i = 0; i < _station.SlotCount; i++) BuildSlotRow(i);
        }

        void Clear<T>(List<T> rows) where T : class
        {
            foreach (T row in rows)
            {
                var rect = row.GetType().GetField("Rect").GetValue(row) as RectTransform;
                if (rect != null) Destroy(rect.gameObject);
            }
            rows.Clear();
        }

        void BuildRecipeRow(CraftRecipe recipe)
        {
            RectTransform rect = Instantiate(_recipeTemplate, _recipeParent);
            rect.gameObject.SetActive(true);
            rect.name = recipe.DisplayName;

            var row = new RecipeRow
            {
                Rect = rect,
                Icon = rect.Find("Icon").GetComponent<Image>(),
                Title = rect.Find("Title").GetComponent<TMP_Text>(),
                Detail = rect.Find("Detail").GetComponent<TMP_Text>(),
                Make = rect.Find("Make").GetComponent<Button>(),
                MakeImage = rect.Find("Make").GetComponent<Image>(),
            };

            // 람다가 바깥 변수를 잡으면 마지막 제작법만 가리킨다. 지역 변수로 복사해 고정한다.
            CraftRecipe captured = recipe;
            row.Make.onClick.AddListener(() => _station.TryStart(captured));

            _recipeRows.Add(row);
        }

        void BuildSlotRow(int index)
        {
            RectTransform rect = Instantiate(_slotTemplate, _slotParent);
            rect.gameObject.SetActive(true);
            rect.name = "Slot" + index;

            var row = new SlotRow
            {
                Rect = rect,
                Icon = rect.Find("Icon").GetComponent<Image>(),
                Title = rect.Find("Title").GetComponent<TMP_Text>(),
                Time = rect.Find("Time").GetComponent<TMP_Text>(),
                Fill = rect.Find("Bar/Fill").GetComponent<RectTransform>(),
                FillImage = rect.Find("Bar/Fill").GetComponent<Image>(),
                Action = rect.Find("Action").GetComponent<Button>(),
                ActionLabel = rect.Find("Action/Text").GetComponent<TMP_Text>(),
            };

            int captured = index;
            row.Action.onClick.AddListener(() => OnSlotAction(captured));

            _slotRows.Add(row);
        }

        /// 완성이면 받고, 만드는 중이면 무른다. 버튼 하나가 상황에 따라 둘을 겸한다.
        void OnSlotAction(int index)
        {
            if (_station.IsDone(index)) { _station.TryCollect(index); return; }
            if (_station.IsBusy(index)) _station.Cancel(index);
        }

        // ------------------------------------------------------------------ 그리기

        void Redraw()
        {
            if (_station == null) return;

            for (int i = 0; i < _recipeRows.Count; i++)
            {
                RecipeRow row = _recipeRows[i];
                CraftRecipe recipe = _station.Recipes[i];

                row.Title.text = recipe.DisplayName;

                bool can = _station.CanStart(recipe, out string reason);
                row.Detail.color = can ? _ready : _blocked;

                // 재료는 되든 안 되든 전 종류를 보유/필요로 보여준다.
                // 못 만들 때만 이유로 바꿔 끼우면 "무엇이 얼마나 모자란지"가 안 보여서,
                // 한 종류씩 채워 오며 같은 화면을 여러 번 열게 된다(3차 프로토타입 피드백 3번).
                // 재료가 아닌 이유(자리 없음 등)는 뒤에 덧붙인다.
                string ingredients = $"{_station.DescribeIngredients(recipe)} · {recipe.Seconds:0}초";
                row.Detail.text = can || string.IsNullOrEmpty(reason)
                    ? ingredients
                    : $"{ingredients}   —   {reason}";

                row.Make.interactable = can;

                Color tint = row.MakeImage.color;
                tint.a = can ? 1f : 0.35f;
                row.MakeImage.color = tint;

                row.Icon.color = recipe.Equipment != null ? recipe.Equipment.UiColor : Color.white;

                Color faded = row.Icon.color;
                faded.a = can ? 1f : 0.35f;
                row.Icon.color = faded;
            }

            RedrawSlots();

            if (_gearLabel != null)
            {
                _gearLabel.text = $"지금 도구 · {_equipment.NameOf(EquipmentSlot.Tool)}"
                                + $" (채집 +{_equipment.HarvestPower})";
            }
        }

        void RedrawSlots()
        {
            for (int i = 0; i < _slotRows.Count; i++)
            {
                SlotRow row = _slotRows[i];
                CraftRecipe recipe = _station.RecipeAt(i);

                if (recipe == null)
                {
                    row.Title.text = "빈 자리";
                    row.Time.text = string.Empty;
                    row.Fill.anchorMax = new Vector2(0f, 1f);
                    row.Icon.enabled = false;
                    row.Action.gameObject.SetActive(false);
                    continue;
                }

                bool done = _station.IsDone(i);
                float remaining = _station.RemainingAt(i);
                float ratio = recipe.Seconds <= 0f ? 1f : 1f - Mathf.Clamp01(remaining / recipe.Seconds);

                row.Title.text = recipe.DisplayName;
                row.Time.text = done ? "완성" : $"{Mathf.Ceil(remaining):0}초";

                row.Fill.anchorMax = new Vector2(done ? 1f : ratio, 1f);
                row.FillImage.color = done ? _doneTint : _busyTint;

                row.Icon.enabled = true;
                row.Icon.color = recipe.Equipment != null ? recipe.Equipment.UiColor : Color.white;

                row.Action.gameObject.SetActive(true);
                row.ActionLabel.text = done ? "받기" : "취소";
            }
        }

        void Say(string text)
        {
            if (_notice != null) _notice.text = text;
        }
    }
}
