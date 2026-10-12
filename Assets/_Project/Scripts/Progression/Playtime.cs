using System;
using UnityEngine;

namespace ForagerCP
{
    /// 이 세이브로 얼마나 놀았는지. 슬롯 목록에서 어느 것이 내 진행인지 가르는 가장 빠른 단서다.
    ///
    /// 실제 시간(unscaled)으로 센다. 일시정지나 슬로모션에 따라 늘었다 줄었다 하면
    /// "몇 시간 했지"라는 물음에 답이 안 된다.
    public class Playtime : MonoBehaviour, ISaveable, ISaveSummary
    {
        public TimeSpan Elapsed => TimeSpan.FromSeconds(_seconds);

        float _seconds;

        void Update() => _seconds += Time.unscaledDeltaTime;

        // ---------------- 저장 ----------------

        [Serializable]
        class Save
        {
            public float Seconds;
        }

        public string SaveKey => "playtime";

        /// 초 단위 그대로 적으면 매 프레임 글자가 달라져, 바뀐 게 있는지 비교하는 쪽이
        /// 늘 "바뀌었다"고 답한다. 1초 단위로 깎아서 적는다.
        public string Capture() => JsonUtility.ToJson(new Save { Seconds = Mathf.Floor(_seconds) });

        public void Restore(string payload)
        {
            var save = JsonUtility.FromJson<Save>(payload);
            if (save == null) return;

            _seconds = Mathf.Max(0f, save.Seconds);
        }

        public int SummaryOrder => 10;

        public string Describe()
        {
            TimeSpan span = Elapsed;
            return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }
    }
}
