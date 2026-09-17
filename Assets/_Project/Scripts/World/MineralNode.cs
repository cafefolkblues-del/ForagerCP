using System;
using System.Collections;
using UnityEngine;

namespace ForagerCP
{
    /// 광물 오브젝트. 상태: 생성 → 공격 → 체력감소 → 0 → 파괴 → 리스폰대기 → 재생성 (사양 1-3).
    /// 보상은 여기서 직접 지급하지 않고 Harvested 이벤트만 쏜다 — 지급 순서와 인벤 제약 삽입 지점을
    /// HarvestRewardService 한 곳에 몰아두기 위해서.
    public class MineralNode : MonoBehaviour, IHarvestable
    {
        [SerializeField] int _maxHp = 30;
        [SerializeField] float _respawnDelay = 10f;
        [SerializeField] int _mineralReward = 1;
        [SerializeField] int _expReward = 1;

        public event Action<MineralNode> Harvested;

        public bool IsHarvestable => _alive;
        public Transform Transform => transform;
        public int MineralReward => _mineralReward;
        public int ExpReward => _expReward;
        public int CurrentHp => _hp;
        public int MaxHp => _maxHp;

        Renderer[] _renderers;
        Collider _collider;
        Coroutine _respawnRoutine;
        bool _alive = true;
        int _hp;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _collider = GetComponentInChildren<Collider>(true);
            _hp = _maxHp;
        }

        public void Harvest(int power)
        {
            if (!_alive) return; // 예외 1-10: 이미 파괴된 광물은 추가 공격해도 보상 없음
            _hp -= Mathf.Max(1, power);
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
            yield return new WaitForSeconds(_respawnDelay);
            _respawnRoutine = null;
            Respawn();
        }

        /// 디버그 즉시 재생성(1-11)도 같은 경로를 쓴다.
        public void Respawn()
        {
            if (_respawnRoutine != null)
            {
                StopCoroutine(_respawnRoutine);
                _respawnRoutine = null;
            }

            _hp = _maxHp;
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
