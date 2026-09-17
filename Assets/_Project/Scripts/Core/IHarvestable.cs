using UnityEngine;

namespace ForagerCP
{
    /// 좌클릭 판정의 대상. 확장기능 2-1의 몬스터는 IDamageable로 따로 받아서
    /// 같은 판정 경로를 공유하되 보상 처리는 섞이지 않게 한다.
    public interface IHarvestable
    {
        bool IsHarvestable { get; }
        Transform Transform { get; }
        void Harvest(int power);
    }
}
