using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ForagerCP
{
    /// 스킬 격자 화면. 노드는 템플릿을 복제해 만들고, 자리는 스킬이 든 셀 좌표에서 계산한다.
    ///
    /// 칸 위치를 화면 좌표로 따로 적지 않는다 — 격자 에셋에서 노드를 옮기면 화면이 알아서 따라와야
    /// 디자이너가 배치를 혼자 고칠 수 있다.
    public class SkillPanelView : MonoBehaviour
    {
        [SerializeField] SkillBoard _board;

        [Header("격자")]
        [SerializeField] RectTransform _gridParent;
        [SerializeField] RectTransform _nodeTemplate;
        [SerializeField] RectTransform _linkTemplate;
        [SerializeField] float _cellSize = 96f;

        [Header("고른 것")]
        [SerializeField] TMP_Text _pointsLabel;
        [SerializeField] TMP_Text _nameLabel;
        [SerializeField] TMP_Text _summaryLabel;
        [SerializeField] Button _learnButton;
        [SerializeField] TMP_Text _learnLabel;

        [Header("색")]
        [SerializeField] Color _lockedIcon = new Color(1f, 1f, 1f, 0.10f);

        /// 칸 바탕. 잠긴 칸은 바탕까지 어둡게 해야 한눈에 걸러진다 — 테두리 유무만으로는 약하다.
        [SerializeField] Color _learnedBack = new Color(0.20f, 0.23f, 0.31f, 1f);
        [SerializeField] Color _availableBack = new Color(0.14f, 0.16f, 0.22f, 1f);
        [SerializeField] Color _lockedBack = new Color(0.08f, 0.09f, 0.12f, 0.75f);
        [SerializeField] Color _learnedBorder = new Color(1f, 0.86f, 0.35f, 1f);
        [SerializeField] Color _availableBorder = new Color(0.92f, 0.95f, 1f, 0.75f);
        [SerializeField] Color _linkOn = new Color(1f, 0.86f, 0.35f, 0.85f);
        [SerializeField] Color _linkOff = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] Color _refusedText = new Color(1f, 0.62f, 0.55f);
        [SerializeField] Color _normalText = new Color(0.86f, 0.88f, 0.93f);

        readonly List<SkillDefinition> _skills = new List<SkillDefinition>();
        readonly List<RectTransform> _nodes = new List<RectTransform>();
        readonly List<Image> _backs = new List<Image>();
        readonly List<Image> _icons = new List<Image>();
        readonly List<Image> _borders = new List<Image>();

        /// 선 하나당 (이미지, 양 끝 스킬). 둘 다 배웠을 때만 밝힌다.
        readonly List<(Image image, SkillDefinition a, SkillDefinition b)> _links =
            new List<(Image, SkillDefinition, SkillDefinition)>();

        readonly List<SkillDefinition> _neighbours = new List<SkillDefinition>();

        SkillDefinition _selected;

        /// 거부 사유를 띄운 상태. 다음 Redraw가 설명으로 되돌린다.
        bool _showingRefusal;

        void Awake()
        {
            if (_board == null) _board = FindFirstObjectByType<SkillBoard>();
            if (_nodeTemplate != null) _nodeTemplate.gameObject.SetActive(false);
            if (_linkTemplate != null) _linkTemplate.gameObject.SetActive(false);
            if (_learnButton != null) _learnButton.onClick.AddListener(LearnSelected);
        }

        void OnEnable()
        {
            if (_board == null) return;

            _board.Changed += OnBoardChanged;
            _board.Refused += OnRefused;

            Build();
            Redraw();
        }

        void OnDisable()
        {
            if (_board == null) return;

            _board.Changed -= OnBoardChanged;
            _board.Refused -= OnRefused;
        }

        void OnBoardChanged(SkillBoard board) => Redraw();

        // ------------------------------------------------------------------ 짓기

        void Build()
        {
            if (_nodes.Count > 0 || _board == null || _board.Tree == null) return;
            if (_nodeTemplate == null || _gridParent == null) return;

            _skills.Clear();
            foreach (SkillDefinition skill in _board.Tree.Skills)
            {
                if (skill != null) _skills.Add(skill);
            }

            BuildLinks();

            // 노드를 선보다 나중에 만들어야 선이 노드 뒤로 깔린다.
            for (int i = 0; i < _skills.Count; i++) BuildNode(_skills[i]);
        }

        void BuildLinks()
        {
            if (_linkTemplate == null) return;

            for (int i = 0; i < _skills.Count; i++)
            {
                SkillDefinition skill = _skills[i];
                _board.Tree.CollectNeighbours(skill, _neighbours);

                for (int n = 0; n < _neighbours.Count; n++)
                {
                    SkillDefinition other = _neighbours[n];

                    // 한 쌍에 선 하나만. 좌표 순서로 한쪽만 그리게 걸러낸다.
                    if (other.Cell.x < skill.Cell.x || other.Cell.y < skill.Cell.y) continue;

                    RectTransform link = Instantiate(_linkTemplate, _gridParent);
                    link.gameObject.SetActive(true);
                    link.name = "Link";

                    Vector2 from = Place(skill.Cell);
                    Vector2 to = Place(other.Cell);
                    link.anchoredPosition = (from + to) * 0.5f;

                    bool horizontal = Mathf.Abs(to.x - from.x) > Mathf.Abs(to.y - from.y);
                    float thickness = link.sizeDelta.y;
                    float span = Mathf.Max(8f, _cellSize - _nodeTemplate.sizeDelta.x + 10f);
                    link.sizeDelta = horizontal
                        ? new Vector2(span, thickness)
                        : new Vector2(thickness, span);

                    _links.Add((link.GetComponent<Image>(), skill, other));
                }
            }
        }

        void BuildNode(SkillDefinition skill)
        {
            RectTransform node = Instantiate(_nodeTemplate, _gridParent);
            node.gameObject.SetActive(true);
            node.name = skill.DisplayName;
            node.anchoredPosition = Place(skill.Cell);

            _nodes.Add(node);
            _backs.Add(node.GetComponent<Image>());
            _icons.Add(node.Find("Icon").GetComponent<Image>());

            Image border = node.Find("Border").GetComponent<Image>();
            _borders.Add(border);

            // 람다가 바깥 변수를 잡으면 마지막 스킬만 가리키게 된다. 지역 변수로 복사해 고정한다.
            SkillDefinition captured = skill;
            node.GetComponent<Button>().onClick.AddListener(() => Select(captured));
        }

        /// 셀 좌표를 화면 좌표로. 격자 한가운데가 패널 가운데에 오게 평균만큼 민다.
        Vector2 Place(Vector2Int cell)
        {
            RectInt bounds = _board.Tree.Bounds;
            float centerX = bounds.xMin + (bounds.width - 1) * 0.5f;
            float centerY = bounds.yMin + (bounds.height - 1) * 0.5f;

            return new Vector2((cell.x - centerX) * _cellSize, (cell.y - centerY) * _cellSize);
        }

        // ------------------------------------------------------------------ 그리기

        void Redraw()
        {
            if (_board == null) return;

            if (_pointsLabel != null) _pointsLabel.text = $"스킬 포인트: {_board.Points}";

            for (int i = 0; i < _nodes.Count; i++)
            {
                SkillDefinition skill = _skills[i];
                bool learned = _board.IsLearned(skill);
                bool available = _board.IsAvailable(skill);

                _backs[i].color = learned ? _learnedBack : available ? _availableBack : _lockedBack;

                _icons[i].sprite = skill.Icon;
                _icons[i].color = learned ? skill.UiColor
                    : available ? skill.UiColor * 0.55f
                    : _lockedIcon;

                // 테두리는 '손댈 수 있는가'를 알린다. 잠긴 노드는 테두리도 없어 한눈에 걸러진다.
                _borders[i].enabled = learned || available || skill == _selected;
                _borders[i].color = learned ? _learnedBorder
                    : skill == _selected ? Color.white
                    : _availableBorder;

                _nodes[i].localScale = skill == _selected ? Vector3.one * 1.1f : Vector3.one;
            }

            for (int i = 0; i < _links.Count; i++)
            {
                bool lit = _board.IsLearned(_links[i].a) && _board.IsLearned(_links[i].b);
                _links[i].image.color = lit ? _linkOn : _linkOff;
            }

            if (_showingRefusal) _showingRefusal = false;
            else DrawSelection();
        }

        void DrawSelection()
        {
            if (_nameLabel == null) return;

            if (_selected == null)
            {
                _nameLabel.text = "스킬을 고르세요";
                if (_summaryLabel != null) { _summaryLabel.text = string.Empty; }
                SetLearnButton(false, "배우기");
                return;
            }

            _nameLabel.text = _selected.DisplayName;

            if (_summaryLabel != null)
            {
                _summaryLabel.color = _normalText;
                _summaryLabel.text = _selected.Summary;
            }

            if (_board.IsLearned(_selected))
            {
                SetLearnButton(false, "배움");
                return;
            }

            bool can = _board.CanLearn(_selected, out _);
            SetLearnButton(can, $"배우기 ({_selected.Cost})");
        }

        void SetLearnButton(bool on, string text)
        {
            if (_learnButton != null) _learnButton.interactable = on;
            if (_learnLabel != null) _learnLabel.text = text;
        }

        // ------------------------------------------------------------------ 조작

        void Select(SkillDefinition skill)
        {
            _selected = skill;
            Redraw();
        }

        void LearnSelected()
        {
            if (_selected == null) return;
            _board.TryLearn(_selected);
        }

        /// 못 배운 이유를 설명 자리에 띄운다. 조건이 자기를 설명하므로 여기엔 사유별 분기가 없다.
        void OnRefused(SkillDefinition skill, string reason)
        {
            if (_summaryLabel == null) return;

            _summaryLabel.color = _refusedText;
            _summaryLabel.text = reason;
            _showingRefusal = true;
        }
    }
}
