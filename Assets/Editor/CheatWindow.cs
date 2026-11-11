using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ForagerCP.EditorTools
{
    /// 플레이 모드에서 상태를 강제로 만드는 창. 구역·용광로·보스 검증을 손으로 재현하지 않으려고 둔다.
    ///
    /// 게임 쪽은 DebugXxx 진입점만 열어 두고, 버튼은 전부 여기 모은다.
    /// 런타임 컴포넌트(DebugCheats, F1~F5)는 빌드에서도 써야 해서 그대로 둔다.
    public class CheatWindow : EditorWindow
    {
        static readonly float[] TimeScales = { 0f, 0.5f, 1f, 2f, 5f, 10f };

        // 플레이가 끝나면 런타임 참조는 전부 끊기므로 매번 다시 찾는다(찾는 비용은 버튼 누를 때만 든다).
        PlayerHealth _health;
        GoldWallet _gold;
        PlayerLevel _level;
        Inventory _inventory;
        SkillBoard _skills;
        RegionAccess _regions;

        List<ItemDefinition> _items = new List<ItemDefinition>();
        List<MonsterDefinition> _monsters = new List<MonsterDefinition>();
        readonly List<RegionDefinition> _regionList = new List<RegionDefinition>();
        readonly List<GameObject> _summoned = new List<GameObject>();
        readonly List<GameObject> _monsterPrefabs = new List<GameObject>();

        [SerializeField] bool _openPlayer = true;
        [SerializeField] bool _openItems = true;
        [SerializeField] bool _openRegions = true;
        [SerializeField] bool _openSkills = true;
        [SerializeField] bool _openTime = true;
        [SerializeField] bool _openWorld = true;

        [SerializeField] string _itemFilter = "";
        [SerializeField] int _itemAmount = 10;
        [SerializeField] int _goldAmount = 1000;
        [SerializeField] int _expAmount = 50;
        [SerializeField] int _targetLevel = 5;
        [SerializeField] int _monsterIndex;
        [SerializeField] int _monsterCount = 1;

        string _status = "";
        Vector2 _scroll;
        Vector2 _itemScroll;

        [MenuItem("Tools/ForagerCP/치트")]
        static void Open() => GetWindow<CheatWindow>("치트");

        void OnEnable()
        {
            LoadAssets();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable() => EditorApplication.playModeStateChanged -= OnPlayModeChanged;

        // 시간 배율은 플레이가 끝나도 Time.timeScale에 남는다. 다음 플레이가 10배속으로 시작하지 않게 되돌린다.
        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingPlayMode) return;

            Time.timeScale = 1f;
            _summoned.Clear();
            _status = "";
        }

        // 체력·골드 숫자가 플레이 중에 바뀌므로 창을 계속 다시 그린다.
        void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying) Repaint();
        }

        void LoadAssets()
        {
            _items = FindAll<ItemDefinition>()
                .OrderBy(i => i.SortOrder).ThenBy(i => i.name, System.StringComparer.Ordinal).ToList();
            _monsters = FindAll<MonsterDefinition>()
                .OrderBy(m => m.name, System.StringComparer.Ordinal).ToList();

            _monsterPrefabs.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null && prefab.TryGetComponent(out Monster _)) _monsterPrefabs.Add(prefab);
            }
        }

        /// 정의에 프리팹이 없으면 스포너는 자기 기본 프리팹을 쓴다(색·수치만 정의에서 온다).
        /// 치트 창에는 스포너가 없으니, 그 정의를 쓰는 프리팹 → 아무 몬스터 프리팹 순으로 빌린다.
        GameObject PrefabFor(MonsterDefinition definition)
        {
            if (definition.Prefab != null) return definition.Prefab;

            for (int i = 0; i < _monsterPrefabs.Count; i++)
            {
                if (_monsterPrefabs[i].GetComponent<Monster>().Definition == definition) return _monsterPrefabs[i];
            }
            return _monsterPrefabs.Count > 0 ? _monsterPrefabs[0] : null;
        }

        static IEnumerable<T> FindAll<T>() where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) yield return asset;
            }
        }

        static T Find<T>(ref T cached) where T : Object
        {
            if (cached == null) cached = FindFirstObjectByType<T>();
            return cached;
        }

        void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서만 동작합니다.", MessageType.Info);
                if (GUILayout.Button("에셋 목록 다시 읽기")) LoadAssets();
                return;
            }

            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            _openPlayer = EditorGUILayout.BeginFoldoutHeaderGroup(_openPlayer, "플레이어 · 골드 · 레벨");
            if (_openPlayer) DrawPlayer();
            EditorGUILayout.EndFoldoutHeaderGroup();

            _openItems = EditorGUILayout.BeginFoldoutHeaderGroup(_openItems, "아이템");
            if (_openItems) DrawItems();
            EditorGUILayout.EndFoldoutHeaderGroup();

            _openRegions = EditorGUILayout.BeginFoldoutHeaderGroup(_openRegions, "구역");
            if (_openRegions) DrawRegions();
            EditorGUILayout.EndFoldoutHeaderGroup();

            _openSkills = EditorGUILayout.BeginFoldoutHeaderGroup(_openSkills, "스킬");
            if (_openSkills) DrawSkills();
            EditorGUILayout.EndFoldoutHeaderGroup();

            _openTime = EditorGUILayout.BeginFoldoutHeaderGroup(_openTime, "시간");
            if (_openTime) DrawTime();
            EditorGUILayout.EndFoldoutHeaderGroup();

            _openWorld = EditorGUILayout.BeginFoldoutHeaderGroup(_openWorld, "월드 · 몬스터");
            if (_openWorld) DrawWorld();
            EditorGUILayout.EndFoldoutHeaderGroup();

            EditorGUILayout.EndScrollView();
        }

        // ---------------- 플레이어 ----------------

        void DrawPlayer()
        {
            PlayerHealth health = Find(ref _health);
            if (health != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"체력 {health.CurrentHp} / {health.MaxHp}", GUILayout.Width(110));
                    if (GUILayout.Button("가득")) health.Heal(health.MaxHp);
                    if (GUILayout.Button("부활 지점으로")) health.Respawn();
                    health.DebugInvulnerable = GUILayout.Toggle(health.DebugInvulnerable, "무적", "Button", GUILayout.Width(60));
                }
            }
            else EditorGUILayout.LabelField("PlayerHealth 없음");

            GoldWallet gold = Find(ref _gold);
            if (gold != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"골드 {gold.Gold:N0}", GUILayout.Width(110));
                    _goldAmount = EditorGUILayout.IntField(_goldAmount, GUILayout.Width(70));
                    if (GUILayout.Button("더하기")) gold.Add(_goldAmount);
                    if (GUILayout.Button("빼기")) gold.TrySpend(Mathf.Min(_goldAmount, gold.Gold));
                    if (GUILayout.Button("0으로")) gold.TrySpend(gold.Gold);
                }
            }

            PlayerLevel level = Find(ref _level);
            if (level != null)
            {
                string next = level.IsMaxLevel ? "최대" : $"{level.Exp} / {level.ExpToNext}";
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Lv.{level.Level}  ({next})", GUILayout.Width(110));
                    _expAmount = EditorGUILayout.IntField(_expAmount, GUILayout.Width(70));
                    if (GUILayout.Button("경험치 더하기")) level.AddExp(_expAmount);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"최대 Lv.{level.MaxLevel}", GUILayout.Width(110));
                    _targetLevel = EditorGUILayout.IntSlider(_targetLevel, 1, Mathf.Max(1, level.MaxLevel));
                    if (GUILayout.Button("레벨 맞추기", GUILayout.Width(80))) level.DebugSetLevel(_targetLevel);
                }
            }
        }

        // ---------------- 아이템 ----------------

        void DrawItems()
        {
            Inventory inventory = Find(ref _inventory);
            if (inventory == null)
            {
                EditorGUILayout.LabelField("Inventory 없음");
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"가방 {inventory.UsedSlots} / {inventory.SlotCount}칸", GUILayout.Width(110));
                if (GUILayout.Button("칸 +10")) inventory.ExpandSlots(10);
                if (GUILayout.Button("비우기")) inventory.Clear();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _itemFilter = EditorGUILayout.TextField("찾기", _itemFilter);
                EditorGUILayout.LabelField("개수", GUILayout.Width(30));
                _itemAmount = Mathf.Max(1, EditorGUILayout.IntField(_itemAmount, GUILayout.Width(50)));
            }

            List<ItemDefinition> shown = _items.Where(Matches).ToList();
            if (GUILayout.Button($"보이는 {shown.Count}종 전부 지급")) GiveAll(inventory, shown);

            _itemScroll = EditorGUILayout.BeginScrollView(_itemScroll, GUILayout.MaxHeight(220));
            for (int i = 0; i < shown.Count; i++)
            {
                ItemDefinition item = shown[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{item.DisplayName}  ({item.name})");
                    EditorGUILayout.LabelField(inventory.CountOf(item).ToString(), GUILayout.Width(40));
                    if (GUILayout.Button("지급", GUILayout.Width(50))) Give(inventory, item, _itemAmount);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        bool Matches(ItemDefinition item)
        {
            if (item == null) return false;
            if (string.IsNullOrEmpty(_itemFilter)) return true;

            return item.name.IndexOf(_itemFilter, System.StringComparison.OrdinalIgnoreCase) >= 0
                   || item.DisplayName.IndexOf(_itemFilter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void Give(Inventory inventory, ItemDefinition item, int amount)
        {
            // Add는 '담긴 수'를 돌려준다. 넘친 분은 버린다(치트라 바닥에 떨굴 이유가 없다).
            int left = amount - inventory.Add(item, amount);
            _status = left > 0
                ? $"{item.DisplayName} {amount - left}개 지급 — 가방이 차서 {left}개 못 넣음"
                : $"{item.DisplayName} {amount}개 지급";
        }

        void GiveAll(Inventory inventory, List<ItemDefinition> items)
        {
            int failed = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (inventory.Add(items[i], _itemAmount) < _itemAmount) failed++;
            }
            _status = failed > 0
                ? $"{items.Count}종 지급 — {failed}종은 가방이 차서 일부만 들어감 ('칸 +10' 후 다시)"
                : $"{items.Count}종을 {_itemAmount}개씩 지급";
        }

        // ---------------- 구역 ----------------

        void DrawRegions()
        {
            RegionAccess access = Find(ref _regions);
            if (access == null)
            {
                EditorGUILayout.LabelField("RegionAccess 없음");
                return;
            }

            // 점령·확보 단계는 #25에서 들어온다. 지금은 잠금/열림만 있다.
            RegionMap.CollectRegions(_regionList);
            _regionList.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            int lockable = 0;
            for (int i = 0; i < _regionList.Count; i++)
            {
                RegionDefinition region = _regionList[i];
                if (!region.IsLockable) continue;
                lockable++;

                bool open = access.IsUnlocked(region);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(region.DisplayName);
                    EditorGUILayout.LabelField(open ? "열림" : "잠김", GUILayout.Width(40));

                    using (new EditorGUI.DisabledScope(open))
                    {
                        // 정식 경로: 조건을 보고 대가(골드 등)를 치른다. 지도 테이블이 들어오기 전 개방 검증용.
                        var unlock = new GUIContent("해금", "실제 개방과 같은 경로 — 조건 확인 후 골드를 치른다");
                        if (GUILayout.Button(unlock, GUILayout.Width(50))) Unlock(access, region);

                        var force = new GUIContent("강제", "조건·대가 없이 연다");
                        if (GUILayout.Button(force, GUILayout.Width(50)) && !access.DebugUnlock(region))
                            _status = $"{region.DisplayName}: PermanentUnlock이 꺼져 있어 강제로 열 수 없습니다";
                    }
                    using (new EditorGUI.DisabledScope(!open))
                    {
                        if (GUILayout.Button("잠그기", GUILayout.Width(50))) access.DebugLock(region);
                    }
                }
            }

            if (lockable == 0) EditorGUILayout.LabelField("잠글 수 있는 구역이 없습니다");
            else if (GUILayout.Button("전부 열기"))
            {
                for (int i = 0; i < _regionList.Count; i++) access.DebugUnlock(_regionList[i]);
            }
        }

        void Unlock(RegionAccess access, RegionDefinition region)
        {
            if (!access.CanEnter(region, out RegionRequirement blocking))
            {
                _status = $"{region.DisplayName} 해금 불가 — {(blocking != null ? blocking.Describe() : "조건 미충족")}";
                return;
            }

            int goldBefore = Find(ref _gold) != null ? _gold.Gold : 0;
            access.TryUnlock(region);
            int paid = _gold != null ? goldBefore - _gold.Gold : 0;
            _status = paid > 0 ? $"{region.DisplayName} 해금 — {paid:N0}G 지불" : $"{region.DisplayName} 해금";
        }

        // ---------------- 스킬 ----------------

        void DrawSkills()
        {
            SkillBoard board = Find(ref _skills);
            if (board == null)
            {
                EditorGUILayout.LabelField("SkillBoard 없음");
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"포인트 {board.Points} (쓴 것 {board.Spent})", GUILayout.Width(150));
                if (GUILayout.Button("+1")) board.AddPoints(1);
                if (GUILayout.Button("+10")) board.AddPoints(10);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전부 배우기")) _status = $"스킬 {board.DebugLearnAll()}개 새로 배움";
                if (GUILayout.Button("배운 것 초기화")) board.ResetLearned();
            }
            EditorGUILayout.LabelField("초기화는 포인트만 돌려주고, 이미 들어간 효과는 되돌리지 않습니다.", EditorStyles.miniLabel);
        }

        // ---------------- 시간 ----------------

        void DrawTime()
        {
            Time.timeScale = EditorGUILayout.Slider("배율", Time.timeScale, 0f, 10f);

            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < TimeScales.Length; i++)
                {
                    float scale = TimeScales[i];
                    string label = scale == 0f ? "정지" : $"x{scale}";
                    if (GUILayout.Button(label)) Time.timeScale = scale;
                }
            }
            EditorGUILayout.LabelField("용광로·재생성 대기 확인용. 플레이를 끝내면 x1로 돌아갑니다.", EditorStyles.miniLabel);
        }

        // ---------------- 월드 ----------------

        void DrawWorld()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("광물 전부 재생성")) RespawnMinerals();
                if (GUILayout.Button("살아있는 몬스터 전부 처치")) KillMonsters();
            }

            if (_monsters.Count == 0)
            {
                EditorGUILayout.LabelField("MonsterDefinition 에셋이 없습니다");
                return;
            }

            string[] names = _monsters.Select(m => $"{m.DisplayName} ({m.name})").ToArray();
            using (new EditorGUILayout.HorizontalScope())
            {
                _monsterIndex = EditorGUILayout.Popup(Mathf.Clamp(_monsterIndex, 0, names.Length - 1), names);
                _monsterCount = Mathf.Clamp(EditorGUILayout.IntField(_monsterCount, GUILayout.Width(40)), 1, 20);
                if (GUILayout.Button("소환", GUILayout.Width(50))) Summon(_monsters[_monsterIndex], _monsterCount);
            }

            _summoned.RemoveAll(g => g == null);
            using (new EditorGUI.DisabledScope(_summoned.Count == 0))
            {
                if (GUILayout.Button($"소환한 것 지우기 ({_summoned.Count})")) ClearSummoned();
            }
            EditorGUILayout.LabelField("보스도 MonsterDefinition으로 만들면 여기서 바로 소환됩니다.", EditorStyles.miniLabel);
        }

        void RespawnMinerals()
        {
            MineralNode[] nodes = FindObjectsByType<MineralNode>(FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++) nodes[i].Respawn();
            _status = $"광물 {nodes.Length}개 재생성";
        }

        void KillMonsters()
        {
            GameObject source = Find(ref _health) != null ? _health.gameObject : null;

            int killed = 0;
            foreach (Monster monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
            {
                if (!monster.IsAlive) continue;
                monster.TakeDamage(monster.CurrentHp, source);
                killed++;
            }
            _status = $"몬스터 {killed}마리 처치";
        }

        /// 플레이어 앞쪽에 둥글게 세운다.
        /// 스포너와 같은 순서(꺼서 만들고 종류를 끼운 뒤 켠다)를 따르되 GridPlacement는 뗀다 —
        /// 그대로 두면 프리팹의 칸 좌표로 순간이동하고, 죽음 기록(MonsterDeaths)이 진짜 몬스터 칸과 겹친다.
        void Summon(MonsterDefinition definition, int count)
        {
            PlayerHealth player = Find(ref _health);
            GameObject source = PrefabFor(definition);
            if (player == null || source == null)
            {
                _status = source == null ? "몬스터 프리팹을 하나도 못 찾았습니다" : "플레이어를 못 찾았습니다";
                return;
            }

            Vector3 center = player.transform.position + player.transform.forward * 4f;

            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : i * Mathf.PI * 2f / count;
                Vector3 offset = count == 1 ? Vector3.zero : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.5f;

                bool wasActive = source.activeSelf;
                source.SetActive(false);
                GameObject instance = Instantiate(source, center + offset, Quaternion.identity);
                source.SetActive(wasActive);

                instance.name = $"[치트] {definition.name}_{i}";
                if (instance.TryGetComponent(out GridPlacement placement)) DestroyImmediate(placement);

                if (instance.TryGetComponent(out Monster monster))
                {
                    monster.Configure(definition);
                    // 소환한 것은 되살아나면 안 된다. 죽으면 치운다.
                    monster.Died += m => { if (m != null) Destroy(m.gameObject, 0.5f); };
                }

                instance.SetActive(true);
                _summoned.Add(instance);
            }

            _status = $"{definition.DisplayName} {count}마리 소환";
        }

        void ClearSummoned()
        {
            for (int i = 0; i < _summoned.Count; i++)
            {
                if (_summoned[i] != null) Destroy(_summoned[i]);
            }
            _summoned.Clear();
        }
    }
}
