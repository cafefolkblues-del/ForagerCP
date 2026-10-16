using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 한 구역의 몬스터를 만들고 치운다.
    ///
    /// 두 방식을 모두 둔 이유: 기획서는 "지정된 위치에 함께 생성"이라 적었는데 구두로는
    /// 랜덤 배치 이야기가 나와, 어느 쪽으로 확정되든 에셋 설정만 바꾸면 되게 해둔다.
    ///
    /// 랜덤은 '지형을 만드는' 생성이 아니라 **디자이너가 그은 영역 안에서 빈 칸을 고르는** 것이다.
    /// 그래서 런타임 절차적 생성 금지와 부딪히지 않는다 — 무엇이 나오는지는 전부 에셋이 정하고,
    /// 고르는 것은 자리뿐이다.
    ///
    /// 고른 자리는 기억해 둔다. 구역 전투가 초기화되면 "원래 배치와 체력으로 다시 나온다"가
    /// 기획이라, 초기화마다 자리가 달라지면 안 된다. 다시 뽑고 싶으면 Reroll을 부른다.
    public class MonsterSpawner : MonoBehaviour
    {
        public enum Mode
        {
            /// 자식 오브젝트의 자리에 하나씩. 디자이너가 직접 놓는다.
            Placed,

            /// 지정한 칸 범위 안에서 빈 칸을 골라 흩는다.
            Random,
        }

        [SerializeField] Mode _mode = Mode.Placed;
        [SerializeField] GameObject _prefab;

        [Header("배치")]
        /// 비워두면 이 오브젝트의 자식 전부를 자리로 쓴다.
        [SerializeField] List<Transform> _points = new List<Transform>();

        [Header("랜덤")]
        [SerializeField] int _count = 3;

        /// 뽑을 칸 범위(최소 코너와 크기). 기즈모로 보인다.
        [SerializeField] Vector2Int _areaOrigin;
        [SerializeField] Vector2Int _areaSize = new Vector2Int(10, 10);

        /// 서로 이만큼은 떨어뜨린다. 기획이 "시작 위치를 분산한다"고 했고,
        /// 붙여 놓으면 콜라이더가 서로를 밀어 첫 프레임에 자리가 흐트러진다.
        [SerializeField] int _minSpacing = 3;

        /// 빈 칸을 못 찾을 때 포기하기까지의 시도 횟수. 범위가 좁으면 영영 못 찾을 수 있다.
        [SerializeField] int _maxAttempts = 200;

        /// 땅에서 띄우는 높이. 놓인 블럭과 같은 이유로 여기 둔다.
        [SerializeField] float _heightOffset = 0.5f;

        public Mode Current => _mode;
        public int Alive
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _spawned.Count; i++)
                {
                    if (_spawned[i] != null && _spawned[i].IsAlive) count++;
                }
                return count;
            }
        }

        public int SpawnedCount => _spawned.Count;

        readonly List<Monster> _spawned = new List<Monster>();

        /// 한 번 고른 자리. 초기화해도 같은 자리에 다시 선다.
        readonly List<Vector2Int> _cells = new List<Vector2Int>();

        public IReadOnlyList<Vector2Int> Cells => _cells;

        /// 지금 깔린 것을 치우고 다시 세운다. 구역 전투 초기화가 이걸 부른다.
        public void Respawn()
        {
            Clear();
            Spawn();
        }

        public void Spawn()
        {
            if (_prefab == null) return;

            if (_cells.Count == 0) ChooseCells();

            MapGrid grid = MapGrid.Active;
            for (int i = 0; i < _cells.Count; i++)
            {
                Vector3 position = grid != null
                    ? grid.CellToWorld(_cells[i], Vector2Int.one) + Vector3.up * _heightOffset
                    : transform.position;

                GameObject instance = Instantiate(_prefab, position, Quaternion.identity, transform);
                instance.name = _prefab.name + "_" + i;

                // 칸 좌표를 심어준다. 죽음 기록(MonsterDeaths)이 칸으로 몬스터를 알아보므로,
                // 런타임에 생긴 것도 칸을 들고 있어야 저장이 성립한다.
                if (instance.TryGetComponent(out GridPlacement placement))
                {
                    placement.Configure(_cells[i], placement.Size, _heightOffset);
                }

                if (instance.TryGetComponent(out Monster monster)) _spawned.Add(monster);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] == null) continue;

                // 같은 프레임에 다시 세울 수 있어서 즉시 지운다.
                // 보통 Destroy는 프레임 끝에 도는데, 그 사이에 새로 세우면 둘이 겹친다
                // (PlacedBlocks에서 이미 당한 적이 있다).
                DestroyImmediate(_spawned[i].gameObject);
            }
            _spawned.Clear();
        }

        /// 자리를 새로 뽑는다. 배치 모드에서는 자식 자리를 다시 읽는다.
        public void Reroll()
        {
            _cells.Clear();
            ChooseCells();
        }

        void ChooseCells()
        {
            _cells.Clear();

            if (_mode == Mode.Placed)
            {
                ChoosePlaced();
                return;
            }

            ChooseRandom();
        }

        void ChoosePlaced()
        {
            MapGrid grid = MapGrid.Active;

            List<Transform> points = _points;
            if (points == null || points.Count == 0)
            {
                points = new List<Transform>();
                for (int i = 0; i < transform.childCount; i++) points.Add(transform.GetChild(i));
            }

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null) continue;
                _cells.Add(grid != null ? grid.WorldToCell(points[i].position) : Vector2Int.zero);
            }
        }

        void ChooseRandom()
        {
            int wanted = Mathf.Max(0, _count);
            int attempts = 0;

            while (_cells.Count < wanted && attempts < Mathf.Max(1, _maxAttempts))
            {
                attempts++;

                var cell = new Vector2Int(
                    _areaOrigin.x + Random.Range(0, Mathf.Max(1, _areaSize.x)),
                    _areaOrigin.y + Random.Range(0, Mathf.Max(1, _areaSize.y)));

                if (!IsUsable(cell)) continue;
                _cells.Add(cell);
            }
        }

        /// 이미 뭔가 놓인 칸과, 먼저 고른 자리에 너무 가까운 칸은 거른다.
        bool IsUsable(Vector2Int cell)
        {
            if (!GridOccupancy.IsFree(new RectInt(cell.x, cell.y, 1, 1))) return false;

            for (int i = 0; i < _cells.Count; i++)
            {
                Vector2Int gap = _cells[i] - cell;
                if (Mathf.Abs(gap.x) < _minSpacing && Mathf.Abs(gap.y) < _minSpacing) return false;
            }
            return true;
        }

        void OnDrawGizmosSelected()
        {
            MapGrid grid = MapGrid.Active;
            if (grid == null) return;

            if (_mode == Mode.Random)
            {
                Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.35f);
                Vector3 min = grid.CellToWorld(_areaOrigin, Vector2Int.one);
                Vector3 max = grid.CellToWorld(_areaOrigin + _areaSize, Vector2Int.one);
                Gizmos.DrawWireCube((min + max) * 0.5f, new Vector3(Mathf.Abs(max.x - min.x), 0.2f,
                    Mathf.Abs(max.z - min.z)));
            }

            Gizmos.color = new Color(1f, 0.35f, 0.3f, 0.9f);
            for (int i = 0; i < _cells.Count; i++)
            {
                Gizmos.DrawWireSphere(grid.CellToWorld(_cells[i], Vector2Int.one)
                    + Vector3.up * _heightOffset, 0.4f);
            }
        }
    }
}
