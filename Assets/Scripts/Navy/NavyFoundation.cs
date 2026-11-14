using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Navy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TowerRuntime))]
    public sealed class NavyFoundation : MonoBehaviour, ITowerRangeModifier
    {
        [Tooltip("Root sits on the ocean floor; this transform is the top mounting surface.")]
        [SerializeField] private Transform towerMount;
        [SerializeField, Min(0.1f)] private float height = 0.8f;
        [SerializeField, Min(1)] private float rangeMultiplier = 1;
        public Transform TowerMount => towerMount;
        public float Height => height;
        public float RangeMultiplier => Mathf.Max(1, rangeMultiplier);

        public float GetRangeMultiplier(Faction faction) =>
            faction == Faction.Navy && isActiveAndEnabled && GetComponent<TowerRuntime>().CanReceiveBuff ?
            RangeMultiplier : 1;

        public bool TryMount(TowerRuntime tower)
        {
            if (!isActiveAndEnabled || towerMount == null || tower == null ||
                !GetComponent<TowerRuntime>().CanReceiveBuff || tower.Faction != Faction.Navy ||
                tower.Kind == TowerKind.Foundation || towerMount.IsChildOf(tower.transform)) return false;
            foreach (var occupant in towerMount.GetComponentsInChildren<TowerRuntime>(true))
                if (occupant != tower) return false;
            tower.transform.SetParent(towerMount, false);
            tower.transform.localPosition = Vector3.zero;
            tower.transform.localRotation = Quaternion.identity;
            return true;
        }

        public bool Unmount(TowerRuntime tower, Transform newParent = null)
        {
            if (tower == null || towerMount == null || !tower.transform.IsChildOf(towerMount)) return false;
            if (newParent != null && newParent.IsChildOf(tower.transform)) return false;
            tower.transform.SetParent(newParent, true);
            return true;
        }
    }
}
