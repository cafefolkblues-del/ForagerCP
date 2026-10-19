using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 창 열고 닫는 창구. 입력(I / ESC / Q·E)을 받아 어떤 창을 띄울지 여기서만 정한다.
    /// 각 패널이 스스로 입력을 듣게 하면 단축키가 여기저기 흩어져 충돌을 못 잡는다.
    ///
    /// 창은 두 부류다.
    /// - **탭 창**(스탯 / 인벤 / 스킬 / 세이브): ESC 하나로 바로 열리고, 화살표·Q/E로 옆 탭으로 넘어간다.
    ///   예전엔 ESC → 메뉴 → 버튼 선택의 두 단계였는데, 원하는 화면까지 매번 한 번을 더 눌러야 했다.
    /// - **장소 창**(상점·제작대·판매·창고): 월드 오브젝트와 상호작용해서 열린다. 탭에 넣지 않는다 —
    ///   상점 앞이 아닌데 상점 탭으로 넘어갈 수 있으면 안 되므로.
    public class UiRouter : MonoBehaviour
    {
        [SerializeField] GameInput _input;

        [Header("탭 창 — 이 순서가 탭 순서")]
        [SerializeField] UiWindow _statsPanel;
        [SerializeField] UiWindow _inventoryPanel;
        [SerializeField] UiWindow _skillPanel;
        [SerializeField] UiWindow _savePanel;

        /// 탭 창이 떠 있는 동안만 보이는 좌우 화살표. 탭마다 따로 두면 넘길 때 깜빡인다.
        [SerializeField] GameObject _tabNavigation;

        [Header("장소 창")]
        [SerializeField] UiWindow _shopPanel;
        [SerializeField] UiWindow _craftPanel;
        [SerializeField] UiWindow _sellPanel;
        [SerializeField] UiWindow _storagePanel;

        /// 한 번에 하나만 열리게 할지. 창이 늘어나면 기본값으로 두는 게 안전하다.
        [SerializeField] bool _exclusive = true;

        readonly List<UiWindow> _panels = new List<UiWindow>();
        readonly List<UiWindow> _tabs = new List<UiWindow>();

        /// ESC로 다시 열 탭. 마지막에 보던 탭을 기억한다 — 인벤을 보다 닫았으면 다음 ESC도 인벤.
        int _lastTab;

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

        /// 지금 떠 있는 탭의 순번. 탭이 없으면 -1.
        int OpenTabIndex
        {
            get
            {
                for (int i = 0; i < _tabs.Count; i++)
                {
                    if (_tabs[i].IsOpen) return i;
                }
                return -1;
            }
        }

        void Awake()
        {
            if (_input == null) _input = FindFirstObjectByType<GameInput>();

            AddTab(_statsPanel);
            AddTab(_inventoryPanel);
            AddTab(_skillPanel);
            AddTab(_savePanel);

            AddPanel(_shopPanel);
            AddPanel(_craftPanel);
            AddPanel(_sellPanel);
            AddPanel(_storagePanel);
        }

        void AddTab(UiWindow tab)
        {
            if (tab == null) return;
            _tabs.Add(tab);
            _panels.Add(tab);
        }

        void AddPanel(UiWindow panel)
        {
            if (panel != null) _panels.Add(panel);
        }

        void OnEnable()
        {
            for (int i = 0; i < _panels.Count; i++) _panels[i].OpenedChanged += OnPanelChanged;
            Refresh();

            if (_input == null) return;
            _input.InventoryToggled += ToggleInventory;
            _input.CancelPressed += OnCancel;
            _input.TabStepped += StepTab;
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
            _input.TabStepped -= StepTab;
        }

        void OnPanelChanged(UiWindow panel)
        {
            // 어느 길로 열었든(ESC·I·화살표) 탭이 열린 순간을 기억한다. 기억하는 곳이 여기 하나라야
            // "I로 인벤을 열고 닫은 뒤 ESC"도 인벤으로 돌아온다.
            int index = _tabs.IndexOf(panel);
            if (index >= 0 && panel.IsOpen) _lastTab = index;

            Refresh();
        }

        /// 창이 하나라도 떠 있으면 게임 입력을 끊는다. 입력이 UI를 묻는 게 아니라
        /// 창을 아는 쪽이 밀어 넣는다 — 그래야 아는 곳이 여기 하나로 남는다.
        void Refresh()
        {
            if (_input != null) _input.GameplayBlocked = AnyOpen;
            if (_tabNavigation != null) _tabNavigation.SetActive(OpenTabIndex >= 0);
        }

        /// I는 언제나 인벤 탭으로 들어간다. 인벤이 떠 있을 때만 닫는다 —
        /// 스탯 탭을 보다 I를 누르면 닫히는 게 아니라 인벤으로 넘어간다.
        public void ToggleInventory() => TogglePanel(_inventoryPanel);

        public void OpenShop() => OpenPanel(_shopPanel);
        public void OpenSkills() => OpenPanel(_skillPanel);
        public void OpenSaves() => OpenPanel(_savePanel);

        /// ESC: 뭐든 떠 있으면 닫고, 아무것도 없으면 마지막에 보던 탭을 연다.
        ///
        /// 예전에는 창이 없을 때 메뉴를 열었다. 메뉴에서 다시 고르는 한 단계가 늘 끼어서
        /// 탭 화면으로 바꿨다(할로우나이트식).
        void OnCancel()
        {
            if (AnyOpen)
            {
                CloseAll();
                return;
            }

            if (_tabs.Count > 0) OpenPanel(_tabs[Mathf.Clamp(_lastTab, 0, _tabs.Count - 1)]);
        }

        /// 옆 탭으로. 끝에서 넘기면 반대쪽 끝으로 돈다. 탭이 안 떠 있으면(상점 등) 무시한다.
        public void StepTab(int direction)
        {
            int current = OpenTabIndex;
            if (current < 0 || _tabs.Count == 0) return;

            int next = ((current + direction) % _tabs.Count + _tabs.Count) % _tabs.Count;
            OpenPanel(_tabs[next]);
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
