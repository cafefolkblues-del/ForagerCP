using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 몬스터마다 머리 위 체력바를 붙여준다. 바는 오브젝트 풀에서 꺼내 쓴다.
    ///
    /// 체력바 수는 여기서 정하지 않는다. 몬스터가 켜지고 꺼질 때 스스로 알리고(Monster.Appeared/Vanished),
    /// 여기는 듣고 붙였다 뗄 뿐이다. 씬을 훑으면 그 뒤에 생긴 몬스터(스포너·소환)를 놓친다.
    public class MonsterHealthBarSpawner : MonoBehaviour
    {
        [SerializeField] GameObject _barPrefab;
        [SerializeField] Transform _barParent;
        [SerializeField] Camera _viewCamera;

        readonly Dictionary<Monster, GameObject> _bars = new Dictionary<Monster, GameObject>();
        bool _started;

        // 구독은 Start부터. 그 전엔 PoolManager.Awake가 안 끝났을 수 있어 풀 없이 Instantiate된다.
        // 다시 켜질 때는 OnEnable에서 이어 받는다.
        void OnEnable()
        {
            if (_started) Hook();
        }

        void Start()
        {
            if (_viewCamera == null) _viewCamera = Camera.main;
            _started = true;
            Hook();
        }

        void OnDisable()
        {
            Monster.Appeared -= Attach;
            Monster.Vanished -= Detach;

            foreach (KeyValuePair<Monster, GameObject> pair in _bars)
            {
                if (pair.Value != null) PoolManager.Despawn(pair.Value);
            }
            _bars.Clear();
        }

        /// 듣기 시작하기 전에 이미 켜진 몬스터는 이벤트를 놓쳤으므로, 몬스터 쪽 명단으로 한 번 따라잡는다.
        void Hook()
        {
            if (_barPrefab == null || _barParent == null) return;

            Monster.Appeared += Attach;
            Monster.Vanished += Detach;

            IReadOnlyList<Monster> active = Monster.Active;
            for (int i = 0; i < active.Count; i++) Attach(active[i]);
        }

        void Attach(Monster monster)
        {
            if (monster == null || _bars.ContainsKey(monster)) return;

            GameObject bar = PoolManager.Spawn(_barPrefab, Vector3.zero, Quaternion.identity, _barParent);
            if (bar == null) return;

            var view = bar.GetComponent<MonsterHealthBar>();
            if (view != null) view.Bind(monster, _viewCamera);

            _bars.Add(monster, bar);
        }

        void Detach(Monster monster)
        {
            if (monster == null || !_bars.TryGetValue(monster, out GameObject bar)) return;

            PoolManager.Despawn(bar);
            _bars.Remove(monster);
        }
    }
}
