using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 바닥 드랍의 생성과 개수 관리.
    ///
    /// 떨어진 자원은 일정 시간이 지나면 사라진다. 개수 상한도 그대로 둔다 —
    /// 한 번에 왕창 떨어지면 수명이 다하기 전에 쌓이므로 둘 다 필요하다.
    public class DropService : MonoBehaviour
    {
        /// 종류별 모양이 없을 때 쓰는 기본 프리팹.
        [SerializeField] GameObject _dropPrefab;
        [SerializeField] Transform _dropParent;
        [SerializeField] Inventory _inventory;

        /// 바닥에 동시에 존재할 수 있는 드랍 수(임시값 — 기획 확정 전).
        /// 넘으면 가장 오래된 것을 치운다. 무한히 쌓이면 상호작용 탐색 비용이 계속 늘어난다.
        [SerializeField] int _maxDrops = 100;

        /// 떨어진 뒤 이 시간이 지나면 사라진다. 0 이하면 사라지지 않는다.
        [SerializeField] float _lifetime = 30f;

        /// 같은 자리에 겹쳐 쌓이지 않게 흩뿌리는 반경.
        [SerializeField] float _scatterRadius = 0.45f;
        [SerializeField] float _groundOffset = 0.25f;

        /// (아이템, 개수, 주운 위치). 획득 표시가 구독한다.
        public event Action<ItemDefinition, int, Vector3> Picked;

        readonly List<ItemDrop> _active = new List<ItemDrop>();

        /// 각 드랍이 사라질 시각. 드랍 자신에게 들리면 풀에서 재사용될 때 값이 따라다녀 꼬인다.
        readonly Dictionary<ItemDrop, float> _expiry = new Dictionary<ItemDrop, float>();

        public int ActiveCount => _active.Count;
        public int MaxDrops => _maxDrops;

        void Awake()
        {
            if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
            if (_dropParent == null) _dropParent = transform;
        }

        public ItemDrop Spawn(ItemDefinition definition, int count, Vector3 position)
        {
            if (definition == null || count <= 0) return null;

            // 종류가 자기 모양을 들고 있으면 그것을 쓰고, 없으면 기본 프리팹으로 떨어진다.
            GameObject prefab = definition.DropPrefab != null ? definition.DropPrefab : _dropPrefab;
            if (prefab == null) return null;

            TrimToLimit();

            Vector2 scatter = UnityEngine.Random.insideUnitCircle * _scatterRadius;
            Vector3 spot = position + new Vector3(scatter.x, 0f, scatter.y);
            spot.y = position.y + _groundOffset;

            GameObject instance = PoolManager.Spawn(prefab, spot, Quaternion.identity, _dropParent);
            if (instance == null) return null;

            // 아트가 준 모델 프리팹을 그대로 꽂아도 동작하게, 빠진 부품은 여기서 채워준다.
            // (풀에서 재사용되므로 이 보강은 인스턴스당 한 번만 일어난다)
            var drop = instance.GetComponent<ItemDrop>();
            if (drop == null) drop = instance.AddComponent<ItemDrop>();
            EnsurePickupCollider(instance);

            drop.Setup(definition, count, _inventory);
            drop.PickedUp += OnPickedUp;
            _active.Add(drop);

            if (_lifetime > 0f) _expiry[drop] = Time.time + _lifetime;
            return drop;
        }

        /// 줍기 판정은 콜라이더 기반이라 모델에 콜라이더가 없으면 영원히 못 줍는다.
        /// 트리거로 넣어야 길을 막지 않는다.
        static void EnsurePickupCollider(GameObject instance)
        {
            var collider = instance.GetComponentInChildren<Collider>(true);
            if (collider != null)
            {
                collider.isTrigger = true;
                return;
            }

            var box = instance.AddComponent<BoxCollider>();
            box.isTrigger = true;

            var renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer == null) { box.size = Vector3.one * 0.6f; return; }

            box.center = instance.transform.InverseTransformPoint(renderer.bounds.center);
            box.size = renderer.bounds.size;
        }

        /// 수명이 다한 것을 치운다. _active는 오래된 것이 앞이라 앞에서부터 보다가 멈추면 된다.
        void Update()
        {
            if (_lifetime <= 0f || _active.Count == 0) return;

            float now = Time.time;
            while (_active.Count > 0)
            {
                ItemDrop oldest = _active[0];
                if (oldest == null) { _active.RemoveAt(0); continue; }

                // 수명이 안 적힌 것(상한 조정 등으로 들어온 것)은 건드리지 않는다.
                if (!_expiry.TryGetValue(oldest, out float due) || now < due) return;

                _active.RemoveAt(0);
                Retire(oldest);
            }
        }

        void Retire(ItemDrop drop)
        {
            _expiry.Remove(drop);
            drop.PickedUp -= OnPickedUp;
            PoolManager.Despawn(drop.gameObject);
        }

        /// 상한을 넘기 전에 미리 한 칸 비운다. 가장 오래 놓여 있던 것이 먼저 사라진다.
        void TrimToLimit()
        {
            _active.RemoveAll(drop => drop == null);

            while (_active.Count >= Mathf.Max(1, _maxDrops))
            {
                ItemDrop oldest = _active[0];
                _active.RemoveAt(0);
                if (oldest == null) continue;

                Retire(oldest);
            }
        }

        void OnPickedUp(ItemDrop drop, int amount)
        {
            if (drop == null) return;

            Picked?.Invoke(drop.Definition, amount, drop.transform.position);

            // 일부만 주웠으면 남은 만큼 바닥에 그대로 둔다.
            if (drop.Count > 0) return;

            _active.Remove(drop);
            Retire(drop);
        }
    }
}
