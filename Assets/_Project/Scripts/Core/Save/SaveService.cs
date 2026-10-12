using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ForagerCP
{
    [Serializable]
    public class SaveEntry
    {
        public string Key;
        public string Json;
    }

    /// 저장 파일 한 통. 각 시스템의 묶음을 이름표와 함께 담는다.
    [Serializable]
    public class SaveDocument
    {
        public int Version;
        public string SavedAtUtc;
        public string Summary;
        public List<SaveEntry> Entries = new List<SaveEntry>();

        public string Find(string key)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Key == key) return Entries[i].Json;
            }
            return null;
        }
    }

    /// 저장·불러오기의 창구. 슬롯 3개.
    ///
    /// 저장할 '내용'은 하나도 모른다. ISaveable을 구현한 것들을 모아 각자의 묶음을 받아 적을 뿐이라,
    /// 제작·방어구·보관함이 들어와도 이 파일은 그대로다.
    ///
    /// 언제 저장하는가: 일정 간격마다 한 번, 그리고 되돌리기 어려운 일(ISaveMilestone)이 있을 때 바로.
    /// 채집이나 인벤 숫자 같은 잔 변화는 신호로 삼지 않는다 — 그러면 한 번 캘 때마다 디스크를 때린다.
    /// 그런 것들은 다음 주기 저장에 묻어간다(강제 종료로 몇 분이 날아가는 것은 감수하는 범위).
    public class SaveService : MonoBehaviour
    {
        /// 저장 파일 형식의 판. 형식을 바꾸면 올린다.
        public const int CurrentVersion = 1;

        [SerializeField] int _slotCount = 3;

        [Header("자동 저장")]
        /// 주기 저장 간격.
        [SerializeField] float _autoSaveInterval = 180f;

        /// 큰 사건이 잇따를 때 연달아 쓰지 않도록 두는 최소 간격.
        [SerializeField] float _minSecondsBetweenSaves = 5f;

        [SerializeField] bool _autoSave = true;

        /// 자동 저장이 쓰는 슬롯.
        [SerializeField] int _autoSlot;

        public event Action<int> Saved;
        public event Action<int> Loaded;

        /// 불러오지 못했을 때. 인자는 사람이 읽을 사유.
        public event Action<string> Failed;

        public int SlotCount => Mathf.Max(1, _slotCount);

        /// 자동 저장이 쓰는 칸. 슬롯 목록이 "여기는 덮어써진다"고 알리는 데 쓴다.
        public int AutoSlot => _autoSlot;

        readonly List<ISaveable> _saveables = new List<ISaveable>();
        readonly List<ISaveMilestone> _milestones = new List<ISaveMilestone>();
        readonly Dictionary<string, string> _lastWritten = new Dictionary<string, string>();

        float _nextAutoSaveTime;
        float _nextAllowedSaveTime;

        /// 사건이 났는데 직전 저장과 너무 가까워 미뤄둔 상태.
        bool _pending;
        string _pendingReason;

        /// 불러오는 중에는 떠보기를 멈춘다. 안 그러면 복원 도중의 어중간한 상태가 저장될 수 있다.
        bool _restoring;

        void Awake() => Collect();

        void OnEnable()
        {
            Listen(true);
            _nextAutoSaveTime = Time.unscaledTime + Mathf.Max(5f, _autoSaveInterval);
        }

        void OnDisable() => Listen(false);

        void Listen(bool on)
        {
            for (int i = 0; i < _milestones.Count; i++)
            {
                if (_milestones[i] == null) continue;

                if (on) _milestones[i].Milestone += OnMilestone;
                else _milestones[i].Milestone -= OnMilestone;
            }
            _listening = on;
        }

        bool _listening;

        /// 되돌리기 어려운 일이 났다. 너무 잦으면 미뤘다가 간격이 지나면 쓴다.
        void OnMilestone(string reason) => RequestSave(reason);

        public void RequestSave(string reason)
        {
            if (!_autoSave || _restoring) return;

            if (Time.unscaledTime < _nextAllowedSaveTime)
            {
                _pending = true;
                _pendingReason = reason;
                return;
            }

            if (Save(_autoSlot)) LastReason = reason;
        }

        /// 마지막으로 저장하게 만든 사유. 로그·디버그용.
        public string LastReason { get; private set; }

        /// 씬에 있는 ISaveable을 전부 모은다. 비활성 오브젝트도 포함 —
        /// 창처럼 꺼둔 채 상태를 들고 있는 것이 있기 때문.
        public void Collect()
        {
            // 다시 모으기 전에 듣던 것을 놓는다. 안 놓으면 옛 구독이 남아 두 번 불린다.
            if (_listening) Listen(false);

            _saveables.Clear();
            _milestones.Clear();

            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is ISaveable saveable) _saveables.Add(saveable);
                if (behaviour is ISaveMilestone milestone) _milestones.Add(milestone);
            }

            // 저장 파일의 칸 순서가 실행마다 흔들리면 파일 비교가 어려워진다.
            _saveables.Sort((a, b) => string.CompareOrdinal(a.SaveKey, b.SaveKey));

            if (isActiveAndEnabled) Listen(true);
        }

        public string PathFor(int slot) => Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

        public bool Exists(int slot) => File.Exists(PathFor(slot));

        // ------------------------------------------------------------------ 쓰기

        public bool Save(int slot)
        {
            var document = new SaveDocument
            {
                Version = CurrentVersion,
                SavedAtUtc = DateTime.UtcNow.ToString("o"),
                Summary = BuildSummary(),
            };

            for (int i = 0; i < _saveables.Count; i++)
            {
                ISaveable saveable = _saveables[i];
                document.Entries.Add(new SaveEntry { Key = saveable.SaveKey, Json = saveable.Capture() });
            }

            try
            {
                // 쓰는 도중에 꺼지면 반쪽짜리 파일이 남는다. 임시 파일에 다 쓰고 나서 바꿔치운다.
                string path = PathFor(slot);
                string temporary = path + ".tmp";

                File.WriteAllText(temporary, JsonUtility.ToJson(document, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temporary, path);
            }
            catch (Exception error)
            {
                Failed?.Invoke($"저장하지 못했습니다: {error.Message}");
                return false;
            }

            RememberWritten(document);
            _nextAllowedSaveTime = Time.unscaledTime + _minSecondsBetweenSaves;

            Saved?.Invoke(slot);
            return true;
        }

        // ------------------------------------------------------------------ 읽기

        public bool Load(int slot)
        {
            SaveDocument document = Read(slot, out string error);
            if (document == null)
            {
                Failed?.Invoke(error);
                return false;
            }

            _restoring = true;
            try
            {
                for (int i = 0; i < _saveables.Count; i++)
                {
                    ISaveable saveable = _saveables[i];
                    string json = document.Find(saveable.SaveKey);

                    // 저장에 없는 칸은 건드리지 않는다. 그 시스템이 나중에 생긴 경우라
                    // 시작값 그대로 두는 것이 맞다.
                    if (string.IsNullOrEmpty(json)) continue;

                    saveable.Restore(json);
                }
            }
            finally
            {
                _restoring = false;
            }

            RememberWritten(document);
            _nextAllowedSaveTime = Time.unscaledTime + _minSecondsBetweenSaves;

            Loaded?.Invoke(slot);
            return true;
        }

        /// 슬롯 목록에 쓸 머리말만 읽는다. 전부 복원하지 않는다.
        public SaveDocument Peek(int slot) => Read(slot, out _);

        SaveDocument Read(int slot, out string error)
        {
            error = null;

            string path = PathFor(slot);
            if (!File.Exists(path)) { error = "저장된 것이 없습니다."; return null; }

            SaveDocument document;
            try
            {
                document = JsonUtility.FromJson<SaveDocument>(File.ReadAllText(path));
            }
            catch (Exception failure)
            {
                error = $"저장 파일을 읽지 못했습니다: {failure.Message}";
                return null;
            }

            if (document == null) { error = "저장 파일이 비어 있습니다."; return null; }

            // 개발 중에는 옛 판을 고쳐 읽지 않고 거부한다. 형식이 자주 바뀌는 동안
            // 어설프게 변환하면 틀린 상태로 조용히 굴러간다. 출시 전에 변환을 붙인다.
            if (document.Version != CurrentVersion)
            {
                error = $"저장 형식이 다릅니다(파일 {document.Version} / 지금 {CurrentVersion}). 새로 시작해주세요.";
                return null;
            }

            return document;
        }

        public void Delete(int slot)
        {
            string path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
        }

        // ------------------------------------------------------------------ 자동 저장

        void Update()
        {
            if (!_autoSave || _restoring) return;
            if (Time.unscaledTime < _nextAllowedSaveTime) return;

            // 미뤄둔 사건이 있으면 간격이 지나는 대로 먼저 쓴다.
            if (_pending)
            {
                _pending = false;
                if (HasChanges() && Save(_autoSlot)) LastReason = _pendingReason;
                return;
            }

            if (Time.unscaledTime < _nextAutoSaveTime) return;
            _nextAutoSaveTime = Time.unscaledTime + Mathf.Max(5f, _autoSaveInterval);

            // 바뀐 게 없으면 같은 내용을 다시 쓰지 않는다.
            if (!HasChanges()) return;

            if (Save(_autoSlot)) LastReason = "주기 저장";
        }

        bool HasChanges()
        {
            for (int i = 0; i < _saveables.Count; i++)
            {
                ISaveable saveable = _saveables[i];
                string now = saveable.Capture();

                if (!_lastWritten.TryGetValue(saveable.SaveKey, out string before)) return true;
                if (before != now) return true;
            }
            return false;
        }

        void RememberWritten(SaveDocument document)
        {
            _lastWritten.Clear();
            for (int i = 0; i < document.Entries.Count; i++)
            {
                _lastWritten[document.Entries[i].Key] = document.Entries[i].Json;
            }
        }

        string BuildSummary()
        {
            var parts = new List<ISaveSummary>();

            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour is ISaveSummary summary) parts.Add(summary);
            }

            parts.Sort((a, b) => a.SummaryOrder - b.SummaryOrder);
            return string.Join(" · ", parts.Select(part => part.Describe())
                .Where(text => !string.IsNullOrEmpty(text)));
        }
    }
}
