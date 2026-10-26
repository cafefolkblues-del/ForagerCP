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

        public MonsterMotor(Transform transform, Rigidbody body)
        {
            _transform = transform;
            _body = body;
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
            return region == null || RegionMap.At(next) == region;
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
