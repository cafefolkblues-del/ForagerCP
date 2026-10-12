using System;
using UnityEngine;

namespace ForagerCP
{
    /// 놓아둔 보관함 하나. 자기 칸만 들고, 화면은 모른다.
    ///
    /// 구역 사이에 내용이 섞이지 않는다 — 보관함마다 자기 ItemContainer를 들고 있고,
    /// 구역당 하나만 놓을 수 있으므로(ItemDefinition.MaxPerRegion) 결과가 '구역별 창고'가 된다.
    /// 구역을 키로 하는 공용 저장소로 만들지 않은 이유: 그러면 보관함을 부수고 다시 놓았을 때
    /// 내용이 어디 있어야 하는지가 애매해지고, 저장도 블럭과 따로 떨어진다.
    ///
    /// 내용 저장은 ISaveable이 아니라 IBlockState다. 블럭과 한 덩어리로 저장돼야
    /// 복원 순서에 기대지 않는다(PlacedBlock 주석 참고).
    public class StorageBox : MonoBehaviour, IInteractable, IBlockState
    {
        /// 프리팹에 박아두는 에셋 참조. 씬 오브젝트가 아니라 에셋이라 프리팹이 직접 들 수 있다.
        [SerializeField] SaveCatalog _catalog;

        [SerializeField] int _slotCount = 20;

        public event Action<StorageBox> OpenRequested;

        ItemContainer _items;

        public ItemContainer Items
        {
            get
            {
                if (_items != null) return _items;

                _items = new ItemContainer(_slotCount);
                return _items;
            }
        }

        public Transform Transform => transform;

        public string Label
        {
            get
            {
                RegionDefinition region = RegionMap.At(transform.position);
                string where = region != null ? $"{region.DisplayName} " : string.Empty;
                return $"{where}보관함 열기 ({Items.UsedSlots}/{Items.SlotCount})";
            }
        }

        public void Interact() => OpenRequested?.Invoke(this);

        // ---------------- 저장 ----------------

        public string CaptureState() => Items.Capture();

        public void RestoreState(string payload)
        {
            if (_catalog == null)
            {
                // 명부가 없으면 이름으로 에셋을 되찾을 길이 없어 내용이 통째로 사라진다.
                // 조용히 비우면 '세이브가 왜 비었지'를 한참 찾게 되므로 여기서 외친다.
                Debug.LogWarning($"{name}: SaveCatalog가 비어 보관함 내용을 되돌릴 수 없습니다", this);
                return;
            }

            Items.Restore(payload, _catalog);
        }
    }
}
