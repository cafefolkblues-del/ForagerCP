using UnityEngine;

namespace ForagerCP
{
    /// 몬스터 한 마리의 짧은 우회 경로. 공격 타이머와 독립하고, 실제 이동은 Motor만 사용한다.
    public sealed class ThrowerNavigation
    {
        const int HalfSize = 7;
        const int Width = HalfSize * 2 + 1;
        const int Capacity = Width * Width;
        static readonly Vector2Int[] Directions =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1), new Vector2Int(-1, 1)
        };
        readonly int[] _parents = new int[Capacity];
        readonly int[] _queue = new int[Capacity];
        readonly Vector3[] _path = new Vector3[Capacity];
        int _pathCount;
        int _nextPoint;
        float _repathAt;
        float _lastMoveAt = float.NegativeInfinity;
        Vector3 _plannedTarget;
        Vector3 _origin;
        float _spacing;
        bool _escaping;

        public void Tick(MonsterContext ctx, float preferred, float approach)
        {
            // 실행기는 한 물리 프레임에 여러 상태를 실행할 수 있으므로 이동은 한 번만 한다.
            if (_lastMoveAt == ctx.Now) return;
            _lastMoveAt = ctx.Now;
            if (ctx.Distance < preferred) _escaping = true;
            if (ctx.Distance >= preferred + 0.5f) _escaping = false;
            if (!_escaping && ctx.Distance <= approach)
            {
                _pathCount = 0;
                return;
            }

            Vector3 player = ctx.Motor.Position + ctx.ToTarget;
            if (ctx.Now >= _repathAt || (player - _plannedTarget).sqrMagnitude > 1f)
            {
                Plan(ctx, player, preferred + 0.5f, _escaping);
                _plannedTarget = player;
                _repathAt = ctx.Now + 0.6f;
            }

            while (_nextPoint < _pathCount)
            {
                Vector3 direction = _path[_nextPoint] - ctx.Motor.Position;
                direction.y = 0f;
                float distance = direction.magnitude;
                if (distance < 0.08f) { _nextPoint++; continue; }
                float speed = Mathf.Min(ctx.Definition.MoveSpeed, distance / Time.fixedDeltaTime);
                Vector3 next = ctx.Motor.Position + direction.normalized * speed * Time.fixedDeltaTime;
                if (!ctx.Motor.CanTraverse(ctx.Motor.Position, next, ctx.Aggro.HomeRegion))
                {
                    _pathCount = 0;
                    _repathAt = 0f;
                    return;
                }
                ctx.Motor.TryStepWithin(direction, speed, ctx.Aggro.HomeRegion);
                return;
            }
        }

        void Plan(MonsterContext ctx, Vector3 player, float desired, bool escape)
        {
            _origin = ctx.Motor.Position;
            _spacing = MapGrid.Active != null ? Mathf.Clamp(MapGrid.Active.CellSize, 0.6f, 1f) : 0.8f;
            for (int i = 0; i < Capacity; i++) _parents[i] = -2;
            int start = HalfSize * Width + HalfSize;
            _parents[start] = -1;
            _queue[0] = start;
            int head = 0, tail = 1, best = start;
            float startDistance = FlatDistance(_origin, player);
            float bestScore = escape ? startDistance : -Mathf.Abs(startDistance - desired);
            while (head < tail)
            {
                int node = _queue[head++];
                Vector3 position = Position(node);
                float distance = FlatDistance(position, player);
                float score = escape ? Mathf.Min(distance, desired) : -Mathf.Abs(distance - desired);
                if (score > bestScore + 0.05f) { bestScore = score; best = node; }
                if (node != start && (escape ? distance >= desired : distance <= desired))
                {
                    best = node;
                    break;
                }
                int x = node % Width, z = node / Width;
                foreach (Vector2Int direction in Directions)
                {
                    int nx = x + direction.x, nz = z + direction.y;
                    if (nx < 0 || nx >= Width || nz < 0 || nz >= Width) continue;
                    int next = nz * Width + nx;
                    if (_parents[next] != -2) continue;
                    if (!ctx.Motor.CanTraverse(position, Position(next), ctx.Aggro.HomeRegion)) continue;
                    _parents[next] = node;
                    _queue[tail++] = next;
                }
            }
            _pathCount = 0;
            for (int node = best; node != start; node = _parents[node]) _path[_pathCount++] = Position(node);
            System.Array.Reverse(_path, 0, _pathCount);
            _nextPoint = 0;
        }

        Vector3 Position(int index)
            => _origin + new Vector3(index % Width - HalfSize, 0f, index / Width - HalfSize) * _spacing;

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            Vector3 delta = a - b;
            delta.y = 0f;
            return delta.magnitude;
        }
    }
}
