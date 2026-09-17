using UnityEngine;

namespace ForagerCP
{
    /// 사양 1-4의 고정 처리 순서를 한 곳에서 보장한다.
    /// (광물 파괴는 노드가, 광물+1 → 경험치+1 → 레벨판정 → UI갱신은 여기가.)
    /// UI 갱신(6,7)은 각 데이터의 Changed 이벤트가 담당하므로 호출 순서가 곧 갱신 순서다.
    public class HarvestRewardService : MonoBehaviour
    {
        [SerializeField] MineralWallet _minerals;
        [SerializeField] PlayerLevel _level;

        MineralNode[] _nodes;

        void OnEnable()
        {
            // 맵이 고정 배치(절차적 생성 금지)라 시작 시 한 번 수집하면 충분하다.
            // 노드는 파괴가 아니라 렌더러만 끄는 방식이라 참조가 끝까지 유효함.
            _nodes = FindObjectsByType<MineralNode>(FindObjectsSortMode.None);
            for (int i = 0; i < _nodes.Length; i++) _nodes[i].Harvested += OnHarvested;
        }

        void OnDisable()
        {
            if (_nodes == null) return;
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i] != null) _nodes[i].Harvested -= OnHarvested;
            }
        }

        void OnHarvested(MineralNode node)
        {
            _minerals.Add(node.MineralReward);
            _level.AddExp(node.ExpReward);
        }
    }
}
