using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForagerCP.EditorTools
{
    /// 게임 안의 모든 글자 폰트를 한 번에 바꾸는 도구.
    /// 씬에 놓인 글자 / 프리팹 안의 글자 / 앞으로 새로 만들 글자(기본값)까지 한 버튼으로 처리한다.
    public class FontSwapWindow : EditorWindow
    {
        // 한글 확인용 표본: 광 물 골 드
        static readonly char[] HangulSamples = { (char)0xAD11, (char)0xBB3C, (char)0xACE8, (char)0xB4DC };

        /// 프리팹 교체 범위. 프로젝트 전체를 훑으면 TMP 샘플 프리팹까지 바꿔버려서 게임 폴더로 제한한다.
        static readonly string[] PrefabSearchFolders = { "Assets/_Project" };

        [SerializeField] TMP_FontAsset _selected;
        [SerializeField] bool _applyToScenes = true;
        [SerializeField] bool _applyToPrefabs = true;
        [SerializeField] bool _applyToDefault = true;

        Vector2 _scroll;
        string _status = "";
        List<TMP_FontAsset> _fonts = new List<TMP_FontAsset>();

        [MenuItem("Tools/ForagerCP/폰트 바꾸기")]
        static void Open() => GetWindow<FontSwapWindow>("폰트 바꾸기");

        void OnEnable() => RefreshFontList();

        void RefreshFontList()
        {
            _fonts.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null) _fonts.Add(font);
            }

            if (_selected == null && _fonts.Count > 0) _selected = _fonts[0];
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("폰트 고르기", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(120f));
            foreach (TMP_FontAsset font in _fonts)
            {
                bool selected = font == _selected;
                GUI.backgroundColor = selected ? new Color(0.5f, 0.85f, 1f) : Color.white;

                string mark = selected ? "● " : "  ";
                string korean = SupportsKorean(font) ? "" : "   (한글 없음)";
                if (GUILayout.Button(mark + font.name + korean, GUILayout.Height(24f))) _selected = font;

                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("목록 새로고침", EditorStyles.miniButton)) RefreshFontList();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("어디에 적용할까요", EditorStyles.boldLabel);
            _applyToScenes = EditorGUILayout.ToggleLeft("지금 열려 있는 씬의 글자", _applyToScenes);
            _applyToPrefabs = EditorGUILayout.ToggleLeft("프리팹 안의 글자 (게임 프리팹만)", _applyToPrefabs);
            _applyToDefault = EditorGUILayout.ToggleLeft("앞으로 새로 만들 글자의 기본 폰트", _applyToDefault);

            EditorGUILayout.Space(10f);

            using (new EditorGUI.DisabledScope(_selected == null))
            {
                GUI.backgroundColor = new Color(0.45f, 1f, 0.6f);
                if (GUILayout.Button("이 폰트로 전부 바꾸기", GUILayout.Height(36f))) Apply();
                GUI.backgroundColor = Color.white;
            }

            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.Info);

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "새 폰트 파일은 Assets/TextMesh Pro/Fonts 에 넣고,\n" +
                "Tools ▸ ForagerCP ▸ 폰트 에셋 만들기 를 한 번 눌러주세요.\n" +
                "그러면 위 목록에 나타납니다.",
                MessageType.None);
        }

        static bool SupportsKorean(TMP_FontAsset font)
        {
            if (font == null) return false;

            foreach (char sample in HangulSamples)
            {
                if (!font.HasCharacter(sample, false, true)) return false;
            }
            return true;
        }

        void Apply()
        {
            int sceneCount = 0;
            int prefabCount = 0;

            if (_applyToScenes) sceneCount = ApplyToOpenScenes();
            if (_applyToPrefabs) prefabCount = ApplyToPrefabs();
            if (_applyToDefault) ApplyToDefaultSettings();

            AssetDatabase.SaveAssets();

            _status = "'" + _selected.name + "' 적용 완료 — 씬 글자 " + sceneCount + "개, 프리팹 글자 " + prefabCount + "개"
                + (_applyToDefault ? ", 기본 폰트도 변경" : "");
        }

        void SetFont(TMP_Text text)
        {
            text.font = _selected;

            // 폰트를 바꿔도 예전 머티리얼이 남아 있으면 글자가 안 보이거나 예전 서체로 그려진다.
            text.fontSharedMaterial = _selected.material;
        }

        int ApplyToOpenScenes()
        {
            int count = 0;

            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text text in texts)
            {
                // 프리팹 에셋 쪽은 아래 단계에서 따로 처리한다(중복 저장 방지).
                if (EditorUtility.IsPersistent(text)) continue;

                Undo.RecordObject(text, "폰트 바꾸기");
                SetFont(text);
                EditorUtility.SetDirty(text);
                count++;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) EditorSceneManager.MarkSceneDirty(scene);
            }

            return count;
        }

        int ApplyToPrefabs()
        {
            int count = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", PrefabSearchFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;

                TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                if (texts.Length == 0) continue;

                foreach (TMP_Text text in texts)
                {
                    SetFont(text);
                    count++;
                }

                PrefabUtility.SavePrefabAsset(root);
            }

            return count;
        }

        void ApplyToDefaultSettings()
        {
            TMP_Settings settings = TMP_Settings.instance;
            if (settings == null) return;

            var serialized = new SerializedObject(settings);
            SerializedProperty property = serialized.FindProperty("m_defaultFontAsset");
            if (property == null) return;

            property.objectReferenceValue = _selected;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
        }
    }
}
