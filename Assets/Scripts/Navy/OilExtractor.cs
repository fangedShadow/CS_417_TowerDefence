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
        private IProductionActivity activity;
        private TowerRuntime runtime;
        public float ProductionRemaining => productionRemaining;
        public bool HasResourceBinding => resources != null;
        public bool IsOperating => isActiveAndEnabled && resources != null && runtime != null && runtime.CanReceiveBuff &&
            (resourceProvider == null || resourceProvider.isActiveAndEnabled) && activity != null && activity.ProductionActive;
        public event System.Action<int, int, int> Produced;
        private void Awake()
        {
            runtime = GetComponent<TowerRuntime>();
            productionRemaining = runtime.Stats != null ? runtime.Stats.ResourceGenerationInterval : 10;
            if (resourceProvider is IPlayerResources provider) BindResources(provider);
        }
        public void BindResources(IPlayerResources sharedResources,IProductionActivity productionActivity=null)
        {resources=sharedResources;activity=productionActivity??resourceProvider as IProductionActivity;}
        private void Update() => AdvanceProduction(Time.deltaTime);
        public void AdvanceProduction(float deltaTime)
        {
            if (!IsOperating || deltaTime <= 0) return;
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
            if (money > 0 || exp > 0 || points > 0) Produced?.Invoke(money, exp, points);
        }
        private static int ProductionTotal(int cycles, int perCycle) =>
            (int)System.Math.Min(int.MaxValue, (long)cycles * Mathf.Max(0, perCycle));
    }
}
