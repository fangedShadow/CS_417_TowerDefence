namespace TowerDefence.Shared
{
    // Shared activity contract keeps Navy production independent of the battlefield assembly.
    public interface IProductionActivity
    {
        bool ProductionActive { get; }
    }
}
