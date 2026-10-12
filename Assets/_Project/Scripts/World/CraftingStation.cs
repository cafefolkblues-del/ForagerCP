using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 제작대. 재료를 받아 슬롯에 걸고, 시간이 차면 수령할 수 있게 둔다.
    ///
    /// 시간은 게임을 켜둔 동안에만 흐른다(확정). 그래서 저장에는 '끝나는 시각'이 아니라
    /// '남은 초'를 적는다 — 시각으로 적으면 꺼둔 사이에 저절로 완성된다.
    ///
    /// 분류(Category)가 맞는 제작법만 걸 수 있다. 보스 전용 제작대(M6)는 분류가 다른 제작대일 뿐이라
    /// 코드가 늘어나지 않는다.
    public class CraftingStation : MonoBehaviour, IInteractable, ISaveable, ISaveMilestone
    {
        [SerializeField] string _category = "Tools";
        [SerializeField] List<CraftRecipe> _recipes = new List<CraftRecipe>();

        /// 동시에 걸 수 있는 수.
        [SerializeField] int _slotCount = 2;

        [SerializeField] Inventory _inventory;
        [SerializeField] PlayerEquipment _equipment;
        [SerializeField] SaveCatalog _catalog;

        public event Action<CraftingStation> Changed;
        public event Action<CraftRecipe> Finished;

        /// 못 건 이유. 화면이 그대로 띄운다.
        public event Action<string> Refused;

        /// 장비를 새로 얻는 것은 되돌리기 어렵다.
        public event Action<string> Milestone;

        public string Category => _category;
        public IReadOnlyList<CraftRecipe> Recipes => _recipes;
        public int SlotCount => Mathf.Max(1, _slotCount);

        public Transform Transform => transform;

        class Slot
        {
            public CraftRecipe Recipe;
            public float Remaining;

            public bool Busy => Recipe != null;
            public bool Done => Recipe != null && Remaining <= 0f;
        }

        readonly List<Slot> _slots = new List<Slot>();

        public string Label
        {
            get
            {
                int done = 0, busy = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].Done) done++;
                    else if (_slots[i].Busy) busy++;
                }

                if (done > 0) return $"제작대 — 완성 {done}개 수령";
                return busy > 0 ? $"제작대 — 만드는 중 {busy}개" : "제작대 열기";
            }
        }

        void Awake()
        {
            if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
            if (_equipment == null) _equipment = FindFirstObjectByType<PlayerEquipment>();

            for (int i = 0; i < SlotCount; i++) _slots.Add(new Slot());
        }

        void Update()
        {
            bool moved = false;

            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];
                if (!slot.Busy || slot.Remaining <= 0f) continue;

                slot.Remaining -= Time.deltaTime;
                moved = true;

                if (slot.Remaining > 0f) continue;

                slot.Remaining = 0f;
                Finished?.Invoke(slot.Recipe);
            }

            if (moved) Changed?.Invoke(this);
        }

        // ------------------------------------------------------------------ 보기

        public CraftRecipe RecipeAt(int slot) => Valid(slot) ? _slots[slot].Recipe : null;
        public float RemainingAt(int slot) => Valid(slot) ? _slots[slot].Remaining : 0f;
        public bool IsBusy(int slot) => Valid(slot) && _slots[slot].Busy;
        public bool IsDone(int slot) => Valid(slot) && _slots[slot].Done;

        bool Valid(int slot) => slot >= 0 && slot < _slots.Count;

        // ------------------------------------------------------------------ 걸기

        public bool CanStart(CraftRecipe recipe, out string reason)
        {
            reason = null;
            if (recipe == null) { reason = "제작법이 없습니다"; return false; }

            if (recipe.Category != _category) { reason = "이 제작대에서는 만들 수 없습니다"; return false; }
            if (FreeSlot() < 0) { reason = "빈 자리가 없습니다"; return false; }

            // 만들어봐야 못 끼는 것은 애초에 걸지 못하게 막는다. 다 만들고 수령에서 막히면
            // 재료만 날리고 자리도 묶인다.
            if (recipe.Equipment != null && !_equipment.CanEquip(recipe.Equipment, out string why))
            {
                reason = why;
                return false;
            }

            if (!HasIngredients(recipe, out string missing)) { reason = $"{missing} 부족"; return false; }
            return true;
        }

        public bool TryStart(CraftRecipe recipe)
        {
            if (!CanStart(recipe, out string reason))
            {
                Refused?.Invoke(reason);
                return false;
            }

            // 먼저 치르고 건다. 반대로 하면 재료가 모자랄 때 자리만 묶인다.
            TakeIngredients(recipe);

            Slot slot = _slots[FreeSlot()];
            slot.Recipe = recipe;
            slot.Remaining = recipe.Seconds;

            // 시간이 0인 제작법은 다음 Update를 기다리지 않고 바로 완성으로 둔다.
            if (slot.Remaining <= 0f) Finished?.Invoke(recipe);

            Changed?.Invoke(this);
            return true;
        }

        /// 완성된 것을 받는다. 장비는 그 자리에서 끼워진다(보관 불가).
        public bool TryCollect(int index)
        {
            if (!Valid(index)) return false;

            Slot slot = _slots[index];
            if (!slot.Done) { Refused?.Invoke("아직 만드는 중입니다"); return false; }

            CraftRecipe recipe = slot.Recipe;

            if (recipe.Equipment != null && !_equipment.Equip(recipe.Equipment))
            {
                // 거는 시점에 확인했지만 그 사이 더 좋은 것을 꼈을 수 있다. 자리는 그대로 둔다.
                Refused?.Invoke("지금 낀 것보다 낮아 받을 수 없습니다");
                return false;
            }

            if (recipe.Item != null) _inventory.Add(recipe.Item, recipe.ItemCount);

            slot.Recipe = null;
            slot.Remaining = 0f;

            Changed?.Invoke(this);
            Milestone?.Invoke($"제작: {recipe.DisplayName}");
            return true;
        }

        /// 만드는 중인 것을 무른다. 재료는 전액 돌려준다(확정) — 잃는 것은 들인 시간뿐.
        public bool Cancel(int index)
        {
            if (!Valid(index)) return false;

            Slot slot = _slots[index];
            if (!slot.Busy) return false;

            CraftRecipe recipe = slot.Recipe;
            slot.Recipe = null;
            slot.Remaining = 0f;

            for (int i = 0; i < recipe.Ingredients.Count; i++)
            {
                CraftRecipe.Ingredient ingredient = recipe.Ingredients[i];
                if (ingredient?.Item == null) continue;

                _inventory.Add(ingredient.Item, ingredient.Count);
            }

            Changed?.Invoke(this);
            return true;
        }

        public void Interact()
        {
            // 화면이 붙기 전까지는 F가 '완성된 것 받기'로만 동작한다.
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Done) { TryCollect(i); return; }
            }
        }

        // ------------------------------------------------------------------ 재료

        int FreeSlot()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].Busy) return i;
            }
            return -1;
        }

        bool HasIngredients(CraftRecipe recipe, out string missing)
        {
            missing = null;

            for (int i = 0; i < recipe.Ingredients.Count; i++)
            {
                CraftRecipe.Ingredient ingredient = recipe.Ingredients[i];
                if (ingredient?.Item == null) continue;

                if (_inventory.CountOf(ingredient.Item) >= ingredient.Count) continue;

                missing = ingredient.Item.DisplayName;
                return false;
            }
            return true;
        }

        void TakeIngredients(CraftRecipe recipe)
        {
            for (int i = 0; i < recipe.Ingredients.Count; i++)
            {
                CraftRecipe.Ingredient ingredient = recipe.Ingredients[i];
                if (ingredient?.Item == null) continue;

                _inventory.Remove(ingredient.Item, ingredient.Count);
            }
        }

        // ------------------------------------------------------------------ 저장

        [Serializable]
        class SavedSlot
        {
            public string Recipe;
            public float Remaining;
        }

        [Serializable]
        class Save
        {
            public List<SavedSlot> Slots = new List<SavedSlot>();
        }

        /// 제작대가 여럿일 수 있으므로 분류를 칸 이름에 섞는다.
        public string SaveKey => "crafting:" + _category;

        public string Capture()
        {
            var save = new Save();
            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];
                save.Slots.Add(new SavedSlot
                {
                    Recipe = slot.Busy ? SaveCatalog.NameOf(slot.Recipe) : string.Empty,

                    // 초 단위로 깎아 적는다. 소수점까지 적으면 매 프레임 글자가 달라져
                    // '바뀐 게 있나' 비교가 늘 참이 된다.
                    Remaining = Mathf.Ceil(slot.Remaining),
                });
            }
            return JsonUtility.ToJson(save);
        }

        public void Restore(string payload)
        {
            var save = JsonUtility.FromJson<Save>(payload);
            if (save == null) return;

            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].Recipe = null;
                _slots[i].Remaining = 0f;

                if (i >= save.Slots.Count) continue;

                SavedSlot saved = save.Slots[i];
                if (string.IsNullOrEmpty(saved.Recipe)) continue;

                // 제작법이 지워졌으면 그 자리만 비운다. 재료를 돌려주지는 않는다 —
                // 없어진 제작법의 재료가 무엇이었는지 알 길이 없다.
                CraftRecipe recipe = _catalog != null ? _catalog.Find<CraftRecipe>(saved.Recipe) : null;
                if (recipe == null) continue;

                _slots[i].Recipe = recipe;
                _slots[i].Remaining = Mathf.Max(0f, saved.Remaining);
            }

            Changed?.Invoke(this);
        }
    }
}
