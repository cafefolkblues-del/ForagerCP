using UnityEngine;

namespace ForagerCP
{
    /// 몬스터 처치 보상을 한 곳에서 처리한다(광물 쪽 HarvestRewardService와 같은 구조).
    /// 드랍 아이템은 기획 미확정이라 지금은 경험치만 준다 — 확정되면 OnMonsterDied 한 곳만 고치면 된다.
    public class CombatRewardService : MonoBehaviour
    {
        [SerializeField] PlayerLevel _level;

        Monster[] _monsters;

        void OnEnable()
        {
            if (_level == null) _level = FindFirstObjectByType<PlayerLevel>();

            // 맵이 고정 배치라 시작 시 한 번 수집하면 된다.
            // 몬스터도 광물처럼 죽어도 오브젝트가 남아서 참조가 계속 유효하다.
            _monsters = FindObjectsByType<Monster>(FindObjectsSortMode.None);
            for (int i = 0; i < _monsters.Length; i++) _monsters[i].Died += OnMonsterDied;
        }

        void OnDisable()
        {
            if (_monsters == null) return;
            for (int i = 0; i < _monsters.Length; i++)
            {
                if (_monsters[i] != null) _monsters[i].Died -= OnMonsterDied;
            }
        }

        void OnMonsterDied(Monster monster)
        {
            if (monster.Definition == null || _level == null) return;
            _level.AddExp(monster.Definition.ExpReward);
        }
    }
}
