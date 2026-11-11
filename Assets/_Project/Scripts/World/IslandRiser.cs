using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 잠긴 구역은 물 아래에 가라앉아 있다가, 플레이 중에 열리면 솟아오른다.
    ///
    /// 무엇을 들어 올리는지는 RegionLand가 답한다(몸통 + 칸 명부의 배치물). 씬 구조는 건드리지 않고
    /// 위치만 옮긴다 — 부모를 바꾸면 배치 팔레트·저장이 기대는 폴더 구조가 흔들린다.
    ///
    /// 신호 구분(RegionAccess):
    ///   Opened   → 플레이 중 개방: 연출과 함께 솟는다.
    ///   Unlocked → 저장 불러오기: 연출 없이 바로 올라와 있다.
    /// 플레이 중 개방은 두 신호가 같은 프레임에 같이 오므로, 프레임 끝에서 Opened가 있었는지로 가른다.
    [RequireComponent(typeof(RegionLand))]
    public class IslandRiser : MonoBehaviour
    {
        public enum State { Risen, Sunk, Rising }

        /// 가라앉음·솟는 중·다 솟음이 바뀔 때. 투명 경계벽이 이걸 듣고 다시 세운다.
        public static event Action<IslandRiser> StateChanged;

        static readonly Dictionary<RegionDefinition, IslandRiser> _byRegion = new Dictionary<RegionDefinition, IslandRiser>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            _byRegion.Clear();
            StateChanged = null;
        }

        public static IslandRiser For(RegionDefinition region) =>
            region != null && _byRegion.TryGetValue(region, out IslandRiser riser) ? riser : null;

        [SerializeField] RegionAccess _access;
        [SerializeField] SeaSurface _sea;
        [SerializeField] CameraRig _cameraRig;

        [Header("연출")]
        [Tooltip("잠겨 있을 때 가라앉은 깊이(m). 섬 몸통 절벽(−4m)보다 얕아야 솟기 전에 바닥이 안 보인다.")]
        [SerializeField] float _sinkDepth = 3f;
        [SerializeField] float _riseDuration = 2.5f;
        [Tooltip("다 올라오기 직전에 살짝 넘쳤다가 내려앉는 높이(m).")]
        [SerializeField] float _overshoot = 0.15f;
        [Tooltip("솟는 동안 땅이 떨리는 폭(m).")]
        [SerializeField] float _rumble = 0.05f;
        [SerializeField] float _cameraShake = 0.2f;

        /// 물 높이(SeaSurface와 같은 값). 땅 윗면이 이걸 넘는 순간부터 해안 거품이 생긴다.
        const float WaterLevel = -0.15f;

        public State Current { get; private set; } = State.Risen;
        public RegionDefinition Region => _land != null ? _land.Region : null;

        RegionLand _land;
        bool _pendingOpened;
        bool _pendingUnlocked;

        readonly List<Transform> _members = new List<Transform>();
        readonly List<Vector3> _restPositions = new List<Vector3>();
        readonly List<GameObject> _hiddenMonsters = new List<GameObject>();

        void Awake()
        {
            _land = GetComponent<RegionLand>();
            if (_access == null) _access = FindFirstObjectByType<RegionAccess>();
            if (_sea == null) _sea = FindFirstObjectByType<SeaSurface>();
            if (_cameraRig == null) _cameraRig = FindFirstObjectByType<CameraRig>();
        }

        void OnEnable()
        {
            if (Region != null) _byRegion[Region] = this;
            if (_access == null) return;

            _access.Opened += OnOpened;
            _access.Unlocked += OnUnlocked;
            _access.Locked += OnLocked;
        }

        void OnDisable()
        {
            if (Region != null && For(Region) == this) _byRegion.Remove(Region);
            if (_access == null) return;

            _access.Opened -= OnOpened;
            _access.Unlocked -= OnUnlocked;
            _access.Locked -= OnLocked;
        }

        // 배치물이 칸 명부에 등록을 마친 뒤라야 무엇을 가라앉힐지 안다. 그래서 Awake가 아니라 Start.
        void Start()
        {
            if (Region == null || !Region.IsLockable) return;
            if (_access != null && _access.IsUnlocked(Region)) return;

            Sink();
        }

        void OnOpened(RegionDefinition region)
        {
            if (region == Region) _pendingOpened = true;
        }

        void OnUnlocked(RegionDefinition region)
        {
            if (region == Region) _pendingUnlocked = true;
        }

        // 치트로 다시 잠그면 연출 없이 바로 가라앉는다(검증을 반복하기 쉽게).
        void OnLocked(RegionDefinition region)
        {
            if (region != Region) return;
            _pendingOpened = false;
            _pendingUnlocked = false;

            if (Current == State.Sunk) return;

            // 솟는 도중이면 제자리로 돌려 두고(숨긴 몬스터도 되살려서) 처음부터 다시 가라앉힌다.
            // 그대로 Sink하면 지금 흔들린 자리를 '원래 자리'로 기억하고, 숨겨둔 몬스터 목록을 잃는다.
            if (Current == State.Rising)
            {
                StopAllCoroutines();
                PlaceAt(0f, Vector3.zero);
                ShowMonsters();
            }

            Sink();
        }

        void LateUpdate()
        {
            if (!_pendingOpened && !_pendingUnlocked) return;

            bool animate = _pendingOpened;
            _pendingOpened = false;
            _pendingUnlocked = false;

            if (Current != State.Sunk) return;

            if (animate) StartCoroutine(RiseRoutine());
            else Surface();
        }

        /// 몸통과 배치물을 물 아래로 내린다. 몬스터는 꺼 둔다 — 물리·높이 고정이 끌어올리고,
        /// 가라앉은 섬에서 플레이어를 쫓아오면 안 된다.
        void Sink()
        {
            _land.CollectMembers(_members);
            _restPositions.Clear();
            _hiddenMonsters.Clear();

            for (int i = _members.Count - 1; i >= 0; i--)
            {
                Transform member = _members[i];
                if (member.TryGetComponent(out Monster _))
                {
                    if (member.gameObject.activeSelf)
                    {
                        _hiddenMonsters.Add(member.gameObject);
                        member.gameObject.SetActive(false);
                    }
                    _members.RemoveAt(i);
                }
            }

            for (int i = 0; i < _members.Count; i++) _restPositions.Add(_members[i].position);

            PlaceAt(-_sinkDepth, Vector3.zero);
            if (_sea != null) _sea.SetLandAmount(Region, 0f);

            SetState(State.Sunk);
        }

        /// 연출 없이 제자리로(저장 불러오기).
        void Surface()
        {
            PlaceAt(0f, Vector3.zero);
            Finish();
        }

        IEnumerator RiseRoutine()
        {
            SetState(State.Rising);

            if (_sea != null) _sea.PlayRipple(RegionCenter());
            if (_cameraRig != null) _cameraRig.Shake(_cameraShake, _riseDuration * 0.8f);

            float elapsed = 0f;
            while (elapsed < _riseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _riseDuration);

                float height = Mathf.Lerp(-_sinkDepth, 0f, EaseOutBack(t, _overshoot / _sinkDepth));

                // 떨림은 처음에 세고 다 올라올수록 잦아든다.
                float rumble = _rumble * (1f - t);
                var jitter = new Vector3(UnityEngine.Random.Range(-rumble, rumble), 0f, UnityEngine.Random.Range(-rumble, rumble));

                PlaceAt(height, jitter);
                if (_sea != null) _sea.SetLandAmount(Region, height > WaterLevel ? 1f : 0f);

                yield return null;
            }

            PlaceAt(0f, Vector3.zero);
            Finish();
        }

        void ShowMonsters()
        {
            for (int i = 0; i < _hiddenMonsters.Count; i++)
            {
                if (_hiddenMonsters[i] != null) _hiddenMonsters[i].SetActive(true);
            }
            _hiddenMonsters.Clear();
        }

        void Finish()
        {
            ShowMonsters();

            if (_sea != null) _sea.SetLandAmount(Region, 1f);
            SetState(State.Risen);
        }

        void PlaceAt(float height, Vector3 jitter)
        {
            var offset = new Vector3(jitter.x, height, jitter.z);
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i] != null) _members[i].position = _restPositions[i] + offset;
            }
        }

        void SetState(State state)
        {
            if (Current == state) return;
            Current = state;
            StateChanged?.Invoke(this);
        }

        readonly List<RectInt> _areas = new List<RectInt>();

        Vector3 RegionCenter()
        {
            RegionMap.CollectAreas(Region, _areas);
            if (_areas.Count == 0) return transform.position;

            Vector2 sum = Vector2.zero;
            int cells = 0;
            for (int i = 0; i < _areas.Count; i++)
            {
                int count = _areas[i].width * _areas[i].height;
                sum += _areas[i].center * count;
                cells += count;
            }

            Vector2 cell = sum / Mathf.Max(1, cells);
            MapGrid grid = MapGrid.Active;
            float size = grid != null ? grid.CellSize : 1f;
            Vector3 origin = grid != null ? grid.Origin : Vector3.zero;

            // RectInt.center는 칸 모서리 기준이라 반 칸 당긴다(칸 (0,0)의 중심이 원점).
            return origin + new Vector3((cell.x - 0.5f) * size, 0f, (cell.y - 0.5f) * size);
        }

        /// 끝에서 살짝 넘쳤다가 돌아오는 감속. overshoot는 전체 높이 대비 비율.
        static float EaseOutBack(float t, float overshoot)
        {
            // 넘치는 양이 overshoot가 되도록 계수를 근사한다(작은 값에서 c ≈ overshoot * 10).
            float c = Mathf.Max(0f, overshoot) * 10f;
            float u = t - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }
    }
}
