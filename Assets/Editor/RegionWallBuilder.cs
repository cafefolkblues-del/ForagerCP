using System.Collections.Generic;
using ForagerCP;
using UnityEditor;
using UnityEngine;

namespace ForagerCP.EditorTools
{
    /// 구획 경계를 따라 벽과 문을 둘러주는 도구.
    ///
    /// 에디터에서만 돈다 — 나온 결과는 평범한 씬 오브젝트라 디자이너가 옮기고 지우고 바꿀 수 있다.
    /// 실행 중에 생기는 것은 하나도 없다.
    ///
    /// 경계는 사각형 단위가 아니라 칸 단위로 본다. 한 지역을 ㄱ자로 여러 번 그렸을 때
    /// 사각형 네 변을 그대로 두르면 지역 안쪽에도 벽이 서기 때문.
    public static class RegionWallBuilder
    {
        const string BarrierRootName = "Barriers";

        /// 둘레를 따라 네 방향. 값은 방향 벡터이자 정렬 기준이다.
        static readonly Vector2Int[] Directions =
        {
            new Vector2Int(0, 1),   // 북
            new Vector2Int(0, -1),  // 남
            new Vector2Int(1, 0),   // 동
            new Vector2Int(-1, 0),  // 서
        };

        class Run
        {
            public Vector2Int Direction;
            public int Line;          // 변이 놓인 줄(가로변이면 y, 세로변이면 x)
            public int Start;         // 변이 뻗은 축의 시작 칸(포함)
            public int End;           // 끝 칸(포함)
            public RegionDefinition Outside;
            public bool IsGate;

            public int Length => End - Start + 1;
            public bool Horizontal => Direction.x == 0;
        }

        // ------------------------------------------------------------------ 바깥에서 부르는 것

        /// 한 지역의 벽을 전부 지우고 다시 두른다. 반환값은 새로 생긴 개수.
        public static int Rebuild(RegionDefinition region)
        {
            if (region == null) return 0;

            MapGrid grid = MapGrid.Active;
            if (grid == null) return 0;

            Transform group = ResolveGroup(region);
            ClearGroup(group);

            // 조건이 없는 지역은 막을 이유가 없다. 시작 지역을 벽으로 가두지 않도록.
            if (!region.IsLockable) return 0;

            Dictionary<Vector2Int, RegionDefinition> world = MapCells();

            var cells = new HashSet<Vector2Int>();
            foreach (KeyValuePair<Vector2Int, RegionDefinition> pair in world)
            {
                if (pair.Value == region) cells.Add(pair.Key);
            }
            if (cells.Count == 0) return 0;

            List<Run> runs = BuildRuns(region, cells, world);
            MarkGates(runs);

            var access = Object.FindFirstObjectByType<RegionAccess>();
            int made = 0;

            foreach (Run run in runs) made += Spawn(grid, region, access, group, run);
            return made;
        }

        public static int RebuildAll()
        {
            var seen = new HashSet<RegionDefinition>();
            int made = 0;

            foreach (RegionVolume volume in Object.FindObjectsByType<RegionVolume>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (volume.Region == null || !seen.Add(volume.Region)) continue;
                made += Rebuild(volume.Region);
            }
            return made;
        }

        /// 지역이 통째로 사라졌을 때 남은 벽을 치운다.
        public static void ClearFor(RegionDefinition region)
        {
            Transform group = FindGroup(region);
            if (group == null) return;

            Undo.DestroyObjectImmediate(group.gameObject);
        }

        // ------------------------------------------------------------------ 경계 계산

        /// 씬의 볼륨에서 직접 칸 지도를 만든다.
        /// RegionMap을 쓰지 않는 이유: 그쪽은 플레이 모드에 맞춰 비워지는 정적 사전이라
        /// 에디터에서는 지난 씬의 찌꺼기가 남아 있을 수 있다. 도구는 눈앞의 씬만 믿어야 한다.
        static Dictionary<Vector2Int, RegionDefinition> MapCells()
        {
            var world = new Dictionary<Vector2Int, RegionDefinition>();

            foreach (RegionVolume volume in Object.FindObjectsByType<RegionVolume>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (volume.Region == null) continue;

                RectInt area = volume.Area;
                for (int x = area.xMin; x < area.xMax; x++)
                {
                    for (int y = area.yMin; y < area.yMax; y++) world[new Vector2Int(x, y)] = volume.Region;
                }
            }
            return world;
        }

        /// 바깥쪽이 어느 지역인지까지 같이 묶는다. 이웃이 바뀌는 자리에서 벽이 끊겨야
        /// "이쪽은 바위 지대로 난 문, 저쪽은 그냥 벽"을 가를 수 있다.
        static List<Run> BuildRuns(RegionDefinition region, HashSet<Vector2Int> cells,
            Dictionary<Vector2Int, RegionDefinition> world)
        {
            var runs = new List<Run>();

            foreach (Vector2Int direction in Directions)
            {
                // (줄, 바깥 지역) 별로 변을 모은 뒤 이어진 것끼리 합친다.
                var lines = new Dictionary<(int line, RegionDefinition outside), List<int>>();

                foreach (Vector2Int cell in cells)
                {
                    Vector2Int neighbour = cell + direction;
                    if (cells.Contains(neighbour)) continue;

                    world.TryGetValue(neighbour, out RegionDefinition outside);
                    if (outside == region) continue; // 같은 지역의 다른 볼륨이면 경계가 아니다

                    bool horizontal = direction.x == 0;
                    int line = horizontal ? cell.y : cell.x;
                    int along = horizontal ? cell.x : cell.y;

                    var key = (line, outside);
                    if (!lines.TryGetValue(key, out List<int> list)) lines[key] = list = new List<int>();
                    list.Add(along);
                }

                foreach (KeyValuePair<(int line, RegionDefinition outside), List<int>> pair in lines)
                {
                    List<int> along = pair.Value;
                    along.Sort();

                    int start = along[0];
                    int previous = along[0];

                    for (int i = 1; i <= along.Count; i++)
                    {
                        bool broken = i == along.Count || along[i] != previous + 1;
                        if (broken)
                        {
                            runs.Add(new Run
                            {
                                Direction = direction,
                                Line = pair.Key.line,
                                Start = start,
                                End = previous,
                                Outside = pair.Key.outside,
                            });

                            if (i < along.Count) start = along[i];
                        }

                        if (i < along.Count) previous = along[i];
                    }
                }
            }
            return runs;
        }

        /// 이웃 지역마다 가장 긴 변 하나의 한가운데를 문으로 뚫는다.
        /// 안 뚫으면 들어갈 길이 없고, 전부 문으로 두면 아무 데서나 열려 통로라는 감각이 사라진다.
        static void MarkGates(List<Run> runs)
        {
            var longest = new Dictionary<RegionDefinition, Run>();

            foreach (Run run in runs)
            {
                // 구획하지 않은 땅 쪽에는 문을 내지 않는다 — 보통 맵 바깥이라 갈 데가 없다.
                if (run.Outside == null) continue;
                if (longest.TryGetValue(run.Outside, out Run best) && best.Length >= run.Length) continue;

                longest[run.Outside] = run;
            }

            // 이웃 지역이 하나도 없으면(구획 안 된 땅에 둘러싸인 경우) 가장 긴 변에 하나라도 낸다.
            if (longest.Count == 0 && runs.Count > 0)
            {
                Run best = runs[0];
                foreach (Run run in runs)
                {
                    if (run.Length > best.Length) best = run;
                }
                longest[null] = best;
            }

            foreach (Run gateRun in longest.Values) Split(runs, gateRun);
        }

        /// 한 변의 가운데 한 칸을 문으로 떼어내고 양옆은 벽으로 남긴다.
        static void Split(List<Run> runs, Run run)
        {
            int middle = (run.Start + run.End) / 2;

            int originalEnd = run.End;
            int originalStart = run.Start;

            // 가운데 칸만 남겨 이 Run 자체를 문으로 바꾼다.
            run.Start = middle;
            run.End = middle;
            run.IsGate = true;

            if (originalStart <= middle - 1)
            {
                runs.Add(new Run
                {
                    Direction = run.Direction,
                    Line = run.Line,
                    Start = originalStart,
                    End = middle - 1,
                    Outside = run.Outside,
                });
            }

            if (middle + 1 <= originalEnd)
            {
                runs.Add(new Run
                {
                    Direction = run.Direction,
                    Line = run.Line,
                    Start = middle + 1,
                    End = originalEnd,
                    Outside = run.Outside,
                });
            }
        }

        // ------------------------------------------------------------------ 만들기

        static int Spawn(MapGrid grid, RegionDefinition region, RegionAccess access, Transform group, Run run)
        {
            BarrierKind kind = run.IsGate ? BarrierKind.Gate : BarrierKind.Wall;
            GameObject prefab = run.IsGate ? region.GatePrefab : region.WallPrefab;

            // 아트가 들어오면 칸마다 하나씩 — 늘려 쓰면 모델이 찌그러진다.
            // 아직 없으면 변 하나를 길쭉한 상자 하나로 때운다. 씬에 수백 개가 쌓이지 않게.
            if (prefab != null)
            {
                for (int along = run.Start; along <= run.End; along++)
                {
                    var one = new Run { Direction = run.Direction, Line = run.Line, Start = along, End = along };
                    SpawnPrefab(grid, region, access, group, one, prefab, kind);
                }
                return run.Length;
            }

            SpawnBox(grid, region, access, group, run, kind);
            return 1;
        }

        static void SpawnPrefab(MapGrid grid, RegionDefinition region, RegionAccess access, Transform group,
            Run run, GameObject prefab, BarrierKind kind)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            Undo.RegisterCreatedObjectUndo(instance, "경계 두르기");

            instance.transform.position = CenterOf(grid, region, run);
            instance.transform.rotation = Quaternion.LookRotation(
                new Vector3(run.Direction.x, 0f, run.Direction.y), Vector3.up);

            Finish(instance, region, access, kind);
        }

        static void SpawnBox(MapGrid grid, RegionDefinition region, RegionAccess access, Transform group,
            Run run, BarrierKind kind)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(box, "경계 두르기");
            box.transform.SetParent(group, false);

            box.transform.position = CenterOf(grid, region, run);

            float span = run.Length * grid.CellSize;
            float thickness = region.WallThickness;
            box.transform.localScale = run.Horizontal
                ? new Vector3(span, region.WallHeight, thickness)
                : new Vector3(thickness, region.WallHeight, span);

            box.GetComponent<MeshRenderer>().sharedMaterial = ResolveMaterial(region, kind);
            Finish(box, region, access, kind);
        }

        /// 지역·종류마다 머티리얼 에셋을 하나씩 두고 돌려 쓴다.
        ///
        /// MaterialPropertyBlock으로 색만 칠하면 안 된다 — 그건 씬에 저장되지 않는 런타임 상태라
        /// 도메인이 한 번 다시 올라오면 전부 기본 회색으로 돌아간다(실제로 그렇게 나왔다).
        /// 에셋으로 두면 디자이너가 직접 손볼 수도 있다.
        static Material ResolveMaterial(RegionDefinition region, BarrierKind kind)
        {
            const string folder = "Assets/_Project/Art/Materials";
            System.IO.Directory.CreateDirectory(folder);

            string suffix = kind == BarrierKind.Gate ? "Gate" : "Wall";
            string path = $"{folder}/Region_{region.name}_{suffix}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // 문은 밝게, 벽은 어둡게. 아트가 오기 전까지 어디로 들어가는지는 보여야 한다.
            Color color = region.MapColor;
            color.a = 1f;
            Color shade = kind == BarrierKind.Gate ? color : color * 0.4f;
            shade.a = 1f;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", shade);
            AssetDatabase.CreateAsset(material, path);

            return material;
        }

        static void Finish(GameObject target, RegionDefinition region, RegionAccess access, BarrierKind kind)
        {
            target.name = kind == BarrierKind.Gate ? $"{region.DisplayName} 문" : $"{region.DisplayName} 벽";

            RegionBarrier barrier = target.GetComponent<RegionBarrier>();
            if (barrier == null) barrier = Undo.AddComponent<RegionBarrier>(target);

            barrier.Setup(region, access, kind);
            EditorUtility.SetDirty(barrier);
        }

        /// 변은 칸 가장자리에 놓인다 — 칸 중심에서 반 칸 바깥.
        static Vector3 CenterOf(MapGrid grid, RegionDefinition region, Run run)
        {
            float cell = grid.CellSize;
            Vector3 origin = grid.Origin;
            float middle = (run.Start + run.End) * 0.5f;
            float y = region.WallHeight * 0.5f;

            if (run.Horizontal)
            {
                float z = origin.z + (run.Line + run.Direction.y * 0.5f) * cell;
                return new Vector3(origin.x + middle * cell, y, z);
            }

            float x = origin.x + (run.Line + run.Direction.x * 0.5f) * cell;
            return new Vector3(x, y, origin.z + middle * cell);
        }

        // ------------------------------------------------------------------ 그룹

        static Transform ResolveGroup(RegionDefinition region)
        {
            Transform existing = FindGroup(region);
            if (existing != null) return existing;

            var group = new GameObject(region.DisplayName);
            Undo.RegisterCreatedObjectUndo(group, "경계 그룹 만들기");
            group.transform.SetParent(ResolveRoot(), false);
            return group.transform;
        }

        static Transform FindGroup(RegionDefinition region)
        {
            var root = GameObject.Find(BarrierRootName);
            return root == null ? null : root.transform.Find(region.DisplayName);
        }

        static Transform ResolveRoot()
        {
            var existing = GameObject.Find(BarrierRootName);
            if (existing != null) return existing.transform;

            var created = new GameObject(BarrierRootName);
            Undo.RegisterCreatedObjectUndo(created, "경계 그룹 만들기");
            return created.transform;
        }

        static void ClearGroup(Transform group)
        {
            for (int i = group.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(group.GetChild(i).gameObject);
            }
        }
    }
}
