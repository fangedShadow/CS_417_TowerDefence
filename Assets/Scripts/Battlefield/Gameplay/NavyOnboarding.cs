using System.Collections.Generic;
using System.Linq;
using TowerDefence.Shared;
using UnityEngine;

namespace TowerDefence.Battlefield
{
    public sealed class NavyOnboarding : MonoBehaviour
    {
        public PlayableNavyGame Game;
        public int StepIndex {get;private set;}
        public bool Completed {get;private set;}
        public bool Replay {get;private set;}
        readonly HashSet<int> accomplished=new HashSet<int>();
        public void Begin(bool replay=false)
        {
            Replay=replay;StepIndex=0;if(!replay){Completed=false;accomplished.Clear();}ShowCurrent();
        }
        public void ShowCurrent()
        {
            var steps=Game.Settings.Tutorial;if(steps.Length==0){Finish();return;}
            var step=steps[StepIndex];var popup=Game.Panel.Tutorials.ShowForGame(step.PopupId);
            if(popup==null)return;
            Game.Panel.Tutorials.MarkSeen(step.PopupId);
            bool task=step.Task!=TutorialTask.Explanation&&!Replay&&!accomplished.Contains(StepIndex);
            popup.Present("Navy tutorial "+(StepIndex+1)+" / "+steps.Length+" — "+step.Title,
                step.Instructions+(task?"\n\nComplete this construction task to continue. Close hides these instructions; use Resume tutorial to return.":""),
                task?step.Task==TutorialTask.Foundation?"Choose foundation":"Choose turret":StepIndex==steps.Length-1?"Finish tutorial":"Next",
                ()=>{if(task)ChooseConstruction(step.Task);else Next();},
                StepIndex>0?"Back":null,StepIndex>0?(System.Action)Back:null,"Hide instructions",()=>Game.Panel.Tutorials.Close());
            if(popup.Progress!=null)popup.Progress.text=$"{StepIndex+1} / {steps.Length}";Game.Refresh();
        }
        void ChooseConstruction(TutorialTask task)
        {
            var kind=task==TutorialTask.Foundation?TowerKind.Foundation:TowerKind.Turret;
            var entry=Game.Session.Catalog.FirstOrDefault(e=>e.Faction==Faction.Navy&&e.Kind==kind);
            Game.Panel.Tutorials.Close();if(entry!=null)Game.Panel.BeginConstruction(entry);
        }
        public bool Next()
        {
            var step=Game.Settings.Tutorial[StepIndex];
            if(!Replay&&step.Task!=TutorialTask.Explanation&&!accomplished.Contains(StepIndex))return false;
            if(StepIndex+1>=Game.Settings.Tutorial.Length){Finish();return true;}
            StepIndex++;ShowCurrent();return true;
        }
        public void Back(){if(StepIndex>0){StepIndex--;ShowCurrent();}}
        public void OnBuilt(PlacedStructure structure)
        {
            if(Completed||Replay||Game.State!=NavyGameState.Tutorial||structure==null)return;
            var task=Game.Settings.Tutorial[StepIndex].Task;
            if(task==TutorialTask.Foundation&&structure.Definition.IsFoundation||task==TutorialTask.Turret&&structure.Definition.Kind==TowerKind.Turret)
            {accomplished.Add(StepIndex);Next();}
        }
        void Finish()
        {
            if(!Replay)Completed=true;Game.Panel.Tutorials.ReleaseGameControl();
            if(Game.State==NavyGameState.Tutorial)Game.FinishTutorial();Game.Refresh();
        }
    }
}
