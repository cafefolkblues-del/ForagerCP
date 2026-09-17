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

        MineralNode[] _nodes;

        void OnEnable()
        {
            if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
            if (_level == null) _level = FindFirstObjectByType<PlayerLevel>();

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
            MineralDefinition definition = node.Definition;
            if (definition == null) return;

            int accepted = _inventory.Add(definition, definition.HarvestYield);

            // 넘친 분 = 확장 2-4(인벤 꽉 참 → 바닥 드랍)가 들어올 자리.
            // 규칙이 미확정이라 지금은 알리기만 하고 버린다.
            int overflow = definition.HarvestYield - accepted;
            if (overflow > 0) Debug.LogWarning($"인벤 초과로 {definition.DisplayName} {overflow}개 버려짐", this);

            _level.AddExp(definition.ExpReward);
        }
    }
}
