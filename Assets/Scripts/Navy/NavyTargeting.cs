using System.Collections.Generic;
using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Navy
{
    public static class NavyTargeting
    {
        public static List<IEnemyTarget> EnemiesInRange(Vector3 center, float radius,
            TargetDomain domains, LayerMask layers)
        {
            var result = new List<IEnemyTarget>();
            var seen = new HashSet<Transform>();
            foreach (var collider in Physics.OverlapSphere(center, Mathf.Max(0, radius), layers,
                QueryTriggerInteraction.Collide))
            {
                var target = collider.GetComponentInParent<IEnemyTarget>();
                if (Valid(target, domains) && seen.Add(target.AimTransform)) result.Add(target);
            }
            return result;
        }
        public static bool Valid(IEnemyTarget target, TargetDomain domains) =>
            target is Component component && component != null && target.IsAlive &&
            (target.Domain & domains) != 0 && target.AimTransform != null;

        public static IEnemyTarget Nearest(Vector3 origin, float range, TargetDomain domains,
            LayerMask layers)
        {
            IEnemyTarget nearest = null;
            float closest = range * range;
            foreach (var enemy in EnemiesInRange(origin, range, domains, layers))
            {
                float distance = (enemy.AimTransform.position - origin).sqrMagnitude;
                if (distance <= closest) { closest = distance; nearest = enemy; }
            }
            return nearest;
        }
    }
}
