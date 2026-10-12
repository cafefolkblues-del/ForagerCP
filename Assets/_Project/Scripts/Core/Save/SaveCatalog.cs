using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 저장 파일에 적힌 이름을 다시 에셋으로 되돌리는 명부.
    ///
    /// 저장에는 에셋을 이름("Mineral_Iron")으로 적는다. 사람이 열어 읽고 손으로 고칠 수 있어야
    /// 문제가 났을 때 원인을 찾을 수 있기 때문. 대신 이름만으로는 런타임에 에셋을 못 찾으므로
    /// 어딘가 한 곳이 전부를 들고 있어야 한다.
    ///
    /// Resources 폴더를 쓰지 않는 이유: 경로 규칙이 보이지 않는 곳에 숨고, 빌드에 무엇이 들어갔는지
    /// 확인할 방법이 없다. 명부를 에셋으로 두면 무엇이 등록됐는지 눈으로 보이고 검사기가 짚을 수 있다.
    /// 목록은 에디터 버튼으로 프로젝트에서 자동 수집한다 — 손으로 관리하면 반드시 빠뜨린다.
    [CreateAssetMenu(menuName = "ForagerCP/저장 명부", fileName = "SaveCatalog")]
    public class SaveCatalog : ScriptableObject
    {
        [SerializeField] List<ScriptableObject> _assets = new List<ScriptableObject>();

        public IReadOnlyList<ScriptableObject> Assets => _assets;

        Dictionary<string, ScriptableObject> _byName;

        /// 이름으로 찾는다. 못 찾거나 타입이 다르면 null —
        /// 호출하는 쪽은 "에셋이 사라졌다"로 보고 그 항목을 건너뛰면 된다.
        public T Find<T>(string assetName) where T : ScriptableObject
        {
            if (string.IsNullOrEmpty(assetName)) return null;

            BuildIndex();
            return _byName.TryGetValue(assetName, out ScriptableObject found) ? found as T : null;
        }

        /// 저장에 적을 이름. 에셋이 없으면 빈 문자열.
        public static string NameOf(ScriptableObject asset) => asset != null ? asset.name : string.Empty;

        void BuildIndex()
        {
            // 에디터에서 목록을 고치면 다시 지어야 한다. 개수로 가늠하는 건 거칠지만
            // 이 명부는 플레이 중에 바뀌지 않으므로 이걸로 충분하다.
            if (_byName != null && _byName.Count == CountValid()) return;

            _byName = new Dictionary<string, ScriptableObject>();
            for (int i = 0; i < _assets.Count; i++)
            {
                if (_assets[i] == null) continue;
                _byName[_assets[i].name] = _assets[i];
            }
        }

        int CountValid()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < _assets.Count; i++)
            {
                if (_assets[i] != null) seen.Add(_assets[i].name);
            }
            return seen.Count;
        }

        /// 같은 이름이 둘 이상이면 저장이 엉뚱한 에셋을 집는다. 검사기가 이걸 본다.
        public void CollectDuplicateNames(List<string> into)
        {
            into.Clear();

            var seen = new HashSet<string>();
            for (int i = 0; i < _assets.Count; i++)
            {
                if (_assets[i] == null) continue;
                if (!seen.Add(_assets[i].name)) into.Add(_assets[i].name);
            }
        }

#if UNITY_EDITOR
        /// 에디터 전용. 자동 수집 버튼이 쓴다.
        public void ReplaceAll(List<ScriptableObject> assets)
        {
            _assets.Clear();
            _assets.AddRange(assets);
            _byName = null;
        }
#endif
    }
}
