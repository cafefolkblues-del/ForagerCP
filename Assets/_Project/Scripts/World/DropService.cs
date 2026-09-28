using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 바닥 드랍의 생성과 개수 관리.
    ///
    /// 확정 스펙: 드랍은 시간이 지나도 사라지지 않는다. 대신 바닥에 쌓일 수 있는 최대 개수를 두고,
    /// 넘치면 가장 오래된 것부터 치운다. 100은 임시값이라 인스펙터로 빼둔다.
    public class DropService : MonoBehaviour
    {
        [SerializeField] GameObject _dropPrefab;
        [SerializeField] Transform _dropParent;
        [SerializeField] Inventory _inventory;

        /// 바닥에 동시에 존재할 수 있는 드랍 수(임시값 — 기획 확정 전).
        /// 넘으면 가장 오래된 것을 치운다. 무한히 쌓이면 상호작용 탐색 비용이 계속 늘어난다.
        [SerializeField] int _maxDrops = 100;

        /// 같은 자리에 겹쳐 쌓이지 않게 흩뿌리는 반경.
        [SerializeField] float _scatterRadius = 0.45f;
        [SerializeField] float _groundOffset = 0.25f;

        /// (아이템, 개수, 주운 위치). 획득 표시가 구독한다.
        public event Action<ItemDefinition, int, Vector3> Picked;

        readonly List<ItemDrop> _active = new List<ItemDrop>();

        public int ActiveCount => _active.Count;
        public int MaxDrops => _maxDrops;

        void Awake()
        {
            if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
            if (_dropParent == null) _dropParent = transform;
        }

        public ItemDrop Spawn(ItemDefinition definition, int count, Vector3 position)
        {
            if (_dropPrefab == null || definition == null || count <= 0) return null;

            TrimToLimit();

            Vector2 scatter = UnityEngine.Random.insideUnitCircle * _scatterRadius;
            Vector3 spot = position + new Vector3(scatter.x, 0f, scatter.y);
            spot.y = position.y + _groundOffset;

            GameObject instance = PoolManager.Spawn(_dropPrefab, spot, Quaternion.identity, _dropParent);
            if (instance == null) return null;

            var drop = instance.GetComponent<ItemDrop>();
            if (drop == null) return null;

            drop.Setup(definition, count, _inventory);
            drop.PickedUp += OnPickedUp;
            _active.Add(drop);
            return drop;
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

                oldest.PickedUp -= OnPickedUp;
                PoolManager.Despawn(oldest.gameObject);
            }
        }

        void OnPickedUp(ItemDrop drop, int amount)
        {
            if (drop == null) return;

            Picked?.Invoke(drop.Definition, amount, drop.transform.position);

            // 일부만 주웠으면 남은 만큼 바닥에 그대로 둔다.
            if (drop.Count > 0) return;

            drop.PickedUp -= OnPickedUp;
            _active.Remove(drop);
            PoolManager.Despawn(drop.gameObject);
        }
    }
}
