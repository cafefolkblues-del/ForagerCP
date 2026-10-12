using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 어느 몬스터가 죽어 있는지를 저장한다.
    ///
    /// 몬스터마다 ISaveable을 달지 않는 이유: 저장 파일에 칸이 마릿수만큼 생기고,
    /// 칸 이름을 뭘로 할지가 다시 문제가 된다. 한 곳이 모아서 한 칸으로 적는 편이 낫다.
    ///
    /// 식별은 GridPlacement의 칸 좌표로 한다. 프리팹에 이미 붙어 있고 칸은 겹치지 않는다
    /// (GridOccupancy가 보장). 디자이너가 몬스터를 옮기면 그 몬스터의 기록만 어긋나는데,
    /// 맵을 고친 것이니 자연스럽다.
    ///
    /// 체력이나 위치는 저장하지 않는다 — 리스폰이 없다는 것이 확정이라 '죽었다'는 사실 외에는
    /// 되살릴 의미가 없다.
    public class MonsterDeaths : MonoBehaviour, ISaveable
    {
        [Serializable]
        class SavedDeath
        {
            public int X;
            public int Y;
        }

        [Serializable]
        class Save
        {
            public List<SavedDeath> Dead = new List<SavedDeath>();
        }

        public string SaveKey => "monsterDeaths";

        public string Capture()
        {
            var save = new Save();

            foreach (Monster monster in FindObjectsByType<Monster>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (monster.IsAlive) continue;
                if (!monster.TryGetComponent(out GridPlacement placement)) continue;

                save.Dead.Add(new SavedDeath { X = placement.Cell.x, Y = placement.Cell.y });
            }

            // 찾는 순서는 보장되지 않는다. 정렬해야 같은 상태가 늘 같은 글자로 나온다.
            save.Dead.Sort((a, b) => a.Y != b.Y ? a.Y - b.Y : a.X - b.X);
            return JsonUtility.ToJson(save);
        }

        public void Restore(string payload)
        {
            var save = JsonUtility.FromJson<Save>(payload);
            if (save == null) return;

            var dead = new HashSet<Vector2Int>();
            for (int i = 0; i < save.Dead.Count; i++) dead.Add(new Vector2Int(save.Dead[i].X, save.Dead[i].Y));

            foreach (Monster monster in FindObjectsByType<Monster>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!monster.TryGetComponent(out GridPlacement placement)) continue;

                bool shouldBeDead = dead.Contains(placement.Cell);

                // 죽어 있어야 하는데 살아 있으면 조용히 눕힌다(보상도 연출도 없이).
                if (shouldBeDead) { monster.MarkDeadSilently(); continue; }

                // 살아 있어야 하는데 죽어 있으면 되살린다. 플레이 도중 불러온 경우에 필요하다.
                if (!monster.IsAlive) monster.Respawn();
            }
        }
    }
}
