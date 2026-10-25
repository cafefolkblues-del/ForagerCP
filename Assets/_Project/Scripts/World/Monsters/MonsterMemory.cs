using System;
using UnityEngine;

namespace ForagerCP
{
    /// 몬스터 한 마리의 전투 진행 값. 판단은 하지 않고 값만 든다.
    ///
    /// MonsterAI에서 떼어 둔 이유: 상태를 SO 에셋으로 옮기면(docs/MONSTER_AI_DESIGN.html 3단계)
    /// 상태 에셋은 같은 종류끼리 공유하는 자산이라 값을 들 수 없다. 진행 값이 여기 한 곳에 모여 있어야
    /// 상태는 여기에 적고, 전투 초기화는 Clear 한 번으로 비운다.
    ///
    /// 시간은 전부 "끝나는 시각"으로 적는다. 남은 시간을 매 프레임 깎으면 프레임이 끊길 때 틀어진다.
    public class MonsterMemory
    {
        /// 맞은 뒤 경직이 끝나는 시각.
        public float HitstunUntil;

        /// 내지른 뒤 빈틈이 끝나는 시각. 방패에 막히면 늘어난다.
        public float RecoverUntil;

        /// 지금 예고 중인 공격의 목록 번호. 없으면 -1.
        public int WindingIndex = -1;
        public float WindupEndsAt;
        public MonsterAttack Winding;

        /// 마지막으로 내지른 공격. 막혔을 때 얼마나 멈출지를 여기서 읽는다.
        public MonsterAttack LastFired;

        /// 공격마다 따로 센다. 하나로 묶으면 근접을 쓴 뒤 원거리까지 같이 잠긴다.
        public float[] ReadyAt = Array.Empty<float>();

        /// 지금 몸을 쓰고 있는 공격(MonsterAttack.HasMotion). 없으면 null.
        public MonsterAttack Motion;
        public float MotionStartedAt;

        public bool IsWindingUp => WindingIndex >= 0;
        public bool InMotion => Motion != null;

        /// 공격 수가 정해진 뒤(정의를 읽은 뒤) 한 번 부른다.
        public void PrepareCooldowns(int attackCount) => ReadyAt = new float[Mathf.Max(0, attackCount)];

        public void CancelWindup()
        {
            WindingIndex = -1;
            Winding = null;
        }

        public void StopMotion() => Motion = null;

        /// 전투 초기화용. 쿨다운 칸 수는 그대로 두고 값만 비운다.
        public void Clear()
        {
            HitstunUntil = 0f;
            RecoverUntil = 0f;
            CancelWindup();
            WindupEndsAt = 0f;
            LastFired = null;
            Array.Clear(ReadyAt, 0, ReadyAt.Length);
            StopMotion();
        }
    }
}
