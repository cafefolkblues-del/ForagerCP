using UnityEngine;

namespace ForagerCP
{
    /// 광물 한 종류의 모든 수치. 새 광물 = 이 에셋 하나 추가 + 팔레트 등록, 코드 수정 없음.
    [CreateAssetMenu(menuName = "ForagerCP/광물", fileName = "Mineral")]
    public class MineralDefinition : ItemDefinition
    {
        [Header("광물 수치")]
        [SerializeField] int _maxHp = 30;
        [SerializeField] int _harvestYield = 1;
        [SerializeField] int _expReward = 1;
        [SerializeField] float _respawnDelay = 10f;

        // 색은 결국 아트 에셋(머티리얼/텍스처)에서 맞추므로 정의에서 들지 않는다.
        // 임시 도형 시절의 틴트가 필요해지면 되살릴 것.
        // [SerializeField] Color _tint = new Color(0.35f, 0.65f, 0.95f);
        // public Color Tint => _tint;

        public int MaxHp => Mathf.Max(1, _maxHp);
        public int HarvestYield => Mathf.Max(1, _harvestYield);
        public int ExpReward => Mathf.Max(0, _expReward);
        public float RespawnDelay => Mathf.Max(0f, _respawnDelay);
    }
}
