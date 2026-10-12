using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 격자 위 스킬 노드 하나. 새 스킬 = 이 에셋 하나 추가 + 격자에 등록, 코드 수정 없음.
    ///
    /// 이웃을 목록으로 들지 않고 좌표만 든다. 이웃을 적어두면 격자를 한 번 고칠 때마다
    /// 주변 노드를 전부 다시 적어야 하는데, 좌표에서 계산하면 노드를 옮겨도 연결이 따라온다.
    [CreateAssetMenu(menuName = "ForagerCP/스킬", fileName = "Skill")]
    public class SkillDefinition : ScriptableObject
    {
        [SerializeField] string _displayName;

        [TextArea]
        [SerializeField] string _summary;

        [Header("격자")]
        /// 격자 좌표. 상하좌우로 1칸 떨어진 노드가 이웃이다.
        [SerializeField] Vector2Int _cell;

        [Header("표시")]
        [SerializeField] Sprite _icon;

        /// 아이콘이 아직 없을 때 칸에 쓰는 색. ItemDefinition과 같은 이유로 종류가 자기 색을 든다.
        [SerializeField] Color _uiColor = new Color(0.78f, 0.80f, 0.85f);

        [Header("비용")]
        [SerializeField] int _cost = 1;

        [Header("효과")]
        /// 비어 있어도 된다. 세부 스펙이 오기 전의 자리 표시 노드가 그렇다.
        [SerializeField] List<SkillEffect> _effects = new List<SkillEffect>();

        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public Vector2Int Cell => _cell;
        public Sprite Icon => _icon;
        public Color UiColor => _uiColor;
        public int Cost => Mathf.Max(0, _cost);
        public IReadOnlyList<SkillEffect> Effects => _effects;

        /// 적어둔 설명이 있으면 그것을, 없으면 효과들이 자기를 설명한 것을 이어 붙인다.
        public string Summary
        {
            get
            {
                if (!string.IsNullOrEmpty(_summary)) return _summary;

                var parts = new List<string>();
                for (int i = 0; i < _effects.Count; i++)
                {
                    if (_effects[i] != null) parts.Add(_effects[i].Describe());
                }
                return parts.Count == 0 ? "효과 미정" : string.Join(" / ", parts);
            }
        }

        public void Apply(SkillContext context)
        {
            for (int i = 0; i < _effects.Count; i++)
            {
                if (_effects[i] != null) _effects[i].Apply(context);
            }
        }
    }
}
