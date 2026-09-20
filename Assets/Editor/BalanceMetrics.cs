using System;
using System.Collections.Generic;
using ForagerCP;
using UnityEngine;

namespace ForagerCP.EditorTools
{
    /// 밸런스 표에 붙는 '계산 열'. 입력값이 아니라 판단용 숫자다.
    /// 밸런스는 체력 45 같은 원시 수치가 아니라 "몇 대 때려야 하나 / 초당 얼마 버나"로 보는 거라
    /// 이 열이 표의 값어치 절반을 담당한다.
    ///
    /// 표의 나머지(열 생성·편집·CSV)는 전부 리플렉션이라 필드가 늘어도 자동이지만,
    /// 계산식만은 코드로 적어야 한다. 그래서 이 파일 하나로 몰아뒀다 — 지표 추가는 여기 한 줄.
    public static class BalanceMetrics
    {
        /// 플레이어 기본 채집력/공격력. 실제 값은 HarvestPower·PlayerAttacker의 인스펙터에 있고,
        /// 여기 값은 어디까지나 표를 읽기 위한 기준점이다.
        public const int BasePower = 5;
        public const int UpgradedPower = 10;
        public const float SwingCooldown = 0.4f;
        public const int PlayerMaxHp = 20;

        public class Metric
        {
            public string Name;
            public string Tooltip;
            public Func<UnityEngine.Object, string> Evaluate;
        }

        static readonly Dictionary<Type, List<Metric>> _byType = new Dictionary<Type, List<Metric>>
        {
            {
                typeof(MineralDefinition), new List<Metric>
                {
                    new Metric
                    {
                        Name = "타수(5)",
                        Tooltip = "기본 채집력 5로 몇 대 때려야 깨지나",
                        Evaluate = o => Hits(((MineralDefinition)o).MaxHp, BasePower)
                    },
                    new Metric
                    {
                        Name = "타수(10)",
                        Tooltip = "강화 채집력 10 기준",
                        Evaluate = o => Hits(((MineralDefinition)o).MaxHp, UpgradedPower)
                    },
                    new Metric
                    {
                        Name = "1회 골드",
                        Tooltip = "한 번 캘 때 얻는 골드 (개수 × 판매가)",
                        Evaluate = o =>
                        {
                            var m = (MineralDefinition)o;
                            return (m.HarvestYield * m.GoldValue).ToString();
                        }
                    },
                    new Metric
                    {
                        Name = "초당 골드",
                        Tooltip = "채집 시간 + 리스폰 대기까지 포함한 효율. 같은 광물만 계속 캔다고 가정",
                        Evaluate = o =>
                        {
                            var m = (MineralDefinition)o;
                            float harvestTime = Mathf.CeilToInt((float)m.MaxHp / BasePower) * SwingCooldown;
                            float cycle = harvestTime + m.RespawnDelay;
                            if (cycle <= 0f) return "-";
                            return (m.HarvestYield * m.GoldValue / cycle).ToString("F2");
                        }
                    },
                    new Metric
                    {
                        Name = "초당 경험치",
                        Tooltip = "위와 같은 기준의 경험치 효율",
                        Evaluate = o =>
                        {
                            var m = (MineralDefinition)o;
                            float harvestTime = Mathf.CeilToInt((float)m.MaxHp / BasePower) * SwingCooldown;
                            float cycle = harvestTime + m.RespawnDelay;
                            if (cycle <= 0f) return "-";
                            return (m.ExpReward / cycle).ToString("F2");
                        }
                    }
                }
            },
            {
                typeof(MonsterDefinition), new List<Metric>
                {
                    new Metric
                    {
                        Name = "처치 타수",
                        Tooltip = "플레이어 공격력 5 기준 몇 대 때려야 죽나",
                        Evaluate = o => Hits(((MonsterDefinition)o).MaxHp, BasePower)
                    },
                    new Metric
                    {
                        Name = "몹 DPS",
                        Tooltip = "공격력 ÷ 공격 간격",
                        Evaluate = o =>
                        {
                            var m = (MonsterDefinition)o;
                            return (m.AttackPower / m.AttackInterval).ToString("F2");
                        }
                    },
                    new Metric
                    {
                        Name = "플레이어 사망까지",
                        Tooltip = "가만히 맞고만 있을 때 플레이어(체력 20)가 죽기까지 걸리는 시간",
                        Evaluate = o =>
                        {
                            var m = (MonsterDefinition)o;
                            if (m.AttackPower <= 0) return "-";
                            int hits = Mathf.CeilToInt((float)PlayerMaxHp / m.AttackPower);
                            return (hits * m.AttackInterval).ToString("F1") + "s";
                        }
                    },
                    new Metric
                    {
                        Name = "경험치/분",
                        Tooltip = "재등장 대기까지 포함해 한 마리를 반복 사냥할 때의 분당 경험치",
                        Evaluate = o =>
                        {
                            var m = (MonsterDefinition)o;
                            float killTime = Mathf.CeilToInt((float)m.MaxHp / BasePower) * SwingCooldown;
                            float cycle = killTime + m.RespawnDelay;
                            if (cycle <= 0f) return "-";
                            return (m.ExpReward * 60f / cycle).ToString("F1");
                        }
                    }
                }
            }
        };

        public static IReadOnlyList<Metric> For(Type type)
        {
            return _byType.TryGetValue(type, out List<Metric> metrics) ? metrics : Array.Empty<Metric>();
        }

        static string Hits(int hp, int power)
        {
            if (power <= 0) return "-";
            return Mathf.CeilToInt((float)hp / power).ToString();
        }
    }
}
