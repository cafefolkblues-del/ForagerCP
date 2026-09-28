using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 사양 1-4의 고정 처리 순서를 한 곳에서 보장한다.
    /// (광물 파괴는 노드가, 광물 지급 → 경험치 → 레벨판정 → UI갱신은 여기가.)
    /// UI 갱신(6,7)은 각 데이터의 Changed 이벤트가 담당하므로 호출 순서가 곧 갱신 순서다.
    public class HarvestRewardService : MonoBehaviour
    {
        [SerializeField] Inventory _inventory;
        [SerializeField] PlayerLevel _level;
        [SerializeField] DropService _drops;

        // 배열이 아니라 List인 이유: 설치한 블럭이 런타임에 노드로 끼어든다(Track).
        readonly List<MineralNode> _nodes = new List<MineralNode>();

        void OnEnable()
        {
            if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
            if (_level == null) _level = FindFirstObjectByType<PlayerLevel>();
            if (_drops == null) _drops = FindFirstObjectByType<DropService>();

            // 맵이 고정 배치(절차적 생성 금지)라 시작 시 한 번 수집하면 충분하다.
            // 노드는 파괴가 아니라 렌더러만 끄는 방식이라 참조가 끝까지 유효함.
            foreach (MineralNode node in FindObjectsByType<MineralNode>(FindObjectsSortMode.None)) Track(node);
        }

        void OnDisable()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null) _nodes[i].Harvested -= OnHarvested;
            }
            _nodes.Clear();
        }

        /// 시작 뒤에 생긴 노드(플레이어가 설치한 블럭)를 지급 대상에 넣는다.
        /// 이걸 안 부르면 그 블럭은 캐도 아무 일이 없다 — OnEnable의 한 번 수집에 잡히지 않으므로.
        public void Track(MineralNode node)
        {
            if (node == null || _nodes.Contains(node)) return;

            node.Harvested += OnHarvested;
            _nodes.Add(node);
        }

        void OnHarvested(MineralNode node)
        {
            MineralDefinition definition = node.Definition;
            if (definition == null) return;

            // 확정 스펙: 캔 자원은 인벤으로 직행하지 않고 바닥에 떨어진다. 줍는 건 F.
            // 드랍 서비스가 없는 씬(테스트 씬 등)에서는 예전처럼 인벤에 바로 넣어 기능이 죽지 않게 한다.
            if (_drops != null) _drops.Spawn(definition, definition.HarvestYield, node.transform.position);
            else _inventory.Add(definition, definition.HarvestYield);

            // 설치한 블럭은 경험치를 주지 않는다 — 놓고 캐기를 반복하는 무한 파밍을 막는 쪽은 노드가 안다.
            if (node.GrantsExp) _level.AddExp(definition.ExpReward);
        }
    }
}
