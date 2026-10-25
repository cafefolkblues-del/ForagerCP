using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 구역 전투 초기화(기획 C9, TODO 22). 플레이어가 싸우던 구역을 떠나거나 죽으면 그 구역 몬스터를 처음 상태로 되돌린다.
    ///
    /// - 죽었을 때: 그 구역 전원을 즉시 스폰 자리·최대 체력으로. 죽은 몹도 되살린다(플레이어는 리스폰 지점에서 다시 시작하니 보이지 않는다).
    /// - 구역을 나갔을 때: 살아 있는 몹은 집까지 걸어서 돌아가며 회복하고(MonsterAI 복귀 규칙), 죽은 몹은 그 자리에서 되살린다.
    ///
    /// "새로 만든다"가 아니라 "되돌린다"다 — 지우고 다시 세우면 칸 좌표로 알아보는 죽음 기록(MonsterDeaths)과
    /// 체력바 같은 구독자가 끊긴다. 그래서 Monster.Respawn과 MonsterAI의 초기화 진입점만 부른다.
    ///
    /// 몬스터를 스포너가 아니라 "자기 구역"(MonsterAI.HomeRegion)으로 묶는다. 스포너로 세운 몹과
    /// 디자이너가 직접 놓은 몹을 같은 규칙으로 다루려고.
    public class RegionCombatReset : MonoBehaviour
    {
        [SerializeField] RegionTracker _tracker;
        [SerializeField] PlayerHealth _player;

        /// 되돌린 직후 (구역, 플레이어가 죽어서인가). 페이드·알림 같은 연출이 들을 자리다 — 지금은 아무도 듣지 않는다.
        public event Action<RegionDefinition, bool> RegionReset;

        RegionDefinition _lastRegion;
        readonly List<MonsterAI> _buffer = new List<MonsterAI>();

        void Awake()
        {
            if (_tracker == null) _tracker = FindFirstObjectByType<RegionTracker>();
            if (_player == null) _player = FindFirstObjectByType<PlayerHealth>();
        }

        void OnEnable()
        {
            if (_tracker != null) _tracker.Changed += OnRegionChanged;
            if (_player != null) _player.Died += OnPlayerDied;
        }

        void OnDisable()
        {
            if (_tracker != null) _tracker.Changed -= OnRegionChanged;
            if (_player != null) _player.Died -= OnPlayerDied;
        }

        void Start() => _lastRegion = _tracker != null ? _tracker.Current : null;

        void OnRegionChanged(RegionDefinition now)
        {
            RegionDefinition left = _lastRegion;
            _lastRegion = now;
            if (left != null) ResetRegion(left, false);
        }

        /// Died는 플레이어가 리스폰 지점으로 옮겨지기 전에 나온다. 그래서 지금 서 있는 구역이 싸우던 구역이다.
        /// 뒤이어 구역이 바뀌어도, 이미 되돌린 구역은 전투 중이 아니라 두 번 되돌리지 않는다.
        void OnPlayerDied(PlayerHealth health)
        {
            RegionDefinition region = _tracker != null && _tracker.Current != null
                ? _tracker.Current
                : RegionMap.At(health.transform.position);
            if (region != null) ResetRegion(region, true);
        }

        /// instant면 전원 즉시 되돌리고, 아니면 살아 있는 몹은 걸어서 돌아가게 한다.
        /// 전투 중이 아니거나(아무도 안 싸웠다) 전멸했으면(확보) 아무것도 하지 않는다.
        public void ResetRegion(RegionDefinition region, bool instant)
        {
            _buffer.Clear();
            foreach (MonsterAI ai in FindObjectsByType<MonsterAI>(FindObjectsSortMode.None))
            {
                if (ai.HomeRegion == region) _buffer.Add(ai);
            }
            if (_buffer.Count == 0) return;

            bool anyAlive = false;
            bool engaged = false;
            for (int i = 0; i < _buffer.Count; i++)
            {
                Monster monster = _buffer[i].GetComponent<Monster>();
                if (monster.IsAlive) anyAlive = true;
                if (!monster.IsAlive || _buffer[i].InCombat || monster.CurrentHp < monster.Definition.MaxHp) engaged = true;
            }

            // 전멸 = 확보로 본다. 진짜 확보 상태(TODO 21)가 생기면 이 줄을 그쪽에 묻는 것으로 바꾼다.
            if (!anyAlive) return;
            if (!engaged) return;

            for (int i = 0; i < _buffer.Count; i++)
            {
                MonsterAI ai = _buffer[i];
                Monster monster = ai.GetComponent<Monster>();

                if (instant || !monster.IsAlive)
                {
                    monster.Respawn();
                    ai.ResetCombat();
                }
                else
                {
                    ai.BeginReturnHome();
                }
            }

            RegionReset?.Invoke(region, instant);
        }
    }
}
