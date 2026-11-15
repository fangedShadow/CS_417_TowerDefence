using UnityEngine;

namespace TowerDefence.Battlefield
{
    // Resolves scene-only HUD references once; no second health or resource state.
    [DefaultExecutionOrder(-50)]
    public sealed class Phase41Bindings : MonoBehaviour
    {
        public CommandHud Hud;
        public PlayableNavyGame Game;
        void Start()
        {
            foreach(var bar in Hud.GetComponentsInChildren<HealthBarView>(true))bar.Bind(Game.Base.SharedBase.Health,Hud.Viewer);
        }
    }
}
