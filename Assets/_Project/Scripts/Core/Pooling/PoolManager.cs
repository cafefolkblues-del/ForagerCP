using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 프리팹별 풀을 모아두는 창구. 호출하는 쪽은 풀 객체를 몰라도 Spawn/Despawn만 쓰면 된다.
    ///
    /// 씬에 이 컴포넌트가 없어도 Spawn/Despawn은 동작한다(그냥 Instantiate/Destroy로 처리).
    /// 풀이 없다고 기능이 죽으면 안 되기 때문 — 풀은 성능 장치지 필수 의존이 아니다.
    public class PoolManager : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public GameObject Prefab;

            [Tooltip("미리 만들어 재워둘 개수")]
            public int Prewarm;

            [Tooltip("대기 상한(0 = 무제한). 넘겨 돌아온 것은 파괴한다.")]
            public int MaxIdle;
        }

        [SerializeField] Entry[] _entries = Array.Empty<Entry>();

        static PoolManager _instance;

        readonly Dictionary<GameObject, GameObjectPool> _pools = new Dictionary<GameObject, GameObjectPool>();

        void Awake()
        {
            _instance = this;

            for (int i = 0; i < _entries.Length; i++)
            {
                Entry entry = _entries[i];
                if (entry == null || entry.Prefab == null) continue;

                GameObjectPool pool = GetOrCreatePool(entry.Prefab, entry.MaxIdle);
                if (entry.Prewarm > 0) pool.Prewarm(entry.Prewarm);
            }
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public GameObjectPool GetOrCreatePool(GameObject prefab, int maxIdle = 0)
        {
            if (prefab == null) return null;
            if (_pools.TryGetValue(prefab, out GameObjectPool existing)) return existing;

            // 풀마다 전용 부모를 둬서 하이어라키에서 무엇이 얼마나 재워져 있는지 바로 보이게 한다.
            var root = new GameObject("Pool_" + prefab.name);
            root.transform.SetParent(transform, false);
            root.SetActive(true);

            var pool = new GameObjectPool(prefab, root.transform, maxIdle);
            _pools.Add(prefab, pool);
            return pool;
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null) return null;

            if (_instance == null)
            {
                // 풀 매니저가 없는 씬(테스트 씬 등)에서도 그대로 동작시킨다.
                GameObject plain = Instantiate(prefab, position, rotation, parent);
                return plain;
            }

            return _instance.GetOrCreatePool(prefab).Get(position, rotation, parent);
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position) =>
            Spawn(prefab, position, Quaternion.identity);

        /// 풀에서 나온 것이면 돌려보내고, 아니면 그냥 파괴한다.
        public static void Despawn(GameObject instance)
        {
            if (instance == null) return;

            var tag = instance.GetComponent<PooledInstance>();
            if (tag == null || tag.Pool == null)
            {
                Destroy(instance);
                return;
            }

            tag.Release();
        }

        /// 디버그용 현황. "풀이 계속 불어나는지"를 눈으로 보려고 남겨둔다.
        public string DumpStats()
        {
            var lines = new List<string>(_pools.Count);
            foreach (KeyValuePair<GameObject, GameObjectPool> pair in _pools)
            {
                GameObjectPool pool = pair.Value;
                lines.Add(pair.Key.name + " idle=" + pool.IdleCount + " live=" + pool.LiveCount + " total=" + pool.TotalCount);
            }
            return string.Join("\n", lines);
        }
    }
}
