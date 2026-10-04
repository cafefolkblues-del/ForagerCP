using UnityEngine;

namespace ForagerCP
{
    /// 조건이 들여다볼 플레이어 상태 묶음. 조건마다 필요한 것이 달라서 한 번에 넘긴다.
    /// 조건이 Inventory나 GoldWallet을 직접 찾아다니면 테스트도 교체도 어려워진다.
    public class RegionContext
    {
        public PlayerLevel Level;
        public Inventory Inventory;
        public GoldWallet Gold;
        public ProgressFlags Flags;
    }

    /// 지역에 들어갈 조건 하나.
    ///
    /// 기획이 어떤 조건을 들고 올지 모르는 상태에서 만들 수 있는 최선의 모양이다.
    /// 조건 종류가 늘어도 이 클래스를 상속한 에셋 하나를 더 만들면 끝 —
    /// 지역 에셋도, 문도, UI도 건드리지 않는다.
    public abstract class RegionRequirement : ScriptableObject
    {
        public abstract bool IsMet(RegionContext context);

        /// 화면에 그대로 띄울 문구. "레벨 5 필요"처럼 조건이 자기를 설명하게 둬야
        /// 조건이 늘어날 때 UI 쪽에 if가 쌓이지 않는다.
        public abstract string Describe();

        /// 치르고 들어가는 종류(골드 지불, 열쇠 소모)는 여기서 실제로 뺀다.
        /// 그냥 자격만 보는 조건(레벨 등)은 아무것도 하지 않는다 — 그래서 기본 구현이 비어 있다.
        public virtual void Consume(RegionContext context) { }

        /// 대가가 있는 조건인지. 참이면 조건이 저절로 채워져도 멋대로 열지 않는다
        /// — 골드가 모였다고 플레이어 의사 없이 땅을 사버리면 안 되기 때문.
        public virtual bool HasCost => false;
    }
}
