using UnityEngine;

namespace ForagerCP
{
    /// 놓인 블럭이 '칸과 종류' 말고 더 들고 있는 상태. 보관함 내용이 첫 사례다.
    ///
    /// 별도 ISaveable로 빼지 않은 이유: 그러면 블럭 복원과 내용 복원이 서로 다른 저장 항목이 되고,
    /// 복원 순서가 보장되지 않아 '아직 없는 블럭에 내용을 넣는' 경우가 생긴다.
    /// 블럭과 그 내용은 한 덩어리로 저장·복원돼야 한다.
    public interface IBlockState
    {
        string CaptureState();
        void RestoreState(string payload);
    }

    /// 플레이어가 놓은 것이라는 표식. 무엇으로 놓았는지도 같이 든다.
    ///
    /// 맵에 원래 박혀 있던 구조물과 가르기 위해 필요하다 — GridPlacement만으로는 둘이 구분되지 않는다.
    /// 저장은 이 표식을 달고 있는 것만 훑는다.
    public class PlacedBlock : MonoBehaviour
    {
        [SerializeField] ItemDefinition _source;

        public ItemDefinition Source => _source;

        public void Setup(ItemDefinition source) => _source = source;

        /// 이 블럭이 추가 상태를 들고 있으면 그 글자, 없으면 빈 문자열.
        public string CaptureState()
            => TryGetComponent(out IBlockState state) ? state.CaptureState() : string.Empty;

        public void RestoreState(string payload)
        {
            if (string.IsNullOrEmpty(payload)) return;
            if (TryGetComponent(out IBlockState state)) state.RestoreState(payload);
        }
    }
}
