using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ForagerCP.EditorTools
{
    /// 폰트 파일(.ttf)을 TMP가 쓸 수 있는 폰트 에셋으로 구워주는 도구.
    /// 디자이너는 폰트 파일을 폴더에 넣고 이 메뉴만 누르면 된다.
    public static class FontAssetBuilder
    {
        const string SourceFolder = "Assets/TextMesh Pro/Fonts";
        const string OutputFolder = "Assets/_Project/Fonts";

        [MenuItem("Tools/ForagerCP/폰트 에셋 만들기")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder("Assets/_Project", "Fonts");

            var created = new List<string>();
            int skipped = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Font", new[] { SourceFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // otf/ttf가 같이 든 배포본이 많아 ttf만 쓴다. 폰트 임포터 호환이 더 안정적.
                if (!path.ToLower().EndsWith(".ttf")) continue;

                string assetName = Path.GetFileNameWithoutExtension(path) + " SDF";
                string outPath = OutputFolder + "/" + assetName + ".asset";

                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath) != null) { skipped++; continue; }

                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font == null) continue;

                // Dynamic: 실제로 쓰는 글자만 아틀라스에 굽는다.
                // 한글 전체를 미리 구우면 아틀라스가 수십 MB로 불어난다.
                TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                    font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);

                if (fontAsset == null) continue;

                fontAsset.name = assetName;
                AssetDatabase.CreateAsset(fontAsset, outPath);

                if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0)
                {
                    fontAsset.atlasTextures[0].name = assetName + " Atlas";
                    AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
                }
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = assetName + " Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }

                EditorUtility.SetDirty(fontAsset);
                created.Add(assetName);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string message = created.Count == 0
                ? "새로 만든 폰트가 없습니다. (이미 만들어진 폰트 " + skipped + "개)"
                : "폰트 에셋 " + created.Count + "개를 만들었습니다:\n" + string.Join("\n", created);

            EditorUtility.DisplayDialog("폰트 에셋 만들기", message, "확인");
        }
    }
}
