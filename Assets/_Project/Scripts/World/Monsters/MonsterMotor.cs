using UnityEngine;

namespace ForagerCP
{
    /// 몬스터의 몸을 옮기는 유일한 곳. 어디로 갈지는 정하지 않고 "한 걸음"만 맡는다.
    ///
    /// 어디로 갈지는 몹마다 달라져도, 한 걸음 옮기는 방법은 같아야 한다 —
    /// MovePosition이어야 구조물을 뚫지 않고, fixedDeltaTime이어야 프레임레이트에 속도가 안 묶인다.
    /// 이걸 상태마다 따로 쓰면 한 곳만 transform.position을 써도 그 몹만 벽을 뚫는다.
    ///
    /// FixedUpdate 안에서만 부른다.
    public class MonsterMotor
    {
        readonly Transform _transform;
        readonly Rigidbody _body;
        readonly Collider[] _ownColliders;
        readonly RaycastHit[] _pathHits = new RaycastHit[32];
        readonly Collider[] _pathOverlaps = new Collider[32];

        public MonsterMotor(Transform transform, Rigidbody body)
        {
            _transform = transform;
            _body = body;
            _ownColliders = transform.GetComponentsInChildren<Collider>();
        }

        public Vector3 Position => _body != null ? _body.position : _transform.position;

        /// 이 방향으로 이 속도만큼 한 걸음.
        public void Step(Vector3 direction, float speed)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            MoveTo(Position + direction.normalized * (speed * Time.fixedDeltaTime));
        }

        /// 한 걸음 옮기되, 그 자리가 이 구역 밖이면 옮기지 않고 false.
        /// 구역이 null이면(구획 밖 몬스터) 따지지 않는다.
        public bool TryStepWithin(Vector3 direction, float speed, RegionDefinition region)
        {
            if (!CanStepWithin(direction, speed, region)) return false;

            MoveTo(Position + direction.normalized * (speed * Time.fixedDeltaTime));
            return true;
        }

        /// TryStepWithin과 같은 판정을 옮기지 않고 묻기만 한다.
        public bool CanStepWithin(Vector3 direction, float speed, RegionDefinition region)
        {
            if (direction.sqrMagnitude < 0.0001f) return false;

            Vector3 next = Position + direction.normalized * (speed * Time.fixedDeltaTime);
            return CanMoveWithin(next, region);
        }

        /// 일반 이동과 넉백이 공유하는 위치 검사. 구역이 없으면 기존처럼 제한하지 않는다.
        public bool CanMoveWithin(Vector3 position, RegionDefinition region)
            => region == null || RegionMap.At(position) == region;

        /// 우회 경로 후보 검사만 한다. 실제 이동은 기존 TryStepWithin이 담당한다.
        public bool CanTraverse(Vector3 from, Vector3 to, RegionDefinition region)
        {
            Vector3 delta = to - from;
            delta.y = 0f;
            float length = delta.magnitude;
            float spacing = MapGrid.Active != null ? Mathf.Max(0.1f, MapGrid.Active.CellSize * 0.4f) : 0.4f;
            int samples = Mathf.Max(1, Mathf.CeilToInt(length / spacing));
            for (int i = 0; i <= samples; i++)
                if (!CanMoveWithin(Vector3.Lerp(from, to, (float)i / samples), region)) return false;

            Bounds bounds = new Bounds(Position + Vector3.up * 0.5f, Vector3.one * 0.6f);
            bool found = false;
            foreach (Collider own in _ownColliders)
            {
                if (own == null || !own.enabled || own.isTrigger) continue;
                if (!found) { bounds = own.bounds; found = true; }
                else bounds.Encapsulate(own.bounds);
            }
            float radius = Mathf.Max(0.05f, Mathf.Max(bounds.extents.x, bounds.extents.z) - 0.03f);
            float halfHeight = Mathf.Max(radius, bounds.extents.y - 0.03f);
            Vector3 center = from + bounds.center - Position;
            Vector3 vertical = Vector3.up * (halfHeight - radius);
            Vector3 top = center + vertical;
            Vector3 bottom = center - vertical;

            int overlaps = Physics.OverlapCapsuleNonAlloc(top + delta, bottom + delta, radius,
                _pathOverlaps, ~0, QueryTriggerInteraction.Ignore);
            if (overlaps == _pathOverlaps.Length) return false;
            for (int i = 0; i < overlaps; i++)
                if (!_pathOverlaps[i].transform.IsChildOf(_transform)) return false;

            if (length < 0.0001f) return true;
            int hits = Physics.CapsuleCastNonAlloc(top, bottom, radius, delta / length,
                _pathHits, length, ~0, QueryTriggerInteraction.Ignore);
            if (hits == _pathHits.Length) return false;
            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = _pathHits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(_transform)) continue;
                if (hit.distance <= 0.0001f || Mathf.Abs(hit.normal.y) > 0.7f) continue;
                return false;
            }
            return true;
        }

        /// 몸의 충돌체로 경로를 훑어 장애물 앞까지 갈 수 있는 거리를 묻는다. 이동은 하지 않는다.
        /// ignoredTarget의 충돌체는 호출하는 공격이 별도로 타격을 판정한다.
        public float GetAllowedStep(Vector3 direction, float step, Transform ignoredTarget)
        {
            if (_body == null || step <= 0f || direction.sqrMagnitude < 0.0001f)
                return 0f;

            float allowedStep = step;

            RaycastHit[] hits = _body.SweepTestAll(
                direction.normalized,
                step,
                QueryTriggerInteraction.Ignore
            );

            foreach (RaycastHit hit in hits)
            {
                Collider collider = hit.collider;
                if (collider == null) continue;

                // 바닥이나 경사면은 벽으로 처리하지 않는다.
                if (Mathf.Abs(hit.normal.y) > 0.7f) continue;

                // 컴포넌트를 매번 찾지 않고 대상의 자식 충돌체까지 제외한다.
                if (ignoredTarget != null && collider.transform.IsChildOf(ignoredTarget))
                    continue;

                // 몸이 장애물에 닿기 직전까지만 이동한다.
                allowedStep = Mathf.Min(allowedStep, Mathf.Max(0f, hit.distance - 0.01f));
            }

            return allowedStep;
        }

        /// MovePosition: 콜라이더를 밀어내며 이동해 구조물을 관통하지 않는다.
        public void MoveTo(Vector3 position)
        {
            if (_body != null) _body.MovePosition(position);
            else _transform.position = position;
        }

        public void Face(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            _transform.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }
}
