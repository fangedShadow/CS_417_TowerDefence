using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Navy
{
    // No path ownership, automatic movement, attacks, rewards, or wave scheduling.
    [RequireComponent(typeof(EnemyRuntime))]
    public sealed class NavalEnemy : MonoBehaviour
    {
        private EnemyRuntime runtime;
        public EnemyRuntime Runtime => runtime != null ? runtime : runtime = GetComponent<EnemyRuntime>();
        public void MoveTowards(Vector3 destination, float deltaTime)
        {
            if (!isActiveAndEnabled || !Runtime.IsAlive || deltaTime <= 0) return;
            destination.y = transform.position.y;
            Vector3 heading = destination - transform.position;
            if (heading.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(heading);
            transform.position = Vector3.MoveTowards(transform.position, destination,
                Runtime.MovementSpeed * deltaTime);
        }
    }
}
