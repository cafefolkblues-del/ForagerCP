using System;
using UnityEngine;

namespace ForagerCP
{
    /// 설치가 거부된 이유. 나중에 "여기엔 못 놓습니다" 안내를 붙일 때 이 값만 보면 된다.
    public enum PlaceResult
    {
        Ok,
        NoItem,        // 고른 칸이 비었거나 수량이 0
        NotPlaceable,  // 설치용 모양이 없는 아이템(무기·재료 등)
        NoGrid,
        OutOfRange,
        CellTaken,     // 이미 뭔가 놓여 있는 칸
        Occupied,      // 플레이어나 몬스터가 서 있는 칸
    }

    /// 핫바에서 고른 것을 커서가 가리키는 칸에 놓는다(E).
    ///
    /// 무엇을 놓을지는 QuickBar만, 놓을 수 있는지는 GridOccupancy만 묻는다.
    /// UI도 씬 전체 탐색도 여기서 하지 않는다 — 나중에 철거나 다른 설치물이 붙어도 같은 창구를 쓰게.
    public class BlockPlacer : MonoBehaviour
    {
        [SerializeField] GameInput _input;
        [SerializeField] QuickBar _quickBar;
        [SerializeField] Inventory _inventory;
        [SerializeField] PlacedBlocks _blocks;
        [SerializeField] Camera _camera;
        [SerializeField] Transform _player;

        [Header("규칙")]
        /// 플레이어로부터 이 거리(m)까지만 놓을 수 있다.
        [SerializeField] float _range = 4f;

        /// 그 칸에 살아있는 것이 서 있는지 볼 반경. 셀 반폭보다 살짝 작게 둬야
        /// 옆 칸에 붙어 선 플레이어까지 걸려 못 놓는 일이 없다.
        [SerializeField] float _occupantCheckRadius = 0.4f;

        [Header("미리보기")]
        /// 어느 칸에 놓이는지 보여주는 표식. 없어도 설치 자체는 동작한다.
        [SerializeField] Transform _ghost;
        [SerializeField] Color _allowedColor = new Color(0.35f, 1f, 0.5f, 0.35f);
        [SerializeField] Color _blockedColor = new Color(1f, 0.35f, 0.3f, 0.35f);

        public event Action<ItemDefinition, Vector2Int> Placed;
        public event Action<PlaceResult> Rejected;

        /// 지금 커서가 가리키는 칸. 미리보기와 설치가 같은 값을 보도록 프레임당 한 번만 계산한다.
        public Vector2Int TargetCell { get; private set; }
        public PlaceResult CurrentResult { get; private set; } = PlaceResult.NoItem;

        readonly Collider[] _occupants = new Collider[8];
        Renderer _ghostRenderer;
        MaterialPropertyBlock _ghostBlock;
        bool _hasTarget;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            if (_input == null) _input = FindFirstObjectByType<GameInput>();
            if (_quickBar == null) _quickBar = FindFirstObjectByType<QuickBar>();
            if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
            if (_blocks == null) _blocks = FindFirstObjectByType<PlacedBlocks>();
            if (_camera == null) _camera = Camera.main;
            if (_player == null) _player = transform;

            if (_ghost == null) return;

            _ghostRenderer = _ghost.GetComponentInChildren<Renderer>();
            _ghostBlock = new MaterialPropertyBlock();
            _ghost.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            if (_input != null) _input.PlacePressed += TryPlace;
        }

        void OnDisable()
        {
            if (_input != null) _input.PlacePressed -= TryPlace;
            if (_ghost != null) _ghost.gameObject.SetActive(false);
        }

        void Update()
        {
            _hasTarget = TryGetTargetCell(out Vector2Int cell);
            TargetCell = cell;
            CurrentResult = _hasTarget ? Evaluate(cell, out _) : PlaceResult.NoGrid;

            UpdateGhost();
        }

        /// 지면 평면에 레이를 쏜다. 실제 바닥 콜라이더를 맞히면 구조물 윗면에 걸려
        /// 커서가 가리키는 것과 다른 칸이 잡힌다. 평면은 언제나 같은 답을 준다.
        bool TryGetTargetCell(out Vector2Int cell)
        {
            cell = default;

            MapGrid grid = MapGrid.Active;
            if (grid == null || _camera == null || _input == null) return false;

            var ground = new Plane(Vector3.up, grid.Origin);
            Ray ray = _camera.ScreenPointToRay(_input.PointerPosition);
            if (!ground.Raycast(ray, out float distance)) return false;

            cell = grid.WorldToCell(ray.GetPoint(distance));
            return true;
        }

        PlaceResult Evaluate(Vector2Int cell, out ItemDefinition item)
        {
            item = _quickBar != null ? _quickBar.SelectedItem : null;
            if (item == null || _quickBar.SelectedCount <= 0) return PlaceResult.NoItem;
            if (!item.CanPlace) return PlaceResult.NotPlaceable;

            MapGrid grid = MapGrid.Active;
            if (grid == null) return PlaceResult.NoGrid;

            Vector2Int size = FootprintOf(item);
            Vector3 center = grid.CellToWorld(cell, size);

            // 거리는 평면상으로 잰다. 높이 차이까지 섞으면 같은 칸인데 지형에 따라 사거리가 달라진다.
            Vector3 flatPlayer = _player.position;
            flatPlayer.y = center.y;
            if ((center - flatPlayer).sqrMagnitude > _range * _range) return PlaceResult.OutOfRange;

            if (!GridOccupancy.IsFree(new RectInt(cell.x, cell.y, size.x, size.y))) return PlaceResult.CellTaken;

            // 살아있는 것은 그리드에 매이지 않아 레지스트리에 없다. 이것만 물리로 확인한다.
            if (HasOccupant(center + Vector3.up * HeightOffset)) return PlaceResult.Occupied;

            return PlaceResult.Ok;
        }

        /// 놓는 높이는 만드는 쪽이 들고 있다. 여기서 따로 두면 두 값이 어긋나 미리보기와 결과가 달라진다.
        float HeightOffset => _blocks != null ? _blocks.HeightOffset : 0.5f;

        /// 프리팹이 들고 있는 크기를 그대로 쓴다(2칸 구조물 대비). 인스턴스를 만들지 않고 읽는다.
        static Vector2Int FootprintOf(ItemDefinition item)
        {
            if (item == null || item.PlacePrefab == null) return Vector2Int.one;
            return item.PlacePrefab.TryGetComponent(out GridPlacement placement) ? placement.Size : Vector2Int.one;
        }

        bool HasOccupant(Vector3 center)
        {
            int count = Physics.OverlapSphereNonAlloc(
                center, _occupantCheckRadius, _occupants, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (_occupants[i] == null) continue;
                if (_occupants[i].GetComponentInParent<IDamageable>() != null) return true;
            }
            return false;
        }

        void TryPlace()
        {
            if (!_hasTarget) return;

            PlaceResult result = Evaluate(TargetCell, out ItemDefinition item);
            if (result != PlaceResult.Ok)
            {
                Rejected?.Invoke(result);
                return;
            }

            // 먼저 빼고 나중에 만든다. 반대로 하면 빼기가 실패했을 때 블럭만 남아 복제가 된다.
            if (_inventory.Remove(item, 1) < 1) return;

            // 만드는 일은 PlacedBlocks가 한다 — 저장에서 돌아올 때도 같은 길을 타야 하므로.
            _blocks.Place(item, TargetCell);

            Placed?.Invoke(item, TargetCell);
        }

        void UpdateGhost()
        {
            if (_ghost == null) return;

            // 놓을 것이 없을 때도 표식을 띄우면 화면이 지저분해진다. 고른 것이 설치물일 때만 보인다.
            bool visible = _hasTarget
                && CurrentResult != PlaceResult.NoItem
                && CurrentResult != PlaceResult.NotPlaceable
                && CurrentResult != PlaceResult.NoGrid;

            if (_ghost.gameObject.activeSelf != visible) _ghost.gameObject.SetActive(visible);
            if (!visible) return;

            MapGrid grid = MapGrid.Active;
            Vector2Int size = FootprintOf(_quickBar.SelectedItem);

            _ghost.position = grid.CellToWorld(TargetCell, size) + Vector3.up * 0.02f;
            _ghost.localScale = new Vector3(size.x * grid.CellSize, 1f, size.y * grid.CellSize);

            if (_ghostRenderer == null) return;

            // MaterialPropertyBlock: 색만 바꾸려고 머티리얼을 복제하면 인스턴스가 새는 것과 같다.
            _ghostRenderer.GetPropertyBlock(_ghostBlock);
            _ghostBlock.SetColor(BaseColorId, CurrentResult == PlaceResult.Ok ? _allowedColor : _blockedColor);
            _ghostRenderer.SetPropertyBlock(_ghostBlock);
        }
    }
}
