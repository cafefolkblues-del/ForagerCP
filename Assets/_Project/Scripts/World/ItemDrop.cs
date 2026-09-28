using System;
using UnityEngine;

namespace ForagerCP
{
    /// 바닥에 떨어진 자원. F로 줍는다 — 기존 상호작용 경로(IInteractable)를 그대로 타므로
    /// "구리 광석 ×2 줍기" 프롬프트가 변환기·상점과 같은 방식으로 자동으로 뜬다.
    ///
    /// 확정 스펙: 시간이 지나도 사라지지 않는다. 대신 바닥에 쌓이는 개수를 DropService가 제한한다.
    /// 자주 생기고 사라지므로 풀에서 꺼내 쓴다.
    public class ItemDrop : MonoBehaviour, IInteractable, IPooled
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer[] _renderers;

        /// (드랍, 이번에 주운 수량). 인벤이 꽉 차면 일부만 줍고 나머지는 바닥에 남는다.
        public event Action<ItemDrop, int> PickedUp;

        public ItemDefinition Definition { get; private set; }
        public int Count { get; private set; }

        public Transform Transform => transform;

        public string Label => Definition == null
            ? "줍기"
            : Count > 1 ? $"{Definition.DisplayName} ×{Count} 줍기" : $"{Definition.DisplayName} 줍기";

        Inventory _inventory;
        MaterialPropertyBlock _block;

        void Awake()
        {
            if (_renderers == null || _renderers.Length == 0) _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        public void Setup(ItemDefinition definition, int count, Inventory inventory)
        {
            Definition = definition;
            Count = Mathf.Max(1, count);
            _inventory = inventory;

            Tint(definition != null ? definition.UiColor : Color.white);
        }

        public void Interact()
        {
            if (Definition == null || _inventory == null) return;

            int accepted = _inventory.Add(Definition, Count);
            if (accepted <= 0) return; // 인벤이 꽉 차면 그대로 바닥에 남는다(확장 2-4)

            Count -= accepted;
            PickedUp?.Invoke(this, accepted);
        }

        /// 머티리얼을 공유하므로 직접 칠하지 않고 프로퍼티 블록으로 덮는다.
        void Tint(Color color)
        {
            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _renderers[i].SetPropertyBlock(_block);
            }
        }

        public void OnSpawnedFromPool() => Count = 0;

        public void OnReturnedToPool()
        {
            Definition = null;
            Count = 0;
            _inventory = null;
            PickedUp = null;
        }
    }
}
