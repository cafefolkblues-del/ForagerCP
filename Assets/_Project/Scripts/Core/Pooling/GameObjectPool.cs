using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 프리팹 한 종류에 대한 재사용 풀.
    /// 자주 생기고 사라지는 것(드랍 아이템, 데미지 숫자, 타격 이펙트, 투사체)을 위한 것이고,
    /// 맵에 고정 배치되는 광물·몬스터는 애초에 파괴하지 않으므로 대상이 아니다.
    public class GameObjectPool
    {
        readonly GameObject _prefab;
        readonly Transform _root;

        /// 대기 중인 오브젝트 상한. 0이면 무제한으로 쌓아둔다.
        /// 상한을 넘겨 돌아온 것은 재우지 않고 파괴한다 — 한 번 크게 튄 사용량이 메모리에 눌러앉지 않도록.
        readonly int _maxIdle;

        readonly Stack<GameObject> _idle = new Stack<GameObject>();

        public GameObject Prefab => _prefab;
        public int IdleCount => _idle.Count;
        public int LiveCount { get; private set; }
        public int TotalCount => _idle.Count + LiveCount;

        public GameObjectPool(GameObject prefab, Transform root, int maxIdle = 0)
        {
            _prefab = prefab;
            _root = root;
            _maxIdle = Mathf.Max(0, maxIdle);
        }

        /// 미리 만들어 재워둔다. 첫 사용 때 생성 비용이 몰리는 것을 피하려는 용도.
        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject instance = Create();
                instance.SetActive(false);
                _idle.Push(instance);
            }
        }

        public GameObject Get(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            GameObject instance = _idle.Count > 0 ? _idle.Pop() : Create();

            // 재사용 중 파괴된 것(씬 전환 등)이 섞여 있으면 건너뛰고 새로 만든다.
            while (instance == null && _idle.Count > 0) instance = _idle.Pop();
            if (instance == null) instance = Create();

            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            var tag = instance.GetComponent<PooledInstance>();
            if (tag != null) tag.MarkInUse(true);

            LiveCount++;

            // SetActive 이후에 알린다 — 꺼진 상태에서는 코루틴을 못 돌리기 때문.
            foreach (IPooled pooled in instance.GetComponentsInChildren<IPooled>(true)) pooled.OnSpawnedFromPool();

            return instance;
        }

        /// 반환값: 이 풀이 실제로 회수했는지. 남의 풀 소속이거나 이미 반납된 것이면 false.
        public bool Release(GameObject instance)
        {
            if (instance == null) return false;

            var tag = instance.GetComponent<PooledInstance>();
            if (tag == null || tag.Pool != this) return false;
            if (!tag.IsInUse) return false; // 이중 반납 방지

            foreach (IPooled pooled in instance.GetComponentsInChildren<IPooled>(true)) pooled.OnReturnedToPool();

            tag.MarkInUse(false);
            instance.SetActive(false);
            instance.transform.SetParent(_root, false);
            LiveCount = Mathf.Max(0, LiveCount - 1);

            if (_maxIdle > 0 && _idle.Count >= _maxIdle)
            {
                Object.Destroy(instance);
                return true;
            }

            _idle.Push(instance);
            return true;
        }

        public void Clear(bool destroyIdle = true)
        {
            if (destroyIdle)
            {
                foreach (GameObject instance in _idle)
                {
                    if (instance != null) Object.Destroy(instance);
                }
            }

            _idle.Clear();
        }

        GameObject Create()
        {
            GameObject instance = Object.Instantiate(_prefab, _root);
            instance.name = _prefab.name;

            var tag = instance.GetComponent<PooledInstance>();
            if (tag == null) tag = instance.AddComponent<PooledInstance>();
            tag.Bind(this);

            return instance;
        }
    }
}
