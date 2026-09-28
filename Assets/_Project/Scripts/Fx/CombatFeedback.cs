using UnityEngine;

namespace ForagerCP
{
    /// 전투·채집의 '보이는 반응'을 한 곳에서 낸다.
    /// 팀 리뷰: "몬스터를 처치하거나 광물을 다 캤을 때 피드백이 부족하다 — 좌상단 UI만으론 부족".
    ///
    /// 각 컴포넌트가 직접 이펙트를 띄우게 하면 연출 교체 때 전부 뒤져야 한다.
    /// 데이터(Monster/MineralNode/Inventory)는 이벤트만 쏘고, 무엇을 보여줄지는 여기서만 정한다.
    public class CombatFeedback : MonoBehaviour
    {
        [SerializeField] GameObject _floatingTextPrefab;
        [SerializeField] Transform _textParent;
        [SerializeField] Camera _viewCamera;

        [Header("색")]
        [SerializeField] Color _damageColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] Color _killColor = new Color(1f, 0.45f, 0.4f);
        [SerializeField] Color _harvestColor = new Color(0.6f, 0.9f, 1f);
        [SerializeField] Color _goldColor = new Color(0.96f, 0.84f, 0.42f);
        [SerializeField] Color _playerHurtColor = new Color(1f, 0.35f, 0.35f);
        [SerializeField] Color _blockColor = new Color(0.6f, 0.8f, 1f);

        [Header("크기")]
        [SerializeField] float _normalScale = 1f;
        [SerializeField] float _bigScale = 1.45f;

        Monster[] _monsters;
        MineralNode[] _nodes;
        PlayerHealth _playerHealth;
        Inventory _inventory;
        ConverterStation _converter;
        DropService _drops;
        CombatRewardService _combatRewards;

        int _lastHpSeen;

        void OnEnable()
        {
            if (_viewCamera == null) _viewCamera = Camera.main;

            _monsters = FindObjectsByType<Monster>(FindObjectsSortMode.None);
            foreach (Monster monster in _monsters)
            {
                monster.Damaged += OnMonsterDamaged;
                monster.Died += OnMonsterDied;
            }

            _nodes = FindObjectsByType<MineralNode>(FindObjectsSortMode.None);
            foreach (MineralNode node in _nodes) node.Harvested += OnMineralHarvested;

            _playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (_playerHealth != null)
            {
                _lastHpSeen = _playerHealth.CurrentHp;
                _playerHealth.Changed += OnPlayerHealthChanged;
                _playerHealth.Blocked += OnPlayerBlocked;
            }

            _converter = FindFirstObjectByType<ConverterStation>();
            if (_converter != null) _converter.Converted += OnConverted;

            _combatRewards = FindFirstObjectByType<CombatRewardService>();
            _drops = FindFirstObjectByType<DropService>();
            if (_drops != null) _drops.Picked += OnPicked;
        }

        void OnDisable()
        {
            if (_monsters != null)
            {
                foreach (Monster monster in _monsters)
                {
                    if (monster == null) continue;
                    monster.Damaged -= OnMonsterDamaged;
                    monster.Died -= OnMonsterDied;
                }
            }

            if (_nodes != null)
            {
                foreach (MineralNode node in _nodes)
                {
                    if (node != null) node.Harvested -= OnMineralHarvested;
                }
            }

            if (_playerHealth != null)
            {
                _playerHealth.Changed -= OnPlayerHealthChanged;
                _playerHealth.Blocked -= OnPlayerBlocked;
            }

            if (_converter != null) _converter.Converted -= OnConverted;
            if (_drops != null) _drops.Picked -= OnPicked;
        }

        // ---------------- 전투 ----------------

        void OnMonsterDamaged(Monster monster, int amount, GameObject source) =>
            Popup(monster.transform.position, "-" + amount, _damageColor, _normalScale);

        void OnMonsterDied(Monster monster)
        {
            string label = monster.Definition != null ? monster.Definition.DisplayName + " 처치" : "처치";
            Popup(monster.transform.position, label, _killColor, _bigScale);

            // 처치 경험치가 꺼져 있으면 숫자도 띄우지 않는다 — 안 들어오는 보상을 보여주면 거짓말이 된다.
            if (_combatRewards != null && !_combatRewards.GrantsExp) return;
            if (monster.Definition != null && monster.Definition.ExpReward > 0)
                Popup(monster.transform.position, "EXP +" + monster.Definition.ExpReward, _harvestColor, _normalScale);
        }

        void OnPlayerHealthChanged(PlayerHealth health)
        {
            int delta = health.CurrentHp - _lastHpSeen;
            _lastHpSeen = health.CurrentHp;

            // 리스폰으로 체력이 꽉 차는 것까지 숫자로 띄우면 시끄럽다. 피해만 보여준다.
            if (delta >= 0) return;
            Popup(health.transform.position, delta.ToString(), _playerHurtColor, _normalScale);
        }

        void OnPlayerBlocked(PlayerHealth health) =>
            Popup(health.transform.position, "막음", _blockColor, _normalScale);

        // ---------------- 채집 / 경제 ----------------

        /// 캔 순간에는 자원이 바닥에 떨어질 뿐이라 "얻었다"고 쓰면 거짓말이 된다.
        /// 캘 때는 경험치만 알리고, 획득 표시는 실제로 주울 때 띄운다.
        void OnMineralHarvested(MineralNode node)
        {
            if (node.Definition == null || node.Definition.ExpReward <= 0) return;
            Popup(node.transform.position, "EXP +" + node.Definition.ExpReward, _harvestColor, _normalScale);
        }

        void OnPicked(ItemDefinition definition, int amount, Vector3 position)
        {
            if (definition == null) return;
            Popup(position, definition.DisplayName + " +" + amount, definition.UiColor, _bigScale);
        }

        void OnConverted(int count, int gold) =>
            Popup(_converter.transform.position, "+" + gold + " G", _goldColor, _bigScale);

        // ---------------- 공통 ----------------

        void Popup(Vector3 worldPosition, string text, Color color, float scale)
        {
            if (_floatingTextPrefab == null || _textParent == null) return;

            GameObject instance = PoolManager.Spawn(_floatingTextPrefab, Vector3.zero, Quaternion.identity, _textParent);
            if (instance == null) return;

            var floating = instance.GetComponent<FloatingText>();
            if (floating != null) floating.Show(worldPosition, text, color, scale, _viewCamera);
        }
    }
}
