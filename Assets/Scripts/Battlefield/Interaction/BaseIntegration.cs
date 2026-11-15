using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    public sealed class BaseIntegration : MonoBehaviour
    {
        public CenterBase SharedBase;
        public Transform LandEntry, NavalEntry;
        public Transform[] LeftGroundRoute, RightGroundRoute, NavalApproach;
        // Route transforms and the existing base health are hooks for teammates, not wave logic.
    }
}
