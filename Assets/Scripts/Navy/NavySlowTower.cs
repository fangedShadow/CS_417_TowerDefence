using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Navy
{
    // Specialization marker/API; the shared NavyWeapon owns targeting, firing and cooldowns.
    // Status effects are carried by each bolt and applied only by its impact handler.
    [RequireComponent(typeof(NavyWeapon))]
    public sealed class NavySlowTower : MonoBehaviour
    {
        public NavyWeapon Weapon => GetComponent<NavyWeapon>();
        public bool TryFire(IEnemyTarget enemy) => isActiveAndEnabled && Weapon.TryFire(enemy);
        private void OnEnable() => Weapon.enabled = true;
        private void OnDisable()
        {
            if (Weapon != null) Weapon.enabled = false;
        }
    }
}
