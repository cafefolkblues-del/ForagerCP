using System;
using System.Collections;
using UnityEngine;

namespace ForagerCP
{
    /// 광물 오브젝트. 상태: 생성 → 공격 → 체력감소 → 0 → 파괴 → 리스폰대기 → 재생성 (사양 1-3).
    /// 수치는 전부 MineralDefinition에서 읽는다 — 새 광물 추가가 코드 수정 없이 끝나도록.
    /// 보상은 여기서 지급하지 않고 Harvested 이벤트만 쏜다(지급 순서·인벤 제약은 HarvestRewardService 담당).
    public class MineralNode : MonoBehaviour, IHarvestable
    {
        [SerializeField] MineralDefinition _definition;
        [SerializeField] HitFlash _hitFlash;

        /// 자리에 누가 서 있을 때 다시 확인하는 간격.
        [SerializeField] float _blockedRetryInterval = 0.5f;

        public event Action<MineralNode> Harvested;

        public bool IsHarvestable => _alive;
        public Transform Transform => transform;
        public MineralDefinition Definition => _definition;
        public int CurrentHp => _hp;
        public int MaxHp => _definition != null ? _definition.MaxHp : 0;

        Renderer[] _renderers;
        Collider _collider;
        Coroutine _respawnRoutine;
        bool _alive = true;
        int _hp;

        /// 콜라이더가 꺼진 뒤에는 bounds를 믿을 수 없어서, 켜져 있는 Awake 시점에 크기를 기억해둔다.
        Vector3 _blockCheckExtents = Vector3.one * 0.5f;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _collider = GetComponentInChildren<Collider>(true);
            if (_hitFlash == null) _hitFlash = GetComponent<HitFlash>();
            if (_collider != null) _blockCheckExtents = _collider.bounds.extents;

            if (_definition == null)
            {
                Debug.LogError($"{name}: 광물 종류(MineralDefinition)가 비어 있음", this);
                _alive = false;
                return;
            }

            _hp = _definition.MaxHp;
        }

        public void Harvest(int power)
        {
            if (!_alive) return; // 예외 1-10: 이미 파괴된 광물은 추가 공격해도 보상 없음

            _hp -= Mathf.Max(1, power);
            if (_hitFlash != null) _hitFlash.Play();

            if (_hp > 0) return;
            Break();
        }

        void Break()
        {
            _alive = false;
            _hp = 0;
            SetPresence(false);

            Harvested?.Invoke(this); // 보상 지급(1-4의 3~7)이 끝난 뒤에 리스폰 대기를 건다
            _respawnRoutine = StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(_definition.RespawnDelay);

            // 자리에 플레이어나 몬스터가 서 있으면 생성을 미룬다.
            // 그냥 켜버리면 콜라이더가 겹친 상대를 밀어내 광물 위로 올려버린다.
            // 통과시키지 않고 '보류'인 이유: 건너뛰면 그 칸의 광물이 영영 안 돌아온다.
            while (IsBlocked()) yield return new WaitForSeconds(_blockedRetryInterval);

            _respawnRoutine = null;
            Respawn();
        }

        /// 살아있는 것(IDamageable = 플레이어·몬스터)만 방해물로 본다.
        /// 다른 광물이나 구조물은 애초에 같은 칸에 놓이지 않으므로 볼 필요가 없다.
        bool IsBlocked()
        {
            Collider[] overlaps = Physics.OverlapBox(transform.position, _blockCheckExtents, transform.rotation);

            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] == null) continue;
                if (overlaps[i].GetComponentInParent<IDamageable>() != null) return true;
            }
            return false;
        }

        /// 디버그 즉시 재생성(1-11)도 같은 경로를 쓴다.
        public void Respawn()
        {
            if (_definition == null) return;

            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
                _respawnRoutine = null;
            }

            _hp = _definition.MaxHp;
            _alive = true;
            SetPresence(true);
        }

        /// SetActive(false)로 숨기면 이 오브젝트의 리스폰 코루틴이 같이 죽는다.
        /// 그래서 렌더러/콜라이더만 끄고 GameObject는 살려둔다.
        void SetPresence(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].enabled = visible;
            if (_collider != null) _collider.enabled = visible;
        }
    }
}
