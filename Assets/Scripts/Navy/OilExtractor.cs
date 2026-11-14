using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Navy
{
    [RequireComponent(typeof(TowerRuntime))]
    public sealed class OilExtractor : MonoBehaviour
    {
        [Tooltip("Optional component implementing IPlayerResources; otherwise call BindResources.")]
        [SerializeField] private MonoBehaviour resourceProvider;
        [SerializeField] private float productionRemaining;
        private IPlayerResources resources;
        private TowerRuntime runtime;
        public float ProductionRemaining => productionRemaining;
        public bool HasResourceBinding => resources != null;
        private void Awake()
        {
            runtime = GetComponent<TowerRuntime>();
            productionRemaining = runtime.Stats != null ? runtime.Stats.ResourceGenerationInterval : 10;
            if (resourceProvider is IPlayerResources provider) BindResources(provider);
        }
        public void BindResources(IPlayerResources sharedResources) => resources = sharedResources;
        private void Update() => AdvanceProduction(Time.deltaTime);
        public void AdvanceProduction(float deltaTime)
        {
            if (!isActiveAndEnabled || resources == null || !runtime.CanReceiveBuff || deltaTime <= 0) return;
            if (resourceProvider != null && !resourceProvider.isActiveAndEnabled) return;
            float interval = Mathf.Max(0.05f, runtime.Stats.ResourceGenerationInterval);
            productionRemaining -= deltaTime;
            if (productionRemaining > 0) return;
            int cycles = 1 + Mathf.FloorToInt(-productionRemaining / interval);
            int money = ProductionTotal(cycles, runtime.Stats.ResourceGenerationAmount);
            int exp = ProductionTotal(cycles, runtime.Stats.ResourceExpGenerationAmount);
            int points = ProductionTotal(cycles, runtime.Stats.ResourceUpgradePointGenerationAmount);
            if (money > 0) resources.AddMoney(money);
            if (exp > 0) resources.AddExp(exp);
            if (points > 0) resources.AddUpgradePoints(points);
            productionRemaining += cycles * interval;
        }
        private static int ProductionTotal(int cycles, int perCycle) =>
            (int)System.Math.Min(int.MaxValue, (long)cycles * Mathf.Max(0, perCycle));
    }
}
