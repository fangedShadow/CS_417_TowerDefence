using TowerDefence.Navy;
using TowerDefence.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefence.Battlefield
{
    // Runtime adapter only: authored battlefield, grid and UI prefab/layout assets are never edited.
    public sealed class NavyInteractionEffects : MonoBehaviour
    {
        ConstructionController construction;
        GamePanelController panel;
        ConstructionPreview preview;
        NavyOutlineTarget selected,baseOutline,hovered;
        WorldInteraction interaction;
        Vector3 textScale;
        float textUntil,baseUntil;
        bool outlineCommander;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded-=Loaded;SceneManager.sceneLoaded+=Loaded;Attach();
        }
        static void Loaded(Scene scene,LoadSceneMode mode)=>Attach();
        static void Attach()
        {
            foreach(var controller in Object.FindObjectsByType<ConstructionController>(FindObjectsSortMode.None))
                if(controller.GetComponent<NavyInteractionEffects>()==null)controller.gameObject.AddComponent<NavyInteractionEffects>();
        }
        void Awake()
        {
            construction=GetComponent<ConstructionController>();panel=Object.FindAnyObjectByType<GamePanelController>();
            if(panel==null){enabled=false;return;}
            preview=panel.Preview;textScale=panel.StatusText.transform.localScale;
            interaction=panel.Interaction;interaction.HoverChanged+=Hover;
            construction.Completed+=Completed;construction.Feedback+=Feedback;construction.SelectionChanged+=Selection;
            preview.Changed+=PreviewChanged;preview.Cancelled+=Cancelled;construction.Session.FactionActivated+=FactionActivated;
        }
        void Selection(PlacedStructure target)
        {
            if(selected!=null)selected.Hide();selected=null;
            if(target==null||target.Definition.Faction!=Faction.Navy){if(hovered!=null)hovered.Show(new Color(.05f,.8f,1),.04f);return;}
            selected=target.Runtime.GetComponent<NavyOutlineTarget>();selected?.Show(new Color(1,.83f,.22f),panel.CommanderOpen?.18f:.06f);
        }
        void Hover(PlacedStructure target)
        {
            if(hovered!=null&&hovered!=selected)hovered.Hide();
            hovered=target!=null&&target.Alive?target.Runtime.GetComponent<NavyOutlineTarget>():null;
            if(hovered!=null&&hovered!=selected)hovered.Show(new Color(.05f,.8f,1),panel.CommanderOpen?.12f:.04f);
        }
        void PreviewChanged()
        {
            var ghost=preview.Ghost;if(ghost==null)return;
            var outline=ghost.GetComponent<NavyOutlineTarget>();if(outline==null)return;
            outline.Show(preview.Valid?new Color(.15f,1,.3f):new Color(1,.12f,.1f),panel.CommanderOpen?.18f:.05f);
        }
        void Completed(ConstructionAction action,PlacedStructure target,ConstructionDefinition definition,Vector3 position)
        {
            if(definition==null||definition.Faction!=Faction.Navy)return;
            var id=action==ConstructionAction.Repaired?NavyEffectId.Repaired:action==ConstructionAction.Sold?NavyEffectId.Sold:
                action==ConstructionAction.GlobalUpgrade||action==ConstructionAction.IndividualUpgrade?NavyEffectId.Upgraded:
                definition.IsFoundation?NavyEffectId.FoundationBuilt:definition.Kind==TowerKind.Turret?NavyEffectId.TurretBuilt:
                definition.Kind==TowerKind.Cannon?NavyEffectId.CannonBuilt:definition.Kind==TowerKind.Slow?NavyEffectId.DefenseBuilt:
                definition.Kind==TowerKind.Support?NavyEffectId.SupportBuilt:NavyEffectId.OilBuilt;
            if(action==ConstructionAction.GlobalUpgrade)position=panel.StatusText.transform.position;
            NavyEffectPool.Instance?.Play(id,position,scale:action==ConstructionAction.GlobalUpgrade?.12f:1);
            PulseText(new Color(.4f,1,.72f));
        }
        void Feedback(string message,bool success)
        {
            if(success)return;
            var point=preview.HoveredCell!=null?construction.Grid.World(preview.HoveredCell)+Vector3.up*.4f:panel.StatusText.transform.position;
            NavyEffectPool.Instance?.Play(NavyEffectId.InvalidAction,point,scale:preview.HoveredCell!=null?1:.1f);PulseText(new Color(1,.2f,.15f));
        }
        void Cancelled(GameObject ghost)
        {
            var point=ghost!=null&&ghost.activeSelf?ghost.transform.position:panel.StatusText.transform.position;
            var effect=NavyEffectPool.Instance?.Play(NavyEffectId.Cancelled,point,scale:ghost!=null&&ghost.activeSelf?1:.1f);effect?.CapturePreview(ghost);
            panel.StatusText.text="Construction cancelled";PulseText(new Color(.6f,.8f,1));
        }
        void PulseText(Color color){panel.StatusText.color=color;textUntil=Time.unscaledTime+.32f;}
        void FactionActivated(Faction faction)
        {
            NavyEffectPool.Instance?.Play(NavyEffectId.FactionUnlocked,panel.RankText.transform.position,scale:.12f);
            panel.StatusText.text=faction+" faction unlocked";PulseText(new Color(1,.7f,.2f));
        }
        // Future wave controller calls this only AFTER its real wave-completion event.
        // No spawner, wave clock, reward or unlock logic is introduced by this adapter.
        public void NotifyWaveCleared(int waveNumber=0)
        {
            var baseRoot=Object.FindAnyObjectByType<CenterBase>();
            if(baseRoot!=null)
            {
                var bounds=baseRoot.GetComponent<Collider>();
                NavyEffectPool.Instance?.Play(NavyEffectId.WaveCleared,baseRoot.transform.position+Vector3.up*(bounds!=null?bounds.bounds.size.y+.8f:4));
                baseOutline=baseRoot.GetComponent<NavyOutlineTarget>();if(baseOutline==null)baseOutline=baseRoot.gameObject.AddComponent<NavyOutlineTarget>();
                baseOutline.Show(new Color(1,.8f,.15f),.075f);baseUntil=Time.unscaledTime+1.6f;
            }
            panel.StatusText.text=waveNumber>0?"Wave "+waveNumber+" cleared":"Wave cleared";PulseText(new Color(1,.85f,.3f));
        }
        void Update()
        {
            if(panel==null)return;
            if(outlineCommander!=panel.CommanderOpen)
            {
                outlineCommander=panel.CommanderOpen;
                selected?.Show(new Color(1,.83f,.22f),outlineCommander?.18f:.06f);PreviewChanged();
            }
            if(textUntil>Time.unscaledTime)panel.StatusText.transform.localScale=textScale*(1+.07f*Mathf.Sin((textUntil-Time.unscaledTime)/.32f*Mathf.PI));
            else panel.StatusText.transform.localScale=textScale;
            if(baseOutline!=null&&baseUntil>0&&Time.unscaledTime>=baseUntil){baseOutline.Hide();baseUntil=0;}
        }
        void OnDestroy()
        {
            if(construction==null||panel==null)return;
            construction.Completed-=Completed;construction.Feedback-=Feedback;construction.SelectionChanged-=Selection;
            preview.Changed-=PreviewChanged;preview.Cancelled-=Cancelled;construction.Session.FactionActivated-=FactionActivated;
            if(interaction!=null)interaction.HoverChanged-=Hover;
            if(hovered!=null)hovered.Hide();if(selected!=null)selected.Hide();if(baseOutline!=null)baseOutline.Hide();
        }
    }
}
