using System;
using UnityEngine;

namespace ForagerCP
{
    /// 플레이어가 지금 어느 지역에 있는지. 바뀔 때만 알린다.
    ///
    /// 못 들어갈 곳에 들어와 버린 경우 되돌려보내는 보험도 여기가 진다.
    /// 문을 세워두는 것만으로는 부족하다 — 넉백에 밀리거나 대시로 끼어들면 벽을 지나칠 수 있고,
    /// 그렇게 한 번 새면 플레이어는 돌아올 방법을 모른다.
    public class RegionTracker : MonoBehaviour
    {
        [SerializeField] Transform _player;
        [SerializeField] RegionAccess _access;
        [SerializeField] Knockback _knockback;

        /// 잠긴 지역에 들어온 것을 감지하면 마지막으로 서 있던 자리로 되돌린다.
        [SerializeField] bool _pushBackFromLocked = true;

        public event Action<RegionDefinition> Changed;

        /// 되돌려보냈을 때. 화면에 "아직 갈 수 없는 곳입니다"를 띄우는 쪽이 구독한다.
        public event Action<RegionDefinition> PushedBack;

        public RegionDefinition Current { get; private set; }

        Rigidbody _body;
        Vector2Int _lastCell = new Vector2Int(int.MinValue, int.MinValue);
        Vector3 _safePosition;
        bool _hasSafePosition;

        void Awake()
        {
            if (_player == null) _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (_player == null) _player = transform;
            if (_access == null) _access = FindFirstObjectByType<RegionAccess>();
            if (_knockback == null) _knockback = _player.GetComponent<Knockback>();

            _body = _player.GetComponent<Rigidbody>();
        }

        void Start()
        {
            _safePosition = _player.position;
            _hasSafePosition = true;
        }

        void Update()
        {
            MapGrid grid = MapGrid.Active;
            if (grid == null) return;

            // 셀이 그대로면 지역도 그대로다. 사전 조회조차 할 이유가 없다.
            Vector2Int cell = grid.WorldToCell(_player.position);
            if (cell == _lastCell) return;
            _lastCell = cell;

            RegionDefinition region = RegionMap.At(cell);

            if (_pushBackFromLocked && _access != null && !_access.IsUnlocked(region))
            {
                PushBack(region);
                return;
            }

            _safePosition = _player.position;
            _hasSafePosition = true;

            if (region == Current) return;

            Current = region;
            Changed?.Invoke(region);
        }

        void PushBack(RegionDefinition region)
        {
            if (!_hasSafePosition) return;

            // 밀려 들어온 경우 넉백이 아직 살아 있다. 안 끄면 되돌리자마자 다시 밀려 핑퐁이 된다.
            if (_knockback != null) _knockback.Stop();

            if (_body != null) _body.position = _safePosition;
            _player.position = _safePosition;
            Physics.SyncTransforms();

            // 되돌린 자리의 셀로 맞춰둔다. 안 맞추면 다음 프레임에 '셀이 바뀌었다'며 또 돈다.
            MapGrid grid = MapGrid.Active;
            if (grid != null) _lastCell = grid.WorldToCell(_safePosition);

            PushedBack?.Invoke(region);
        }
    }
}
