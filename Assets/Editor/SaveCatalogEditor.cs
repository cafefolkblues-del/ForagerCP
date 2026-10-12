using System.Collections.Generic;
using ForagerCP;
using UnityEditor;
using UnityEngine;

namespace ForagerCP.EditorTools
{
    /// 저장 명부를 프로젝트에서 자동으로 채운다.
    ///
    /// 손으로 관리하면 새 아이템·스킬을 만들 때마다 반드시 빠뜨리고, 빠진 것은 불러올 때
    /// 조용히 사라진다(그 칸만 버려지므로 오류도 안 난다). 버튼 하나로 다시 긁게 둔다.
    [CustomEditor(typeof(SaveCatalog))]
    public class SaveCatalogEditor : Editor
    {
        /// 저장 파일에 이름으로 적히는 에셋 종류들. 새 종류가 생기면 여기 한 줄 추가.
        static readonly string[] Kinds =
        {
            "ItemDefinition",   // 광물 등 모든 아이템(MineralDefinition 포함)
            "EquipmentDefinition",
            "CraftRecipe",
            "ShopGood",
            "BuffDefinition",
            "SkillDefinition",
            "RegionDefinition",
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8f);

            var catalog = (SaveCatalog)target;

            var duplicates = new List<string>();
            catalog.CollectDuplicateNames(duplicates);

            if (duplicates.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "이름이 겹칩니다: " + string.Join(", ", duplicates) +
                    "\n저장은 이름으로 에셋을 찾으므로, 겹치면 엉뚱한 것을 집습니다. 파일명을 바꿔주세요.",
                    MessageType.Error);
            }

            if (GUILayout.Button("프로젝트에서 다시 긁어오기", GUILayout.Height(30f))) Rebuild(catalog);

            EditorGUILayout.HelpBox(
                "저장 파일에는 에셋을 이름으로 적습니다. 여기 없는 에셋은 불러올 때 사라집니다.\n" +
                "새 아이템·스킬·구역·무기를 만들었으면 위 버튼을 한 번 눌러주세요.",
                MessageType.None);
        }

        static void Rebuild(SaveCatalog catalog)
        {
            var found = new List<ScriptableObject>();
            var seen = new HashSet<Object>();

            foreach (string kind in Kinds)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:" + kind))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset == null || !seen.Add(asset)) continue;

                    found.Add(asset);
                }
            }

            // 목록 순서가 흔들리면 에셋 파일 변경점이 매번 지저분해진다.
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            Undo.RecordObject(catalog, "저장 명부 다시 긁기");
            catalog.ReplaceAll(found);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            Debug.Log($"저장 명부: {found.Count}개 등록", catalog);
        }
    }
}
