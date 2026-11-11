using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ForagerCP
{
    /// 구역 바깥을 덮는 바다 평면 한 장. 섬(구역 땅)은 이 물을 뚫고 서 있고, 물은 섬을 모른다.
    ///
    /// 카메라가 맵 안에 묶여 있어서 How to Fish처럼 타일을 플레이어 따라 옮길 필요가 없다 —
    /// 맵 범위에 여유를 둔 평면 하나면 화면 끝까지 물이다.
    ///
    /// 메시는 저장하지 않고 켜질 때마다 만든다. 씬 파일에 정점 수천 개가 들어가면 diff를 읽을 수 없다.
    ///
    /// 해안 거품과 얕은 물빛은 "가장 가까운 땅까지의 거리" 텍스처로 칠한다. 깊이 텍스처만 쓰면
    /// 내려다보는 카메라에서는 섬의 카메라 쪽 한 면에만 거품이 생긴다(물 뒤에 잠긴 면이 보여야 하므로).
    /// 땅 모양은 구역 셀(RegionMap)에서 온다 — 씬을 훑지 않고, 구역이 바뀌면 그때 다시 그린다.
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SeaSurface : MonoBehaviour
    {
        static readonly int RippleCenterId = Shader.PropertyToID("_SeaRippleCenter");
        static readonly int RippleParamsId = Shader.PropertyToID("_SeaRippleParams");
        static readonly int ShoreTexId = Shader.PropertyToID("_SeaShoreTex");
        static readonly int ShoreRectId = Shader.PropertyToID("_SeaShoreRect");

        [Tooltip("물이 깔릴 셀 범위(구역 전체). '구역 범위에 맞추기'로 채운다.")]
        [SerializeField] RectInt _area = new RectInt(-18, -5, 42, 31);

        [Tooltip("범위 밖으로 더 까는 칸 수. 카메라가 맵 끝에 있을 때 화면 가장자리까지 덮어야 한다.")]
        [SerializeField] int _margin = 25;

        [Tooltip("한 칸을 몇 조각으로 나눌지. 파도가 정점을 움직이므로 너무 성기면 물결이 안 보인다.")]
        [SerializeField, Range(1, 4)] int _subdivisions = 1;

        [Tooltip("물 높이. 땅 윗면(0)보다 살짝 낮게 둬야 땅 가장자리에 거품이 생긴다.")]
        [SerializeField] float _height = -0.15f;

        [Header("해안 거리")]
        [Tooltip("한 칸을 몇 텍셀로 나눌지. 2면 해안선이 계단처럼 보이지 않는다.")]
        [SerializeField, Range(1, 4)] int _shoreTexelsPerCell = 2;

        [Tooltip("이보다 먼 바다는 전부 '먼 바다'로 친다(m).")]
        [SerializeField] float _maxShoreDistance = 8f;

        [Header("물결 고리 (섬이 솟을 때)")]
        [SerializeField] float _rippleSpeed = 6f;
        [SerializeField] float _rippleWidth = 1.2f;
        [SerializeField] float _rippleHeight = 0.35f;
        [SerializeField] float _rippleDuration = 3f;

        Mesh _mesh;
        Texture2D _shoreTex;
        bool _shoreDirty;

        /// 구역마다 땅이 물 위로 얼마나 올라왔는가(0~1). 없으면 1 — 지금 씬의 구역은 전부 땅이다.
        /// 섬 솟아오름 연출이 0에서 1로 올린다.
        readonly Dictionary<RegionDefinition, float> _landAmount = new Dictionary<RegionDefinition, float>();

        void OnEnable()
        {
            RegionMap.Changed += MarkShoreDirty;
            Build();
            BuildShore();
            // 지난 판의 고리가 남아 있지 않게 끝난 시각으로 돌려 둔다.
            Shader.SetGlobalVector(RippleCenterId, new Vector4(0, 0, 0, -1000f));
            Shader.SetGlobalVector(RippleParamsId, RippleParams);
        }

        void OnDisable()
        {
            RegionMap.Changed -= MarkShoreDirty;

            Release(_mesh);
            Release(_shoreTex);
            _mesh = null;
            _shoreTex = null;
        }

        static void Release(Object asset)
        {
            if (asset == null) return;
            if (Application.isPlaying) Destroy(asset);
            else DestroyImmediate(asset);
        }

        void MarkShoreDirty() => _shoreDirty = true;

        // 볼륨은 이 컴포넌트보다 늦게 켜질 수 있어서, 바뀐 걸 모아 두었다가 프레임 끝에 한 번만 다시 그린다.
        void LateUpdate()
        {
            if (_shoreDirty) BuildShore();
        }

        /// 구역 땅이 물 위로 올라온 정도. 절반을 넘으면 해안선으로 친다.
        public void SetLandAmount(RegionDefinition region, float amount)
        {
            if (region == null) return;

            amount = Mathf.Clamp01(amount);
            bool wasLand = LandAmount(region) >= 0.5f;
            _landAmount[region] = amount;

            if (wasLand != amount >= 0.5f) BuildShore();
        }

        float LandAmount(RegionDefinition region) =>
            region != null && _landAmount.TryGetValue(region, out float amount) ? amount : 1f;

        void OnValidate()
        {
            if (!isActiveAndEnabled) return;

            // OnValidate 안에서는 메시를 바로 갈아끼울 수 없어(경고) 한 프레임 미룬다.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled) return;
                Build();
                BuildShore();
            };
#endif
        }

        Vector4 RippleParams => new Vector4(_rippleSpeed, _rippleWidth, _rippleHeight, _rippleDuration);

        /// 이 지점에서 물결 고리를 하나 퍼뜨린다. 섬 솟아오름 연출이 부른다.
        /// 고리는 한 번에 하나만 — 동시에 두 섬이 솟는 일은 없다.
        public void PlayRipple(Vector3 center)
        {
            Shader.SetGlobalVector(RippleParamsId, RippleParams);
            Shader.SetGlobalVector(RippleCenterId, new Vector4(center.x, center.y, center.z, Time.timeSinceLevelLoad));
        }

        /// 텍셀마다 가장 가까운 해안선까지의 거리(m). 바다는 +, 땅 아래는 −.
        /// 8방향 2회 훑기(chamfer)로 구한다 — 정확한 유클리드는 아니지만 거품 폭 정도에선 구분이 안 된다.
        void BuildShore()
        {
            _shoreDirty = false;

            MapGrid grid = MapGrid.Active;
            float cell = grid != null ? grid.CellSize : 1f;
            Vector3 origin = grid != null ? grid.Origin : Vector3.zero;

            int perCell = Mathf.Max(1, _shoreTexelsPerCell);
            int width = (_area.width + _margin * 2) * perCell;
            int height = (_area.height + _margin * 2) * perCell;
            float texel = cell / perCell;

            float startX = origin.x + (_area.xMin - _margin - 0.5f) * cell;
            float startZ = origin.z + (_area.yMin - _margin - 0.5f) * cell;

            var land = new bool[width * height];
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    var world = new Vector3(startX + (x + 0.5f) * texel, 0f, startZ + (z + 0.5f) * texel);
                    Vector2Int at = grid != null
                        ? grid.WorldToCell(world)
                        : new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
                    RegionDefinition region = RegionMap.At(at);
                    land[z * width + x] = region != null && LandAmount(region) >= 0.5f;
                }
            }

            float[] toLand = Chamfer(land, width, height, true);
            float[] toWater = Chamfer(land, width, height, false);

            var pixels = new ushort[width * height];
            float half = texel * 0.5f;
            for (int i = 0; i < pixels.Length; i++)
            {
                float signedMeters = land[i]
                    ? -(toWater[i] * texel - half)
                    : toLand[i] * texel - half;
                pixels[i] = Mathf.FloatToHalf(Mathf.Clamp(signedMeters, -_maxShoreDistance, _maxShoreDistance));
            }

            if (_shoreTex == null || _shoreTex.width != width || _shoreTex.height != height)
            {
                Release(_shoreTex);
                _shoreTex = new Texture2D(width, height, TextureFormat.RHalf, false, true)
                {
                    name = "SeaShoreDistance",
                    hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
            }
            _shoreTex.SetPixelData(pixels, 0);
            _shoreTex.Apply(false, false);

            Shader.SetGlobalTexture(ShoreTexId, _shoreTex);
            Shader.SetGlobalVector(ShoreRectId, new Vector4(startX, startZ, width * texel, height * texel));
        }

        /// target 쪽 텍셀까지의 거리(텍셀 단위). target이면 0.
        static float[] Chamfer(bool[] land, int width, int height, bool targetIsLand)
        {
            const float Straight = 1f;
            const float Diagonal = 1.41421356f;
            const float Far = 1e6f;

            var d = new float[land.Length];
            for (int i = 0; i < d.Length; i++) d[i] = land[i] == targetIsLand ? 0f : Far;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    if (d[i] == 0f) continue;
                    float best = d[i];
                    if (x > 0) best = Mathf.Min(best, d[i - 1] + Straight);
                    if (z > 0)
                    {
                        best = Mathf.Min(best, d[i - width] + Straight);
                        if (x > 0) best = Mathf.Min(best, d[i - width - 1] + Diagonal);
                        if (x < width - 1) best = Mathf.Min(best, d[i - width + 1] + Diagonal);
                    }
                    d[i] = best;
                }
            }

            for (int z = height - 1; z >= 0; z--)
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    int i = z * width + x;
                    if (d[i] == 0f) continue;
                    float best = d[i];
                    if (x < width - 1) best = Mathf.Min(best, d[i + 1] + Straight);
                    if (z < height - 1)
                    {
                        best = Mathf.Min(best, d[i + width] + Straight);
                        if (x < width - 1) best = Mathf.Min(best, d[i + width + 1] + Diagonal);
                        if (x > 0) best = Mathf.Min(best, d[i + width - 1] + Diagonal);
                    }
                    d[i] = best;
                }
            }
            return d;
        }

        void Build()
        {
            MapGrid grid = MapGrid.Active;
            float cell = grid != null ? grid.CellSize : 1f;
            Vector3 origin = grid != null ? grid.Origin : Vector3.zero;

            int columns = (_area.width + _margin * 2) * _subdivisions;
            int rows = (_area.height + _margin * 2) * _subdivisions;
            float step = cell / _subdivisions;

            // 셀 (0,0)의 중심이 그리드 원점이므로 셀 경계는 반 칸 밀려 있다.
            float startX = origin.x + (_area.xMin - _margin - 0.5f) * cell;
            float startZ = origin.z + (_area.yMin - _margin - 0.5f) * cell;

            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            for (int z = 0, i = 0; z <= rows; z++)
            {
                for (int x = 0; x <= columns; x++, i++)
                {
                    vertices[i] = new Vector3(startX + x * step, 0f, startZ + z * step);
                }
            }

            var triangles = new int[columns * rows * 6];
            for (int z = 0, t = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int a = z * (columns + 1) + x;
                    int b = a + columns + 1;

                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = a + 1;
                    triangles[t++] = a + 1; triangles[t++] = b; triangles[t++] = b + 1;
                }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "SeaSurface", hideFlags = HideFlags.DontSave };
                _mesh.indexFormat = IndexFormat.UInt32;
            }
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();

            // 파도가 정점을 위아래로 움직이니 컬링 상자를 넉넉히 키운다. 안 그러면 화면 끝에서 물이 깜빡 사라진다.
            Bounds bounds = _mesh.bounds;
            bounds.Expand(new Vector3(0f, 4f, 0f));
            _mesh.bounds = bounds;

            GetComponent<MeshFilter>().sharedMesh = _mesh;

            // 위치는 높이만 쓴다. 정점이 이미 월드 XZ로 놓였으므로 오브젝트를 옮기면 물이 어긋난다.
            transform.SetPositionAndRotation(new Vector3(0f, _height, 0f), Quaternion.identity);
            transform.localScale = Vector3.one;

            var renderer = GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

#if UNITY_EDITOR
        /// 씬의 구역 볼륨을 전부 덮도록 범위를 맞춘다. 편집 중에 한 번 누르는 버튼이라 씬을 훑어도 된다.
        [ContextMenu("구역 범위에 맞추기")]
        void FitToRegions()
        {
            var volumes = new List<RegionVolume>(FindObjectsByType<RegionVolume>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (volumes.Count == 0) return;

            RectInt area = volumes[0].Area;
            for (int i = 1; i < volumes.Count; i++)
            {
                RectInt next = volumes[i].Area;
                int xMin = Mathf.Min(area.xMin, next.xMin);
                int yMin = Mathf.Min(area.yMin, next.yMin);
                int xMax = Mathf.Max(area.xMax, next.xMax);
                int yMax = Mathf.Max(area.yMax, next.yMax);
                area = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
            }

            UnityEditor.Undo.RecordObject(this, "구역 범위에 맞추기");
            _area = area;
            Build();
            BuildShore();
        }
#endif
    }
}
