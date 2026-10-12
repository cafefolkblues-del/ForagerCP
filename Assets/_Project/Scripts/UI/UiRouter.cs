using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 창 열고 닫는 창구. 입력(I / M / ESC)을 받아 어떤 창을 띄울지 여기서만 정한다.
    /// 각 패널이 스스로 입력을 듣게 하면 단축키가 여기저기 흩어져 충돌을 못 잡는다.
    public class UiRouter : MonoBehaviour
    {
        [SerializeField] GameInput _input;
        [SerializeField] UiWindow _menuPanel;
        [SerializeField] UiWindow _inventoryPanel;
        [SerializeField] UiWindow _shopPanel;
        [SerializeField] UiWindow _skillPanel;
        [SerializeField] UiWindow _savePanel;
        [SerializeField] UiWindow _craftPanel;
        [SerializeField] UiWindow _sellPanel;
        [SerializeField] UiWindow _storagePanel;

        /// 한 번에 하나만 열리게 할지. 창이 늘어나면 기본값으로 두는 게 안전하다.
        [SerializeField] bool _exclusive = true;

        readonly List<UiWindow> _panels = new List<UiWindow>();

        public bool AnyOpen
        {
            get
            {
                for (int i = 0; i < _panels.Count; i++)
                {
                    if (_panels[i] != null && _panels[i].IsOpen) return true;
                }
                return false;
            }
        }

        void Awake()
        {
            if (_input == null) _input = FindFirstObjectByType<GameInput>();

            if (_menuPanel != null) _panels.Add(_menuPanel);
            if (_inventoryPanel != null) _panels.Add(_inventoryPanel);
            if (_shopPanel != null) _panels.Add(_shopPanel);
            if (_skillPanel != null) _panels.Add(_skillPanel);
            if (_savePanel != null) _panels.Add(_savePanel);
            if (_craftPanel != null) _panels.Add(_craftPanel);
            if (_sellPanel != null) _panels.Add(_sellPanel);
            if (_storagePanel != null) _panels.Add(_storagePanel);
        }

        void OnEnable()
        {
            for (int i = 0; i < _panels.Count; i++) _panels[i].OpenedChanged += OnPanelChanged;
            PushInputState();

            if (_input == null) return;
            _input.InventoryToggled += ToggleInventory;
            _input.CancelPressed += OnCancel;
        }

        void OnDisable()
        {
            for (int i = 0; i < _panels.Count; i++)
            {
                if (_panels[i] != null) _panels[i].OpenedChanged -= OnPanelChanged;
            }

            // 꺼질 때 막아둔 채로 두면 입력이 영영 안 돌아온다.
            if (_input != null) _input.GameplayBlocked = false;

            if (_input == null) return;
            _input.InventoryToggled -= ToggleInventory;
            _input.CancelPressed -= OnCancel;
        }

        void OnPanelChanged(UiWindow panel) => PushInputState();

        /// 창이 하나라도 떠 있으면 게임 입력을 끊는다. 입력이 UI를 묻는 게 아니라
        /// 창을 아는 쪽이 밀어 넣는다 — 그래야 아는 곳이 여기 하나로 남는다.
        void PushInputState()
        {
            if (_input != null) _input.GameplayBlocked = AnyOpen;
        }

        public void ToggleInventory() => TogglePanel(_inventoryPanel);

        public void OpenShop() => OpenPanel(_shopPanel);
        public void OpenSkills() => OpenPanel(_skillPanel);
        public void OpenSaves() => OpenPanel(_savePanel);

        /// ESC 한 번에 다 닫아버리면 메뉴에서 들어간 화면을 빠져나올 때마다 처음부터 다시 열어야 한다.
        /// 하위 화면이 열려 있으면 메뉴로 돌아가고, 메뉴만 있으면 그때 닫는다.
        void OnCancel()
        {
            if (AnyOpenExcept(_menuPanel))
            {
                // 한 번에 하나만 여는 설정이면 OpenPanel이 나머지를 알아서 닫는다.
                OpenPanel(_menuPanel);
                if (!_exclusive) CloseAllExcept(_menuPanel);
                return;
            }

            TogglePanel(_menuPanel);
        }

        bool AnyOpenExcept(UiWindow keep)
        {
            for (int i = 0; i < _panels.Count; i++)
            {
                if (_panels[i] != null && _panels[i] != keep && _panels[i].IsOpen) return true;
            }
            return false;
        }

        void CloseAllExcept(UiWindow keep)
        {
            for (int i = 0; i < _panels.Count; i++)
            {
                if (_panels[i] != null && _panels[i] != keep) _panels[i].Close();
            }
        }

        public void TogglePanel(UiWindow panel)
        {
            if (panel == null) return;

            if (panel.IsOpen) panel.Close();
            else OpenPanel(panel);
        }

        public void OpenPanel(UiWindow panel)
        {
            if (panel == null) return;

            if (_exclusive)
            {
                for (int i = 0; i < _panels.Count; i++)
                {
                    if (_panels[i] != null && _panels[i] != panel) _panels[i].Close();
                }
            }

            panel.Open();
        }

        public void CloseAll()
        {
            for (int i = 0; i < _panels.Count; i++)
            {
                if (_panels[i] != null) _panels[i].Close();
            }
        }
    }
}
