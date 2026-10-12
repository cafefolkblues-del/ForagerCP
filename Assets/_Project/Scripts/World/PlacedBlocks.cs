using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 플레이어가 놓은 블럭을 만들고, 저장에 내놓고, 돌려받는다.
    ///
    /// 만드는 일을 BlockPlacer에서 떼어낸 이유: 블럭은 두 경로로 생긴다 — 플레이어가 E로 놓거나,
    /// 저장에서 돌아오거나. 두 곳이 각자 만들면 한쪽만 고치는 사고가 난다.
    ///
    /// 저장할 때 목록을 따로 들지 않고 씬을 훑는다. 목록을 들면 '씬'과 '목록' 두 군데가 진실을
    /// 주장하게 되고, 블럭이 다른 이유로 사라지는 경로(몹이 부수거나 에디터에서 지우거나)가
    /// 생기는 순간 저장이 조용히 틀린다. 훑는 비용은 저장이 몇 분에 한 번이라 문제되지 않는다.
    public class PlacedBlocks : MonoBehaviour, ISaveable
    {
        [SerializeField] SaveCatalog _catalog;
        [SerializeField] HarvestRewardService _rewards;

        /// 놓인 것을 담을 그룹. 비어 있으면 씬 최상위에 놓인다.
        [SerializeField] MapFolder _folder;

        /// 놓인 것의 바닥이 지면에 닿도록 올리는 높이. 1m 큐브 기준 0.5.
        [SerializeField] float _heightOffset = 0.5f;

        public float HeightOffset => _heightOffset;

        public event Action<PlacedBlock> Created;

        // ------------------------------------------------------------------ 만들기

        public GameObject Place(ItemDefinition item, Vector2Int cell)
        {
            if (item == null || item.PlacePrefab == null) return null;

            Transform parent = _folder != null ? _folder.transform : null;
            GameObject instance = Instantiate(item.PlacePrefab, parent);
            instance.name = item.DisplayName;

            if (!instance.TryGetComponent(out GridPlacement placement)) placement = instance.AddComponent<GridPlacement>();
            placement.Configure(cell, placement.Size, _heightOffset);

            if (!instance.TryGetComponent(out PlacedBlock mark)) mark = instance.AddComponent<PlacedBlock>();
            mark.Setup(item);

            if (instance.TryGetComponent(out MineralNode node))
            {
                // 되살아나거나 경험치를 주면 무한 복제·무한 파밍이 된다.
                node.MarkAsPlaced();

                // 시작 뒤에 생긴 노드라 보상 서비스가 아직 모른다. 등록해야 캤을 때 드랍이 나온다.
                if (_rewards != null) _rewards.Track(node);
            }

            Created?.Invoke(mark);
            return instance;
        }

        // ------------------------------------------------------------------ 저장

        [Serializable]
        class SavedBlock
        {
            public string Item;
            public int X;
            public int Y;
        }

        [Serializable]
        class Save
        {
            public List<SavedBlock> Blocks = new List<SavedBlock>();
        }

        public string SaveKey => "placedBlocks";

        public string Capture()
        {
            var save = new Save();

            foreach (PlacedBlock block in FindObjectsByType<PlacedBlock>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (block.Source == null) continue;
                if (!block.TryGetComponent(out GridPlacement placement)) continue;

                save.Blocks.Add(new SavedBlock
                {
                    Item = SaveCatalog.NameOf(block.Source),
                    X = placement.Cell.x,
                    Y = placement.Cell.y,
                });
            }

            // 찾는 순서는 보장되지 않는다. 정렬해야 같은 상태가 늘 같은 글자로 나온다.
            save.Blocks.Sort((a, b) => a.Y != b.Y ? a.Y - b.Y
                : a.X != b.X ? a.X - b.X
                : string.CompareOrdinal(a.Item, b.Item));

            return JsonUtility.ToJson(save);
        }

        public void Restore(string payload)
        {
            var save = JsonUtility.FromJson<Save>(payload);
            if (save == null) return;

            ClearAll();

            for (int i = 0; i < save.Blocks.Count; i++)
            {
                SavedBlock saved = save.Blocks[i];

                // 아이템이 지워졌으면 그 블럭만 건너뛴다. 저장 전체를 버리면
                // 아이템 하나 정리했다고 깔아둔 것이 통째로 날아간다.
                ItemDefinition item = _catalog != null ? _catalog.Find<ItemDefinition>(saved.Item) : null;
                if (item == null) continue;

                Place(item, new Vector2Int(saved.X, saved.Y));
            }
        }

        /// 돌려받기 전에 지금 놓인 것을 치운다. 안 치우면 같은 칸에 두 개가 겹친다.
        void ClearAll()
        {
            foreach (PlacedBlock block in FindObjectsByType<PlacedBlock>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // DestroyImmediate를 쓰는 이유: 보통 Destroy는 프레임 끝에 도는데,
                // 그 사이에 복원이 새 블럭을 놓고 다시 훑으면 옛 것이 그대로 잡혀 두 배로 늘어난다.
                // 불러오기는 한 번에 끝나야 하는 일이라 그 자리에서 지운다.
                DestroyImmediate(block.gameObject);
            }
        }
    }
}
