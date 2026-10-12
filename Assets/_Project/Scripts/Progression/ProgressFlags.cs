using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// "무엇을 했는가"를 문자열 하나로 기록하는 자리. 보스 처치, 튜토리얼 완료 같은 것.
    ///
    /// 전용 클래스를 종류마다 만들지 않는 이유: 기획이 어떤 사건을 조건으로 삼을지 모른다.
    /// 문자열이면 디자이너가 에셋에 적는 것만으로 새 조건이 생긴다.
    /// 세이브가 붙으면 이 집합 하나만 저장하면 된다.
    public class ProgressFlags : MonoBehaviour, ISaveable, ISaveMilestone
    {
        /// 시작부터 켜둘 것. 테스트나 디버그 진입용.
        [SerializeField] List<string> _initialFlags = new List<string>();

        public event Action<string> Raised;

        /// 기록이 서는 일 = 보스 처치, 열쇠 획득 같은 사건. 전부 되돌리기 어렵다.
        public event Action<string> Milestone;

        readonly HashSet<string> _flags = new HashSet<string>();

        void Awake()
        {
            for (int i = 0; i < _initialFlags.Count; i++) _flags.Add(_initialFlags[i]);
        }

        public bool Has(string flag) => !string.IsNullOrEmpty(flag) && _flags.Contains(flag);

        public void Raise(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            if (!_flags.Add(flag)) return; // 이미 있으면 알리지 않는다 — 구독자가 두 번 반응하면 곤란하다

            Raised?.Invoke(flag);
            Milestone?.Invoke($"기록: {flag}");
        }

        public void Clear(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            _flags.Remove(flag);
        }

        // ---------------- 저장 ----------------

        [Serializable]
        class Save
        {
            public List<string> Flags = new List<string>();
        }

        public string SaveKey => "flags";

        public string Capture()
        {
            var save = new Save();
            save.Flags.AddRange(_flags);

            // HashSet은 도는 순서를 보장하지 않는다. 정렬하지 않으면 내용이 같아도 글자가 달라져
            // 서비스가 '바뀌었다'고 오해하고 계속 저장한다.
            save.Flags.Sort(StringComparer.Ordinal);
            return JsonUtility.ToJson(save);
        }

        public void Restore(string payload)
        {
            var save = JsonUtility.FromJson<Save>(payload);
            if (save == null) return;

            _flags.Clear();
            for (int i = 0; i < save.Flags.Count; i++) _flags.Add(save.Flags[i]);
        }
    }
}
