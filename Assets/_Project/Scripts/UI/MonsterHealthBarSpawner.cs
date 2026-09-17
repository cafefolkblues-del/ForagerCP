using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 몬스터마다 머리 위 체력바를 붙여준다. 바는 오브젝트 풀에서 꺼내 쓴다.
    /// 지금은 맵의 몬스터가 고정이라 시작할 때 한 번 붙이면 되고,
    /// 나중에 몬스터를 동적으로 스폰하게 되면 여기서 Spawn 이벤트만 구독하면 된다.
    public class MonsterHealthBarSpawner : MonoBehaviour
    {
        [SerializeField] GameObject _barPrefab;
        [SerializeField] Transform _barParent;
        [SerializeField] Camera _viewCamera;

        readonly Dictionary<Monster, GameObject> _bars = new Dictionary<Monster, GameObject>();

        void Start()
        {
            if (_viewCamera == null) _viewCamera = Camera.main;
            if (_barPrefab == null || _barParent == null) return;

            foreach (Monster monster in FindObjectsByType<Monster>(FindObjectsSortMode.None)) Attach(monster);
        }

        void OnDestroy()
        {
            foreach (KeyValuePair<Monster, GameObject> pair in _bars)
            {
                if (pair.Value != null) PoolManager.Despawn(pair.Value);
            }
            _bars.Clear();
        }

        public void Attach(Monster monster)
        {
            if (monster == null || _bars.ContainsKey(monster)) return;

            GameObject bar = PoolManager.Spawn(_barPrefab, Vector3.zero, Quaternion.identity, _barParent);
            if (bar == null) return;

            var view = bar.GetComponent<MonsterHealthBar>();
            if (view != null) view.Bind(monster, _viewCamera);

            _bars.Add(monster, bar);
        }

        public void Detach(Monster monster)
        {
            if (monster == null || !_bars.TryGetValue(monster, out GameObject bar)) return;

            PoolManager.Despawn(bar);
            _bars.Remove(monster);
        }
    }
}
