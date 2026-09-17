using System;
using UnityEngine;

namespace ForagerCP
{
    /// F키 상호작용(사양 1-2). 2m 안 최근접 대상 하나, 없으면 무동작.
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] GameInput _input;
        [SerializeField] float _range = 2f;

        /// 대상 탐색 주기. 매 프레임 OverlapSphere를 도는 건 프롬프트 표시치고 과하다.
        [SerializeField] float _scanInterval = 0.1f;

        /// 상호작용 프롬프트가 구독한다. 대상이 바뀔 때만 알린다.
        public event Action<IInteractable> TargetChanged;

        public IInteractable CurrentTarget { get; private set; }

        float _nextScanTime;

        void Awake()
        {
            if (_input == null) _input = GetComponent<GameInput>();
        }

        // 구독을 OnEnable/OnDisable 쌍으로 두어 비활성화 중 입력이 새는 것을 막는다.
        void OnEnable() => _input.InteractPressed += TryInteract;
        void OnDisable() => _input.InteractPressed -= TryInteract;

        void Update()
        {
            if (Time.time < _nextScanTime) return;
            _nextScanTime = Time.time + _scanInterval;

            IInteractable found = FindNearestTarget();
            if (ReferenceEquals(found, CurrentTarget)) return;

            CurrentTarget = found;
            TargetChanged?.Invoke(found);
        }

        void TryInteract()
        {
            IInteractable target = FindNearestTarget();
            if (target == null) return; // 예외 1-10: 대상 없는 F는 무동작
            target.Interact();
        }

        IInteractable FindNearestTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, _range);

            IInteractable best = null;
            float bestSqrDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                IInteractable candidate = hits[i].GetComponentInParent<IInteractable>();
                if (candidate == null) continue;

                float sqrDistance = (candidate.Transform.position - transform.position).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance) continue;

                bestSqrDistance = sqrDistance;
                best = candidate;
            }

            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _range);
        }
    }
}
