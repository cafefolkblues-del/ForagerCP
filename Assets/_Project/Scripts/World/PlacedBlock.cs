using UnityEngine;

namespace ForagerCP
{
    /// 플레이어가 놓은 것이라는 표식. 무엇으로 놓았는지도 같이 든다.
    ///
    /// 맵에 원래 박혀 있던 구조물과 가르기 위해 필요하다 — GridPlacement만으로는 둘이 구분되지 않는다.
    /// 저장은 이 표식을 달고 있는 것만 훑는다.
    public class PlacedBlock : MonoBehaviour
    {
        [SerializeField] ItemDefinition _source;

        public ItemDefinition Source => _source;

        public void Setup(ItemDefinition source) => _source = source;
    }
}
