using System;
using UnityEngine;

namespace TowerDefence.Shared
{
    [Serializable]
    public sealed class PlayerResources : IPlayerResources
    {
        [SerializeField, Min(0)] private int money;
        [SerializeField, Min(0)] private int exp;
        [SerializeField, Min(0)] private int upgradePoints;
        public int Money => money;
        public int Exp => exp;
        public int UpgradePoints => upgradePoints;
        public event Action Changed;

        public PlayerResources(int money = 0, int exp = 0, int upgradePoints = 0)
        {
            this.money = Mathf.Max(0, money);
            this.exp = Mathf.Max(0, exp);
            this.upgradePoints = Mathf.Max(0, upgradePoints);
        }

        private static void Validate(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        }
        private void Add(ref int balance, int amount)
        {
            Validate(amount);
            balance = (int)Math.Min(int.MaxValue, (long)balance + amount);
            Changed?.Invoke();
        }
        public bool CanAfford(int amount) { Validate(amount); return money >= amount; }
        public void AddMoney(int amount) => Add(ref money, amount);
        public void AddExp(int amount) => Add(ref exp, amount);
        public void AddUpgradePoints(int amount) => Add(ref upgradePoints, amount);
        public bool SpendMoney(int amount)
        {
            if (!CanAfford(amount)) return false;
            money -= amount;
            Changed?.Invoke();
            return true;
        }
        public bool SpendUpgradePoints(int amount)
        {
            Validate(amount);
            if (upgradePoints < amount) return false;
            upgradePoints -= amount;
            Changed?.Invoke();
            return true;
        }
    }
}
