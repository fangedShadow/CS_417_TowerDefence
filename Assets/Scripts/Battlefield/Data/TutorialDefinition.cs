using UnityEngine;

namespace TowerDefence.Battlefield
{
    [CreateAssetMenu(menuName = "Tower Defence/Tutorial")]
    public sealed class TutorialDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea(4, 12)] public string[] Pages;
    }
}
