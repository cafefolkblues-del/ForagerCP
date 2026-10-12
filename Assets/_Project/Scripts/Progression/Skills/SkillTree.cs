using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 스킬 격자 전체. 디자이너가 에셋으로 편집한다.
    ///
    /// 어느 칸이 어느 스킬인지만 안다. 무엇을 배웠는지는 런타임 상태라 SkillBoard가 든다 —
    /// 에셋에 넣으면 에디터에서 배운 것이 저장돼 다음 플레이에 그대로 남는다.
    [CreateAssetMenu(menuName = "ForagerCP/스킬 격자", fileName = "SkillTree")]
    public class SkillTree : ScriptableObject
    {
        [SerializeField] List<SkillDefinition> _skills = new List<SkillDefinition>();

        /// 아무것도 배우지 않은 상태에서 고를 수 있는 것들(확정 스펙: 4개).
        /// 목록을 따로 두는 이유는 '중앙'을 좌표로 못 박으면 격자를 옮길 때마다 코드가 따라가야 하기 때문.
        [SerializeField] List<SkillDefinition> _starting = new List<SkillDefinition>();

        public IReadOnlyList<SkillDefinition> Skills => _skills;
        public IReadOnlyList<SkillDefinition> Starting => _starting;

        static readonly Vector2Int[] Around =
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
        };

        public SkillDefinition At(Vector2Int cell)
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                if (_skills[i] != null && _skills[i].Cell == cell) return _skills[i];
            }
            return null;
        }

        /// 상하좌우로 맞닿은 노드들. 빈 칸은 건너뛴다.
        public void CollectNeighbours(SkillDefinition skill, List<SkillDefinition> into)
        {
            into.Clear();
            if (skill == null) return;

            for (int i = 0; i < Around.Length; i++)
            {
                SkillDefinition found = At(skill.Cell + Around[i]);
                if (found != null) into.Add(found);
            }
        }

        public bool IsStarting(SkillDefinition skill) => skill != null && _starting.Contains(skill);

        /// 격자가 차지하는 범위. 화면이 칸을 어디에 그릴지 정할 때 쓴다.
        public RectInt Bounds
        {
            get
            {
                if (_skills.Count == 0) return new RectInt(0, 0, 1, 1);

                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                for (int i = 0; i < _skills.Count; i++)
                {
                    if (_skills[i] == null) continue;

                    Vector2Int cell = _skills[i].Cell;
                    minX = Mathf.Min(minX, cell.x);
                    minY = Mathf.Min(minY, cell.y);
                    maxX = Mathf.Max(maxX, cell.x);
                    maxY = Mathf.Max(maxY, cell.y);
                }
                return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
        }
    }
}
