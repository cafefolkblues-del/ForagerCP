using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 제작 하나. 무엇을 넣어 얼마를 기다리면 무엇이 나오는지.
    [CreateAssetMenu(menuName = "ForagerCP/제작법", fileName = "Recipe")]
    public class CraftRecipe : ScriptableObject
    {
        [Serializable]
        public class Ingredient
        {
            public ItemDefinition Item;
            public int Count = 1;
        }

        [SerializeField] string _displayName;

        [Header("결과 — 둘 중 하나만 채운다")]
        /// 장비를 만드는 제작. 완성품은 수령할 때 바로 끼워진다(보관 불가).
        [SerializeField] EquipmentDefinition _equipment;

        /// 아이템을 만드는 제작. 보스 소환물품(M6)이 이쪽이다.
        [SerializeField] ItemDefinition _item;
        [SerializeField] int _itemCount = 1;

        [Header("재료와 시간")]
        [SerializeField] List<Ingredient> _ingredients = new List<Ingredient>();

        /// 제작에 걸리는 시간(초). 게임을 켜둔 동안에만 흐른다.
        [SerializeField] float _seconds = 10f;

        /// 이 분류를 가진 제작대에서만 만들 수 있다.
        /// 보스 전용 제작대(M6)는 분류가 다른 제작대일 뿐이라 여기 값만 바꾸면 된다.
        [SerializeField] string _category = "Tools";

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(_displayName)) return _displayName;
                if (_equipment != null) return _equipment.DisplayName;
                return _item != null ? _item.DisplayName : name;
            }
        }

        public EquipmentDefinition Equipment => _equipment;
        public ItemDefinition Item => _item;
        public int ItemCount => Mathf.Max(1, _itemCount);
        public IReadOnlyList<Ingredient> Ingredients => _ingredients;
        public float Seconds => Mathf.Max(0f, _seconds);
        public string Category => _category;

        public string DescribeIngredients()
        {
            var parts = new List<string>();
            for (int i = 0; i < _ingredients.Count; i++)
            {
                Ingredient ingredient = _ingredients[i];
                if (ingredient?.Item == null) continue;

                parts.Add($"{ingredient.Item.DisplayName} {ingredient.Count}");
            }
            return parts.Count == 0 ? "재료 없음" : string.Join(", ", parts);
        }
    }
}
