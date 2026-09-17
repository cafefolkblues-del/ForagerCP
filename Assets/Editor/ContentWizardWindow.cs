using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ForagerCP;
using UnityEditor;
using UnityEngine;

namespace ForagerCP.EditorTools
{
    /// 새 광물/몬스터/구조물을 한 번에 만들어주는 창.
    /// 손으로 하면 정의(SO) → 머티리얼 → 프리팹 배선 → 팔레트 등록 네 군데를 건드려야 하고,
    /// 하나라도 빠지면 게임을 돌려야 알 수 있다(정의 안 꽂힌 광물 = 채집 불가).
    ///
    /// 만들기 전용 도구다. 이미 있는 값을 고치는 건 밸런스 표 도구가 맡는다.
    public class ContentWizardWindow : EditorWindow
    {
        enum ContentKind { Mineral, Monster, Structure }

        const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        const string ItemFolder = "Assets/_Project/Items";
        const string MaterialFolder = "Assets/_Project/Materials";
        const string PalettePath = "Assets/_Project/MapPalette.asset";
        const string MineralBasePrefab = "Assets/_Project/Prefabs/Minerals/Mineral.prefab";

        [SerializeField] ContentKind _kind = ContentKind.Mineral;

        // 공통
        [SerializeField] string _assetId = "Mineral_New";
        [SerializeField] string _displayName = "새 광물";
        [SerializeField] Color _color = new Color(0.45f, 0.7f, 0.95f);
        [SerializeField] Vector2Int _footprint = Vector2Int.one;
        [SerializeField] float _heightOffset = 0.5f;
        [SerializeField] GameObject _model;
        [SerializeField] Sprite _icon;

        // 광물
        [SerializeField] int _maxHp = 30;
        [SerializeField] int _harvestYield = 1;
        [SerializeField] int _expReward = 1;
        [SerializeField] int _goldValue = 10;
        [SerializeField] int _maxStack = 99;
        [SerializeField] float _respawnDelay = 10f;

        // 몬스터
        [SerializeField] int _monsterHp = 20;
        [SerializeField] int _attackPower = 2;
        [SerializeField] float _attackInterval = 1.2f;
        [SerializeField] float _attackRange = 1.2f;
        [SerializeField] float _moveSpeed = 2.5f;
        [SerializeField] float _detectRange = 8f;
        [SerializeField] float _leashDistance = 12f;
        [SerializeField] int _monsterExp = 2;
        [SerializeField] float _monsterRespawn = 15f;

        // 구조물
        [SerializeField] bool _hasCollider = true;
        [SerializeField] bool _stretchToFootprint = true;

        [SerializeField] Object _template;
        [SerializeField] bool _addToPalette = true;

        readonly List<string> _createdPaths = new List<string>();
        Vector2 _scroll;
        string _message = "";
        MessageType _messageType = MessageType.None;

        [MenuItem("Tools/ForagerCP/새 콘텐츠 만들기")]
        static void Open() => GetWindow<ContentWizardWindow>("새 콘텐츠");

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("무엇을 만들까요", EditorStyles.boldLabel);
            _kind = (ContentKind)GUILayout.Toolbar((int)_kind, new[] { "광물", "몬스터", "구조물" });

            EditorGUILayout.Space(8f);
            DrawTemplatePicker();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("기본 정보", EditorStyles.boldLabel);
            _displayName = EditorGUILayout.TextField("표시 이름 (한글 가능)", _displayName);
            _assetId = EditorGUILayout.TextField("파일 이름 (영문)", _assetId);
            _color = EditorGUILayout.ColorField("대표 색", _color);
            _footprint = EditorGUILayout.Vector2IntField("차지할 칸 (가로/세로)", _footprint);
            _heightOffset = EditorGUILayout.FloatField("바닥에서 높이", _heightOffset);
            _model = (GameObject)EditorGUILayout.ObjectField("모델 (비우면 임시 도형)", _model, typeof(GameObject), false);

            EditorGUILayout.Space(8f);
            switch (_kind)
            {
                case ContentKind.Mineral: DrawMineralFields(); break;
                case ContentKind.Monster: DrawMonsterFields(); break;
                case ContentKind.Structure: DrawStructureFields(); break;
            }

            EditorGUILayout.Space(8f);
            _addToPalette = EditorGUILayout.ToggleLeft("배치 팔레트에 자동 등록", _addToPalette);

            EditorGUILayout.Space(8f);
            DrawPreview();

            EditorGUILayout.Space(10f);
            DrawCreateButton();

            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, _messageType);

            if (_createdPaths.Count > 0)
            {
                EditorGUILayout.Space(6f);
                if (GUILayout.Button("방금 만든 것 취소 (파일 삭제)")) UndoLastCreation();
            }

            EditorGUILayout.EndScrollView();
        }

        // ---------- 입력 영역 ----------

        void DrawTemplatePicker()
        {
            EditorGUILayout.LabelField("기존 것에서 값 불러오기 (선택)", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            System.Type templateType = _kind == ContentKind.Monster ? typeof(MonsterDefinition) : typeof(MineralDefinition);
            _template = EditorGUILayout.ObjectField(_template, templateType, false);

            using (new EditorGUI.DisabledScope(_template == null))
            {
                if (GUILayout.Button("값 가져오기", GUILayout.Width(100f))) CopyFromTemplate();
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawMineralFields()
        {
            EditorGUILayout.LabelField("광물 수치", EditorStyles.boldLabel);
            _maxHp = EditorGUILayout.IntField("체력 (몇 대 맞으면 깨지나)", _maxHp);
            _harvestYield = EditorGUILayout.IntField("한 번에 얻는 개수", _harvestYield);
            _expReward = EditorGUILayout.IntField("경험치", _expReward);
            _goldValue = EditorGUILayout.IntField("개당 판매가 (골드)", _goldValue);
            _respawnDelay = EditorGUILayout.FloatField("다시 생길 때까지 (초)", _respawnDelay);
            _maxStack = EditorGUILayout.IntField("한 칸에 쌓이는 최대 개수", _maxStack);
            _icon = (Sprite)EditorGUILayout.ObjectField("인벤토리 아이콘 (선택)", _icon, typeof(Sprite), false);

            if (_maxHp > 0 && _harvestYield > 0)
            {
                EditorGUILayout.LabelField(" ", $"채집력 5 기준 {Mathf.CeilToInt(_maxHp / 5f)}대 / 채집력 10 기준 {Mathf.CeilToInt(_maxHp / 10f)}대",
                    EditorStyles.miniLabel);
            }
        }

        void DrawMonsterFields()
        {
            EditorGUILayout.LabelField("몬스터 수치", EditorStyles.boldLabel);
            _monsterHp = EditorGUILayout.IntField("체력", _monsterHp);
            _attackPower = EditorGUILayout.IntField("공격력", _attackPower);
            _attackInterval = EditorGUILayout.FloatField("공격 간격 (초)", _attackInterval);
            _attackRange = EditorGUILayout.FloatField("공격 사거리 (m)", _attackRange);
            _moveSpeed = EditorGUILayout.FloatField("이동 속도", _moveSpeed);
            _detectRange = EditorGUILayout.FloatField("플레이어 인지 범위 (m)", _detectRange);
            _leashDistance = EditorGUILayout.FloatField("집에서 멀어지면 복귀 (m)", _leashDistance);
            _monsterExp = EditorGUILayout.IntField("처치 경험치", _monsterExp);
            _monsterRespawn = EditorGUILayout.FloatField("다시 나올 때까지 (초)", _monsterRespawn);

            if (_monsterHp > 0 && _attackPower > 0)
            {
                EditorGUILayout.LabelField(" ", $"공격력 5 기준 {Mathf.CeilToInt(_monsterHp / 5f)}대 / 플레이어 체력 20 기준 {Mathf.CeilToInt(20f / _attackPower)}대 맞으면 사망",
                    EditorStyles.miniLabel);
            }
        }

        void DrawStructureFields()
        {
            EditorGUILayout.LabelField("구조물 설정", EditorStyles.boldLabel);
            _hasCollider = EditorGUILayout.ToggleLeft("막힘 (지나갈 수 없음)", _hasCollider);
            _stretchToFootprint = EditorGUILayout.ToggleLeft("임시 도형을 칸 크기에 맞춰 늘리기", _stretchToFootprint);
        }

        void DrawPreview()
        {
            EditorGUILayout.LabelField("만들어질 파일", EditorStyles.boldLabel);
            foreach (string path in PlannedPaths()) EditorGUILayout.LabelField(" ", path, EditorStyles.miniLabel);
        }

        void DrawCreateButton()
        {
            string error = Validate();
            using (new EditorGUI.DisabledScope(error != null))
            {
                GUI.backgroundColor = new Color(0.45f, 1f, 0.6f);
                if (GUILayout.Button("만들기", GUILayout.Height(36f))) Create();
                GUI.backgroundColor = Color.white;
            }

            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Warning);
        }

        // ---------- 검증 ----------

        string Validate()
        {
            if (string.IsNullOrWhiteSpace(_assetId)) return "파일 이름을 입력해주세요.";

            foreach (char c in _assetId)
            {
                // 파일명에 한글이 들어가면 도구/스크립트 경로에서 인코딩 문제가 생긴 적이 있다.
                // 표시 이름은 한글로 두고 파일 이름만 영문으로 강제한다.
                if (c > 127) return "파일 이름은 영문/숫자만 써주세요. (한글은 '표시 이름'에)";
                if (char.IsWhiteSpace(c)) return "파일 이름에 공백을 쓸 수 없습니다. _ 를 써주세요.";
                if (System.IO.Path.GetInvalidFileNameChars().Contains(c)) return "파일 이름에 쓸 수 없는 문자가 있습니다.";
            }

            foreach (string path in PlannedPaths())
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path) != null) return "이미 같은 이름이 있습니다: " + path;
            }

            if (_footprint.x < 1 || _footprint.y < 1) return "차지할 칸은 1 이상이어야 합니다.";
            if (_kind == ContentKind.Mineral && AssetDatabase.LoadAssetAtPath<GameObject>(MineralBasePrefab) == null && _model == null)
                return "기본 광물 프리팹을 찾을 수 없습니다. 모델을 직접 지정해주세요.";

            return null;
        }

        string PrefabFolder()
        {
            switch (_kind)
            {
                case ContentKind.Mineral: return "Assets/_Project/Prefabs/Minerals";
                case ContentKind.Monster: return "Assets/_Project/Prefabs/Monsters";
                default: return "Assets/_Project/Prefabs/Structures";
            }
        }

        IEnumerable<string> PlannedPaths()
        {
            if (_kind != ContentKind.Structure) yield return $"{ItemFolder}/{_assetId}.asset";
            if (_model == null) yield return $"{MaterialFolder}/{_assetId}.mat";
            yield return $"{PrefabFolder()}/{_assetId}.prefab";
        }

        // ---------- 생성 ----------

        void Create()
        {
            _createdPaths.Clear();

            EnsureFolder(ItemFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder());

            GameObject prefab = null;
            Object definition = null;

            switch (_kind)
            {
                case ContentKind.Mineral:
                    definition = CreateMineralDefinition();
                    prefab = CreateMineralPrefab((MineralDefinition)definition);
                    break;
                case ContentKind.Monster:
                    definition = CreateMonsterDefinition();
                    prefab = CreateMonsterPrefab((MonsterDefinition)definition);
                    break;
                case ContentKind.Structure:
                    prefab = CreateStructurePrefab();
                    break;
            }

            if (_addToPalette && prefab != null) AddToPalette(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Object focus = definition != null ? definition : prefab;
            Selection.activeObject = focus;
            EditorGUIUtility.PingObject(focus);

            _message = $"'{DisplayNameOrId()}' 생성 완료 — 파일 {_createdPaths.Count}개" +
                       (_addToPalette ? "\n배치 팔레트에도 등록했습니다. (Ctrl+Shift+M)" : "");
            _messageType = MessageType.Info;
        }

        MineralDefinition CreateMineralDefinition()
        {
            var definition = ScriptableObject.CreateInstance<MineralDefinition>();
            Set(definition, "_displayName", DisplayNameOrId());
            Set(definition, "_icon", _icon);
            Set(definition, "_maxStack", Mathf.Max(1, _maxStack));
            Set(definition, "_goldValue", Mathf.Max(0, _goldValue));
            Set(definition, "_maxHp", Mathf.Max(1, _maxHp));
            Set(definition, "_harvestYield", Mathf.Max(1, _harvestYield));
            Set(definition, "_expReward", Mathf.Max(0, _expReward));
            Set(definition, "_respawnDelay", Mathf.Max(0f, _respawnDelay));

            string path = $"{ItemFolder}/{_assetId}.asset";
            AssetDatabase.CreateAsset(definition, path);
            _createdPaths.Add(path);
            return definition;
        }

        MonsterDefinition CreateMonsterDefinition()
        {
            var definition = ScriptableObject.CreateInstance<MonsterDefinition>();
            Set(definition, "_displayName", DisplayNameOrId());
            Set(definition, "_maxHp", Mathf.Max(1, _monsterHp));
            Set(definition, "_attackPower", Mathf.Max(0, _attackPower));
            Set(definition, "_attackInterval", Mathf.Max(0.05f, _attackInterval));
            Set(definition, "_attackRange", Mathf.Max(0.1f, _attackRange));
            Set(definition, "_moveSpeed", Mathf.Max(0f, _moveSpeed));
            Set(definition, "_detectRange", Mathf.Max(0f, _detectRange));
            Set(definition, "_leashDistance", Mathf.Max(0f, _leashDistance));
            Set(definition, "_expReward", Mathf.Max(0, _monsterExp));
            Set(definition, "_respawnDelay", Mathf.Max(0f, _monsterRespawn));

            string path = $"{ItemFolder}/{_assetId}.asset";
            AssetDatabase.CreateAsset(definition, path);
            _createdPaths.Add(path);
            return definition;
        }

        /// 모델이 없으면 기본 광물 프리팹의 '변형'으로 만든다 — 공통 구조 수정이 전부에 내려가도록.
        /// 모델을 직접 넣으면 구조가 달라지므로 독립 프리팹으로 만든다.
        GameObject CreateMineralPrefab(MineralDefinition definition)
        {
            string path = $"{PrefabFolder()}/{_assetId}.prefab";

            if (_model == null)
            {
                var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MineralBasePrefab);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);

                var node = instance.GetComponent<MineralNode>();
                if (node != null) Set(node, "_definition", definition);

                var renderer = instance.GetComponentInChildren<Renderer>();
                if (renderer != null) renderer.sharedMaterial = CreateMaterial();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
                DestroyImmediate(instance);
                _createdPaths.Add(path);
                return saved;
            }

            GameObject root = BuildRootFromModel();
            var mineralNode = root.AddComponent<MineralNode>();
            Set(mineralNode, "_definition", definition);
            root.AddComponent<HitFlash>();
            AddGridPlacement(root);

            GameObject standalone = PrefabUtility.SaveAsPrefabAsset(root, path);
            DestroyImmediate(root);
            _createdPaths.Add(path);
            return standalone;
        }

        GameObject CreateMonsterPrefab(MonsterDefinition definition)
        {
            GameObject root = _model != null ? BuildRootFromModel() : BuildPrimitive(PrimitiveType.Sphere);

            var body = root.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var flash = root.AddComponent<HitFlash>();
            var knockback = root.AddComponent<Knockback>();

            var monster = root.AddComponent<Monster>();
            Set(monster, "_definition", definition);
            Set(monster, "_hitFlash", flash);
            Set(monster, "_knockback", knockback);

            var ai = root.AddComponent<MonsterAI>();
            Set(ai, "_body", body);
            Set(ai, "_knockback", knockback);

            AddGridPlacement(root);

            string path = $"{PrefabFolder()}/{_assetId}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            DestroyImmediate(root);
            _createdPaths.Add(path);
            return saved;
        }

        GameObject CreateStructurePrefab()
        {
            GameObject root = _model != null ? BuildRootFromModel() : BuildPrimitive(PrimitiveType.Cube);

            if (!_hasCollider)
            {
                foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) DestroyImmediate(collider);
            }

            GridPlacement placement = AddGridPlacement(root);
            Set(placement, "_stretchToFootprint", _model == null && _stretchToFootprint);

            string path = $"{PrefabFolder()}/{_assetId}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            DestroyImmediate(root);
            _createdPaths.Add(path);
            return saved;
        }

        // ---------- 조각 ----------

        GameObject BuildPrimitive(PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = _assetId;
            go.GetComponent<Renderer>().sharedMaterial = CreateMaterial();
            return go;
        }

        /// 모델 프리팹을 자식으로 넣고, 게임 로직 컴포넌트는 루트에 붙인다.
        /// 모델을 직접 개조하지 않아야 아트가 파일을 갈아끼워도 배선이 안 깨진다.
        GameObject BuildRootFromModel()
        {
            var root = new GameObject(_assetId);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(_model, root.transform);
            if (visual != null) visual.transform.localPosition = Vector3.zero;

            if (root.GetComponentInChildren<Collider>(true) == null)
            {
                var box = root.AddComponent<BoxCollider>();
                Renderer renderer = root.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    box.center = root.transform.InverseTransformPoint(renderer.bounds.center);
                    box.size = renderer.bounds.size;
                }
            }

            return root;
        }

        GridPlacement AddGridPlacement(GameObject root)
        {
            var placement = root.GetComponent<GridPlacement>();
            if (placement == null) placement = root.AddComponent<GridPlacement>();

            Set(placement, "_size", new Vector2Int(Mathf.Max(1, _footprint.x), Mathf.Max(1, _footprint.y)));
            Set(placement, "_heightOffset", _heightOffset);
            return placement;
        }

        Material CreateMaterial()
        {
            string path = $"{MaterialFolder}/{_assetId}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader != null ? shader : Shader.Find("Standard"));
            material.SetColor("_BaseColor", _color);
            material.color = _color;

            AssetDatabase.CreateAsset(material, path);
            _createdPaths.Add(path);
            return material;
        }

        void AddToPalette(GameObject prefab)
        {
            var palette = AssetDatabase.LoadAssetAtPath<MapPalette>(PalettePath);
            if (palette == null)
            {
                _message = "배치 팔레트 파일을 찾지 못해 등록을 건너뛰었습니다: " + PalettePath;
                _messageType = MessageType.Warning;
                return;
            }

            var entries = new List<MapPalette.Entry>(palette.Entries)
            {
                new MapPalette.Entry
                {
                    DisplayName = DisplayNameOrId(),
                    Prefab = prefab,
                    Category = CategoryName(),
                    Size = new Vector2Int(Mathf.Max(1, _footprint.x), Mathf.Max(1, _footprint.y)),
                    HeightOffset = _heightOffset,
                    SwatchColor = _color,
                    AllowRotation = _kind == ContentKind.Structure
                }
            };

            typeof(MapPalette).GetField("_entries", Flags).SetValue(palette, entries);
            EditorUtility.SetDirty(palette);
        }

        string CategoryName()
        {
            switch (_kind)
            {
                case ContentKind.Mineral: return "Minerals";
                case ContentKind.Monster: return "Monsters";
                default: return "Structures";
            }
        }

        void CopyFromTemplate()
        {
            if (_template is MineralDefinition mineral)
            {
                _maxHp = mineral.MaxHp;
                _harvestYield = mineral.HarvestYield;
                _expReward = mineral.ExpReward;
                _goldValue = mineral.GoldValue;
                _maxStack = mineral.MaxStack;
                _respawnDelay = mineral.RespawnDelay;
                _message = $"'{mineral.DisplayName}' 값을 불러왔습니다.";
            }
            else if (_template is MonsterDefinition monster)
            {
                _monsterHp = monster.MaxHp;
                _attackPower = monster.AttackPower;
                _attackInterval = monster.AttackInterval;
                _attackRange = monster.AttackRange;
                _moveSpeed = monster.MoveSpeed;
                _detectRange = monster.DetectRange;
                _leashDistance = monster.LeashDistance;
                _monsterExp = monster.ExpReward;
                _monsterRespawn = monster.RespawnDelay;
                _message = $"'{monster.DisplayName}' 값을 불러왔습니다.";
            }

            _messageType = MessageType.Info;
        }

        /// 에셋 생성은 Ctrl+Z로 되돌릴 수 없어서 따로 지워주는 버튼이 필요하다.
        void UndoLastCreation()
        {
            if (!EditorUtility.DisplayDialog("방금 만든 것 취소",
                $"파일 {_createdPaths.Count}개를 지웁니다. 되돌릴 수 없습니다.", "지우기", "그만두기")) return;

            foreach (string path in _createdPaths) AssetDatabase.DeleteAsset(path);
            _createdPaths.Clear();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _message = "방금 만든 파일을 지웠습니다. (팔레트 항목은 직접 지워주세요)";
            _messageType = MessageType.Warning;
        }

        string DisplayNameOrId() => string.IsNullOrWhiteSpace(_displayName) ? _assetId : _displayName;

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void Set(object target, string field, object value)
        {
            System.Type type = target.GetType();
            FieldInfo info = null;
            while (type != null && info == null)
            {
                info = type.GetField(field, Flags);
                type = type.BaseType;
            }

            if (info == null)
            {
                Debug.LogError($"필드를 찾지 못했습니다: {target.GetType().Name}.{field}");
                return;
            }

            info.SetValue(target, value);
        }
    }
}
