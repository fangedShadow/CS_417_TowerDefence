namespace TowerDefence.Shared
{
    public interface IPlayerResources
    {
        int Money { get; }
        int Exp { get; }
        int UpgradePoints { get; }
        bool CanAfford(int money);
        void AddMoney(int amount);
        bool SpendMoney(int amount);
        void AddExp(int amount);
        void AddUpgradePoints(int amount);
        bool SpendUpgradePoints(int amount);
    }
}
