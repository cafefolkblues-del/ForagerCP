using UnityEngine;

namespace ForagerCP
{
    /// I(인벤토리) / M(맵) 입력 경로만 미리 파둔 stub.
    /// MVP는 광물 1종이라 인벤토리가 불필요하고(1-6) 맵도 사양에 없다.
    /// 확장기능 2-4(인벤 용량 제약)와 맵 UI가 붙는 지점 = 아래 두 메서드.
    public class UiToggleStub : MonoBehaviour
    {
        [SerializeField] GameInput _input;
        [SerializeField] bool _logToggles = true;

        void Awake()
        {
            if (_input == null) _input = GetComponent<GameInput>();
        }

        void OnEnable()
        {
            _input.InventoryToggled += OnInventoryToggled;
            _input.MapToggled += OnMapToggled;
        }

        void OnDisable()
        {
            _input.InventoryToggled -= OnInventoryToggled;
            _input.MapToggled -= OnMapToggled;
        }

        void OnInventoryToggled()
        {
            if (_logToggles) Debug.Log("[stub] 인벤토리 토글 — MVP 미구현 (확장 2-4 자리)");
        }

        void OnMapToggled()
        {
            if (_logToggles) Debug.Log("[stub] 맵 토글 — MVP 미구현");
        }
    }
}
