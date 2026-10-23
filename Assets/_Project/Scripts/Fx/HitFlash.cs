using System.Collections;
using UnityEngine;

namespace ForagerCP
{
    /// 한 대 맞을 때마다 하얗게 점멸 + 크기 순간 증감.
    /// 광물·몬스터가 같이 쓰는 공용 연출이라 대상 타입을 모르게 만들어 둔다.
    [DisallowMultipleComponent]
    public class HitFlash : MonoBehaviour
    {
        [SerializeField] Color _flashColor = Color.white;
        [SerializeField] float _flashDuration = 0.07f;
        [SerializeField] float _punchScale = 0.34f;
        [SerializeField] float _punchDuration = 0.16f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Renderer[] _renderers;
        MaterialPropertyBlock _block;
        Color[] _baseColors;
        bool[] _painted;
        Coroutine _routine;
        Vector3 _baseScale;
        bool _scaleCaptured;

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        public void Play()
        {
            if (_renderers == null || _renderers.Length == 0) return;

            // 스케일 원본은 처음 맞을 때 잡는다.
            // Awake에서 잡으면 GridPlacement가 나중에 스케일을 맞추는 경우 값이 어긋난다.
            if (!_scaleCaptured)
            {
                _baseScale = transform.localScale;
                _scaleCaptured = true;
            }

            // 점멸 중에 또 맞으면 블록에 흰색이 섞여 있다. 그때 잡으면 흰색이 원래 색으로 굳는다.
            if (_routine != null) StopCoroutine(_routine);
            else CaptureBaseColors();

            _routine = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            SetTint(_flashColor, 1f);
            transform.localScale = _baseScale * (1f + _punchScale);

            float elapsed = 0f;
            float total = Mathf.Max(_flashDuration, _punchDuration);

            while (elapsed < total)
            {
                elapsed += Time.deltaTime;

                float flash = _flashDuration <= 0f ? 0f : Mathf.Clamp01(1f - elapsed / _flashDuration);
                SetTint(_flashColor, flash);

                float punch = _punchDuration <= 0f ? 0f : Mathf.Clamp01(1f - elapsed / _punchDuration);
                transform.localScale = _baseScale * (1f + _punchScale * punch);

                yield return null;
            }

            ClearTint();
            transform.localScale = _baseScale;
            _routine = null;
        }

        /// MaterialPropertyBlock을 쓰는 이유: 같은 머티리얼을 공유하는 광물이 여러 개라
        /// material을 직접 건드리면 인스턴스가 복제되거나 다른 광물까지 같이 하얘진다.
        void SetTint(Color color, float weight)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null || renderer.sharedMaterial == null) continue;

                renderer.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, Color.Lerp(_baseColors[i], color, weight));
                renderer.SetPropertyBlock(_block);
            }
        }

        /// 맞기 직전의 색을 잡는다. 머티리얼 색만 보면 Monster.Paint가 블록에 칠한
        /// 몸 색을 모른 채 머티리얼 색으로 되돌린다.
        void CaptureBaseColors()
        {
            if (_baseColors == null || _baseColors.Length != _renderers.Length)
            {
                _baseColors = new Color[_renderers.Length];
                _painted = new bool[_renderers.Length];
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null || renderer.sharedMaterial == null) continue;

                renderer.GetPropertyBlock(_block);
                _painted[i] = _block.HasColor(BaseColorId);

                if (_painted[i]) _baseColors[i] = _block.GetColor(BaseColorId);
                else if (renderer.sharedMaterial.HasProperty(BaseColorId)) _baseColors[i] = renderer.sharedMaterial.GetColor(BaseColorId);
                else _baseColors[i] = Color.white;
            }
        }

        /// 블록을 통째로 비우지 않는다 — 다른 컴포넌트가 칠해둔 색(몬스터 몸 색)까지 지워진다.
        /// 원래 블록에 색이 있었으면 그 색으로, 없었으면 예전처럼 비워 머티리얼 색으로 돌린다.
        void ClearTint()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;

                _renderers[i].GetPropertyBlock(_block);
                if (_painted != null && _painted[i]) _block.SetColor(BaseColorId, _baseColors[i]);
                else _block.Clear();
                _renderers[i].SetPropertyBlock(_block);
            }
        }
    }
}
