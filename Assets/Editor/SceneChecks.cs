using System;
using System.Collections.Generic;
using System.Linq;
using ForagerCP;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ForagerCP.EditorTools
{
    public enum IssueSeverity { Error, Warning, Info }

    public class SceneIssue
    {
        public IssueSeverity Severity;
        public string Category;
        public string Title;
        public string Detail;
        public UnityEngine.Object Target;
    }

    public class SceneCheckContext
    {
        public Scene Scene;
        public GameObject[] Roots;
        public MonoBehaviour[] OurComponents;
        public GridPlacement[] Placements;
        public MapPalette Palette;

        public static SceneCheckContext Build()
        {
            Scene scene = SceneManager.GetActiveScene();

            var ours = new List<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour == null) continue;
                if (behaviour.GetType().Namespace != "ForagerCP") continue;
                ours.Add(behaviour);
            }

            return new SceneCheckContext
            {
                Scene = scene,
                Roots = scene.GetRootGameObjects(),
                OurComponents = ours.ToArray(),
                Placements = UnityEngine.Object.FindObjectsByType<GridPlacement>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None),
                Palette = AssetDatabase.LoadAssetAtPath<MapPalette>("Assets/_Project/MapPalette.asset")
            };
        }
    }

    /// 검사 하나. 새 검사를 추가하려면 이 인터페이스를 구현한 클래스를 하나 더 만들면 된다
    /// (창이 리플렉션으로 자동 수집한다).
    public interface ISceneCheck
    {
        string Category { get; }
        IEnumerable<SceneIssue> Run(SceneCheckContext context);
    }

    // ------------------------------------------------------------------ 배선

    /// 우리 컴포넌트의 참조 슬롯이 비어 있는지 전수 검사.
    /// 지금 구조는 참조 하나가 비면 게임을 돌려야 알 수 있어서, 이 검사가 가장 값어치가 크다.
    public class ReferenceCheck : ISceneCheck
    {
        public string Category => "배선";

        /// Awake에서 스스로 찾아 채우는 필드들. 비어 있어도 동작하므로 '정보'로만 알린다.
        static readonly HashSet<string> SelfHealing = new HashSet<string>
        {
            "ConverterStation._inventory", "ConverterStation._gold",
            "ShopStation._gold", "ShopStation._harvestPower",
            "HarvestRewardService._inventory", "HarvestRewardService._level",
            "CombatRewardService._level",
            "PlayerMotor._input", "PlayerMotor._camera", "PlayerMotor._knockback", "PlayerMotor._health",
            "PlayerAttacker._input", "PlayerAttacker._power",
            "PlayerInteractor._input", "UiToggleStub._input", "UiRouter._input",
            "Monster._hitFlash", "Monster._knockback", "MineralNode._hitFlash",
            "PlayerHealth._hitFlash", "PlayerHealth._knockback",
            "MonsterAI._body", "MonsterAI._knockback", "MonsterAI._target",
            "InventoryPanel._inventory", "PlayerHealthBar._health",
            "InteractionPromptView._interactor", "InteractionPromptView._group",
            "ShopPanel._station", "ShopPanel._gold",
            "MonsterHealthBarSpawner._viewCamera", "MonsterHealthBar._group", "MonsterHealthBar._fill"
        };

        /// 비면 그 오브젝트가 아예 기능을 못 하는 필드.
        static readonly HashSet<string> Critical = new HashSet<string>
        {
            "MineralNode._definition", "Monster._definition",
            "MonsterHealthBarSpawner._barPrefab", "MonsterHealthBarSpawner._barParent",
            "CameraRig._target", "PlayerHealth._respawnPoint"
        };

        public IEnumerable<SceneIssue> Run(SceneCheckContext context)
        {
            foreach (MonoBehaviour behaviour in context.OurComponents)
            {
                var serialized = new SerializedObject(behaviour);
                SerializedProperty property = serialized.GetIterator();
                bool enterChildren = true;

                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (property.propertyPath == "m_Script") continue;
                    if (property.depth > 0) continue;
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (property.objectReferenceValue != null) continue;

                    string key = behaviour.GetType().Name + "." + property.propertyPath;

                    IssueSeverity severity;
                    string detail;

                    if (Critical.Contains(key))
                    {
                        severity = IssueSeverity.Error;
                        detail = "이 참조가 없으면 해당 오브젝트가 동작하지 않습니다.";
                    }
                    else if (SelfHealing.Contains(key))
                    {
                        severity = IssueSeverity.Info;
                        detail = "Awake에서 씬을 뒤져 자동으로 채웁니다. 대상이 씬에 있으면 문제 없습니다.";
                    }
                    else
                    {
                        severity = IssueSeverity.Warning;
                        detail = "인스펙터에서 채워주세요.";
                    }

                    yield return new SceneIssue
                    {
                        Severity = severity,
                        Category = Category,
                        Title = $"{behaviour.GetType().Name}.{property.displayName} 비어 있음",
                        Detail = detail,
                        Target = behaviour.gameObject
                    };
                }
            }
        }
    }

    // ------------------------------------------------------------------ 필수/중복

    /// 씬에 반드시 하나만 있어야 하는 것들. 0개면 기능이 죽고, 2개면 이벤트를 두 번 먹어 조용히 틀어진다.
    public class SingletonCheck : ISceneCheck
    {
        public string Category => "필수/중복";

        static readonly (Type type, bool required)[] Targets =
        {
            (typeof(MapGrid), true),
            (typeof(Inventory), true),
            (typeof(GoldWallet), true),
            (typeof(PlayerLevel), true),
            (typeof(PlayerHealth), true),
            (typeof(PlayerAttacker), true),
            (typeof(HarvestRewardService), true),
            (typeof(HudView), true),
            (typeof(UiRouter), true),
            (typeof(PoolManager), false),
            (typeof(CombatRewardService), false),
            (typeof(MonsterHealthBarSpawner), false)
        };

        public IEnumerable<SceneIssue> Run(SceneCheckContext context)
        {
            foreach ((Type type, bool required) in Targets)
            {
                var found = context.OurComponents.Where(c => c.GetType() == type).ToList();

                if (found.Count == 0 && required)
                {
                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = Category,
                        Title = $"{type.Name} 가 씬에 없음",
                        Detail = "이게 없으면 관련 기능 전체가 동작하지 않습니다."
                    };
                }
                else if (found.Count > 1)
                {
                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = Category,
                        Title = $"{type.Name} 가 {found.Count}개 있음",
                        Detail = "중복되면 이벤트를 여러 번 받아 수치가 두 배로 오르거나 화면이 어긋납니다.",
                        Target = found[0].gameObject
                    };
                }
            }

            if (Camera.main == null)
            {
                yield return new SceneIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = Category,
                    Title = "MainCamera 태그가 붙은 카메라가 없음",
                    Detail = "조준 방향 계산과 체력바 위치가 전부 Camera.main을 씁니다."
                };
            }

            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                yield return new SceneIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = Category,
                    Title = "EventSystem 이 없음",
                    Detail = "UI 버튼 클릭이 동작하지 않습니다."
                };
            }
        }
    }

    /// 스크립트를 지웠을 때 남는 'Missing Script'. 실제로 MineralWallet 제거 때 생겼다.
    public class MissingScriptCheck : ISceneCheck
    {
        public string Category => "필수/중복";

        public IEnumerable<SceneIssue> Run(SceneCheckContext context)
        {
            foreach (GameObject root in context.Roots)
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    if (count == 0) continue;

                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = Category,
                        Title = $"{transform.name} 에 깨진 스크립트 {count}개",
                        Detail = "삭제된 스크립트의 잔재입니다. 인스펙터에서 제거하세요.",
                        Target = transform.gameObject
                    };
                }
            }
        }
    }

    // ------------------------------------------------------------------ 배치

    public class PlacementCheck : ISceneCheck
    {
        public string Category => "배치";

        public IEnumerable<SceneIssue> Run(SceneCheckContext context)
        {
            MapGrid grid = MapGrid.Active;

            // 칸 겹침
            for (int i = 0; i < context.Placements.Length; i++)
            {
                for (int j = i + 1; j < context.Placements.Length; j++)
                {
                    GridPlacement a = context.Placements[i];
                    GridPlacement b = context.Placements[j];
                    if (!a.Footprint.Overlaps(b.Footprint)) continue;

                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = Category,
                        Title = $"{a.name} 와 {b.name} 가 같은 칸을 차지함 {a.Cell}",
                        Detail = "겹쳐 놓이면 채집·상호작용 판정이 엉킵니다. 한쪽을 옮겨주세요.",
                        Target = a.gameObject
                    };
                }
            }

            foreach (GridPlacement placement in context.Placements)
            {
                // 셀 좌표와 실제 위치가 어긋남 (손으로 끌어 옮긴 경우)
                if (grid != null)
                {
                    Vector3 expected = grid.CellToWorld(placement.Cell, placement.RotatedSize);
                    Vector3 actual = placement.transform.position;
                    float drift = Vector2.Distance(new Vector2(expected.x, expected.z), new Vector2(actual.x, actual.z));

                    if (drift > 0.01f)
                    {
                        yield return new SceneIssue
                        {
                            Severity = IssueSeverity.Warning,
                            Category = Category,
                            Title = $"{placement.name} 가 칸에서 {drift:F2}m 벗어남",
                            Detail = "배치 도구의 '고른 오브젝트를 가까운 칸에 맞추기'로 정렬하세요.",
                            Target = placement.gameObject
                        };
                    }
                }

                // 그룹 밖에 나와 있음
                Transform parent = placement.transform.parent;
                if (parent == null || parent.GetComponent<MapFolder>() == null)
                {
                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Info,
                        Category = Category,
                        Title = $"{placement.name} 가 그룹 밖에 있음",
                        Detail = "광물/설비/구조물/몬스터 그룹 안에 넣어두면 하이어라키가 정리됩니다.",
                        Target = placement.gameObject
                    };
                }

                if (!placement.gameObject.activeInHierarchy)
                {
                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = Category,
                        Title = $"{placement.name} 가 꺼져 있음",
                        Detail = "비활성 상태라 게임에 나오지 않습니다. 의도한 것이 아니면 켜주세요.",
                        Target = placement.gameObject
                    };
                }
            }
        }
    }

    // ------------------------------------------------------------------ 팔레트

    public class PaletteCheck : ISceneCheck
    {
        public string Category => "팔레트";

        public IEnumerable<SceneIssue> Run(SceneCheckContext context)
        {
            if (context.Palette == null)
            {
                yield return new SceneIssue
                {
                    Severity = IssueSeverity.Warning,
                    Category = Category,
                    Title = "배치 팔레트 파일을 찾지 못함",
                    Detail = "Assets/_Project/MapPalette.asset 이 있어야 배치 도구가 동작합니다."
                };
                yield break;
            }

            var folders = UnityEngine.Object.FindObjectsByType<MapFolder>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < context.Palette.Entries.Count; i++)
            {
                MapPalette.Entry entry = context.Palette.Entries[i];
                string label = string.IsNullOrEmpty(entry.DisplayName) ? $"{i}번 항목" : entry.DisplayName;

                if (entry.Prefab == null)
                {
                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Error,
                        Category = Category,
                        Title = $"팔레트 '{label}' 의 프리팹이 없음",
                        Detail = "프리팹이 삭제됐습니다. 항목을 지우거나 프리팹을 다시 지정하세요.",
                        Target = context.Palette
                    };
                    continue;
                }

                var placement = entry.Prefab.GetComponent<GridPlacement>();
                if (placement == null)
                {
                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = Category,
                        Title = $"팔레트 '{label}' 프리팹에 GridPlacement 가 없음",
                        Detail = "배치할 때 칸 좌표가 기록되지 않습니다.",
                        Target = entry.Prefab
                    };
                }
                else if (placement.Size != entry.Size)
                {
                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = Category,
                        Title = $"팔레트 '{label}' 의 칸 크기가 프리팹과 다름 (팔레트 {entry.Size} / 프리팹 {placement.Size})",
                        Detail = "점유 판정이 어긋나 다른 오브젝트와 겹쳐 놓일 수 있습니다.",
                        Target = entry.Prefab
                    };
                }

                if (!string.IsNullOrEmpty(entry.Category) && folders.All(f => f.Category != entry.Category))
                {
                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Info,
                        Category = Category,
                        Title = $"'{entry.Category}' 그룹이 씬에 없음",
                        Detail = "배치할 때 자동으로 만들어지므로 보통은 문제 없습니다.",
                        Target = context.Palette
                    };
                }
            }
        }
    }

    // ------------------------------------------------------------------ 물리

    public class PhysicsCheck : ISceneCheck
    {
        public string Category => "물리";

        public IEnumerable<SceneIssue> Run(SceneCheckContext context)
        {
            foreach (MonoBehaviour behaviour in context.OurComponents)
            {
                switch (behaviour)
                {
                    case MineralNode node when node.GetComponentInChildren<Collider>(true) == null:
                        yield return new SceneIssue
                        {
                            Severity = IssueSeverity.Error,
                            Category = Category,
                            Title = $"{node.name} 에 콜라이더가 없음",
                            Detail = "좌클릭 판정이 콜라이더 기반이라 아예 캐지지 않습니다.",
                            Target = node.gameObject
                        };
                        break;

                    case Monster monster:
                        if (monster.GetComponentInChildren<Collider>(true) == null)
                        {
                            yield return new SceneIssue
                            {
                                Severity = IssueSeverity.Error,
                                Category = Category,
                                Title = $"{monster.name} 에 콜라이더가 없음",
                                Detail = "때릴 수도, 부딪힐 수도 없습니다.",
                                Target = monster.gameObject
                            };
                        }

                        if (monster.GetComponent<Rigidbody>() == null)
                        {
                            yield return new SceneIssue
                            {
                                Severity = IssueSeverity.Warning,
                                Category = Category,
                                Title = $"{monster.name} 에 Rigidbody 가 없음",
                                Detail = "이동이 물리를 거치지 않아 구조물을 통과하고, 넉백도 걸리지 않습니다.",
                                Target = monster.gameObject
                            };
                        }
                        break;

                    case PlayerMotor motor:
                        var body = motor.GetComponent<Rigidbody>();
                        if (body != null && !body.freezeRotation)
                        {
                            yield return new SceneIssue
                            {
                                Severity = IssueSeverity.Warning,
                                Category = Category,
                                Title = "플레이어 Rigidbody 의 회전이 잠겨 있지 않음",
                                Detail = "부딪힐 때 캐릭터가 넘어지듯 돌아갑니다. (Awake에서 잠그지만 편집 중 확인용)",
                                Target = motor.gameObject
                            };
                        }
                        break;
                }
            }
        }
    }

    // ------------------------------------------------------------------ 잔여물

    public class LeftoverCheck : ISceneCheck
    {
        public string Category => "잔여물";

        public IEnumerable<SceneIssue> Run(SceneCheckContext context)
        {
            foreach (GameObject root in context.Roots)
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!transform.name.StartsWith("__")) continue;

                    yield return new SceneIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Category = Category,
                        Title = $"테스트 오브젝트가 남아 있음: {transform.name}",
                        Detail = "검증 스크립트가 남긴 잔여물로 보입니다. 지워도 됩니다.",
                        Target = transform.gameObject
                    };
                }
            }
        }
    }
}
