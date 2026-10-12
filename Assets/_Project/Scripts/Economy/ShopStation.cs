using System;
using System.Collections.Generic;
using UnityEngine;

namespace ForagerCP
{
    /// 상점. 품목 목록을 들고 사고판 기록을 남긴다.
    ///
    /// 무엇을 파는지는 전부 에셋(ShopGood)이고, 여기는 '살 수 있나 / 돈을 받고 / 기록한다'만 한다.
    /// 품목이 늘어도 이 파일은 그대로다.
    public class ShopStation : MonoBehaviour, IInteractable, ISaveable, ISaveMilestone
    {
        [SerializeField] GoldWallet _gold;
        [SerializeField] Inventory _inventory;
        [SerializeField] PlayerEquipment _equipment;
        [SerializeField] HarvestPower _harvest;
        [SerializeField] SaveCatalog _catalog;

        [SerializeField] List<ShopGood> _goods = new List<ShopGood>();

        public event Action<ShopGood> Purchased;

        /// 못 산 이유. 화면이 그대로 띄운다.
        public event Action<string> Refused;

        /// 기획서가 콕 집은 자리 — 거래 직후에는 저장한다.
        public event Action<string> Milestone;

        /// F를 눌렀다. 화면이 이걸 듣고 자기를 연다.
        public event Action<ShopStation> OpenRequested;

        public IReadOnlyList<ShopGood> Goods => _goods;

        public Transform Transform => transform;
        public string Label => "상점 열기";

        readonly HashSet<ShopGood> _bought = new HashSet<ShopGood>();
        ShopContext _context;

        /// RegionAccess에서 겪은 것과 같은 이유로 늦게 만든다 —
        /// Awake에서만 채우면 에디터 도구가 물었을 때 null이라 그대로 터진다.
        ShopContext Context
        {
            get
            {
                if (_context != null) return _context;

                if (_gold == null) _gold = FindFirstObjectByType<GoldWallet>();
                if (_inventory == null) _inventory = FindFirstObjectByType<Inventory>();
                if (_equipment == null) _equipment = FindFirstObjectByType<PlayerEquipment>();
                if (_harvest == null) _harvest = FindFirstObjectByType<HarvestPower>();

                _context = new ShopContext
                {
                    Inventory = _inventory,
                    Equipment = _equipment,
                    Gold = _gold,
                    Harvest = _harvest,
                };
                return _context;
            }
        }

        void Awake() => _ = Context;

        public bool Bought(ShopGood good) => good != null && _bought.Contains(good);

        /// 목록에 올릴지. 선행 품목을 아직 안 샀으면 아예 보이지 않는다 —
        /// 못 사는 이유로 띄우면 목록이 '언젠가 살 것'으로 길어진다.
        public bool IsListed(ShopGood good)
        {
            if (good == null) return false;
            return good.Requires == null || Bought(good.Requires);
        }

        public bool CanBuy(ShopGood good, out string reason)
        {
            reason = null;
            if (good == null) { reason = "품목이 없습니다"; return false; }

            if (good.OneTime && Bought(good)) { reason = "이미 샀습니다"; return false; }
            if (!IsListed(good)) { reason = "아직 열리지 않았습니다"; return false; }
            if (!good.CanBuy(Context, out reason)) return false;

            if (_gold.Gold < good.Price) { reason = $"{good.Price - _gold.Gold}골드 모자랍니다"; return false; }
            return true;
        }

        public bool TryBuy(ShopGood good)
        {
            if (!CanBuy(good, out string reason))
            {
                Refused?.Invoke(reason);
                return false;
            }

            // TrySpend가 잔액 확인과 차감을 함께 한다 — 실패하면 효과도 적용되지 않는다.
            if (!_gold.TrySpend(good.Price)) return false;

            good.Apply(Context);
            _bought.Add(good);

            Purchased?.Invoke(good);
            Milestone?.Invoke($"구매: {good.DisplayName}");
            return true;
        }

        public void Interact() => OpenRequested?.Invoke(this);

        // ---------------- 저장 ----------------

        [Serializable]
        class Save
        {
            public List<string> Bought = new List<string>();
        }

        public string SaveKey => "shop";

        public string Capture()
        {
            var save = new Save();
            foreach (ShopGood good in _bought) save.Bought.Add(SaveCatalog.NameOf(good));

            // 집합은 도는 순서를 보장하지 않는다. 정렬해야 같은 상태가 늘 같은 글자로 나온다.
            save.Bought.Sort(StringComparer.Ordinal);
            return JsonUtility.ToJson(save);
        }

        public void Restore(string payload)
        {
            var save = JsonUtility.FromJson<Save>(payload);
            if (save == null) return;

            _bought.Clear();
            for (int i = 0; i < save.Bought.Count; i++)
            {
                // 품목이 지워졌으면 그 기록만 버린다. 효과는 다시 적용하지 않는다 —
                // 장비도 가방 칸도 그 시스템이 따로 저장하므로 여기서 또 주면 두 번 들어간다.
                ShopGood good = _catalog != null ? _catalog.Find<ShopGood>(save.Bought[i]) : null;
                if (good != null) _bought.Add(good);
            }
        }
    }
}
