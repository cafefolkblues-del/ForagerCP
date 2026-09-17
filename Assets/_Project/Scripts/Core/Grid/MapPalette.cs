using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 배치 팔레트에 뜰 항목 목록. 디자이너가 에셋으로 직접 편집한다.
    /// 런타임 코드는 이 에셋을 읽지 않는다 — 배치는 전부 에디터 타임에 끝나고 씬에 박힌다.
    [CreateAssetMenu(menuName = "ForagerCP/Map Palette", fileName = "MapPalette")]
    public class MapPalette : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string DisplayName = "항목";
            public GameObject Prefab;
            public Vector2Int Size = Vector2Int.one;
            public float HeightOffset = 0.5f;
            public Color SwatchColor = new Color(0.4f, 0.8f, 1f);

            /// 같은 Category를 가진 MapFolder 밑으로 자동 분류된다.
            public string Category = "Structures";

            /// 회전이 의미 없는 것(1x1 광물 등)은 꺼서 디자이너가 헷갈리지 않게 한다.
            public bool AllowRotation = true;
        }

        [SerializeField] List<Entry> _entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => _entries;
    }
}
