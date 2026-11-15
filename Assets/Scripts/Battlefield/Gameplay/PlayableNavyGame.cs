using System;
using System.Collections;
using System.Linq;
using TowerDefence.Navy;
using TowerDefence.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace TowerDefence.Battlefield
{
    [DefaultExecutionOrder(-100)]
    public sealed class PlayableNavyGame : MonoBehaviour
    {
        public NavyGameSettings Settings;
        public BattlefieldSession Session;
        public ConstructionController Construction;
        public GamePanelController Panel;
        public BattlefieldRegions Regions;
        public NavyWaveManager Waves;
        public NavyOnboarding Tutorial;
        public NavyGameView View;
        public BaseIntegration Base;
        public NavyGameState State {get;private set;}=NavyGameState.Initializing;
        public Faction? StartingFaction {get;private set;}
        public bool CanManage=>State==NavyGameState.Tutorial||State==NavyGameState.Preparation||State==NavyGameState.WaveActive||State==NavyGameState.WaveCompleted;
        public event Action Changed;
        int awardedRank=1;
        bool critical,started,restarting;
        bool secondUnlock,finalUnlock;
        Faction finalRegionUnlocked;
        Coroutine completion;
        NavyOutlineTarget baseOutline;
        float baseFlashUntil;
        void Awake()
        {
            Session.Game=this;Session.BeginFactionSelection();Regions.ResetRegions();
            Construction.Completed+=ActionCompleted;Session.Resources.Changed+=ResourcesChanged;Waves.WaveCompleted+=WaveCompleted;
            Base.SharedBase.Health.Damaged+=BaseDamaged;Base.SharedBase.Health.Died+=GameOver;
        }
        void Start()
        {
            if(!enabled)return;Panel.Initialize();View.Bind(this);started=true;SetState(NavyGameState.FactionSelection);ShowFactionSelection();
            if(Debug.isDebugBuild)Debug.Log("[Navy startup] Main world ready; state="+State+"; camera="+Panel.Interaction.PhysicalCamera.name+"; panel="+Panel.transform.position);
        }
        public bool ChooseStartingFaction(Faction faction)
        {
            if(State!=NavyGameState.FactionSelection||!Regions.GameplayImplemented(faction))return false;
            StartingFaction=faction;Regions.Unlock(faction);Session.RegisterFaction(faction);SetState(NavyGameState.Tutorial);Tutorial.Begin();return true;
        }
        public void ShowFactionSelection()
        {
            var popup=Panel.Tutorials.ShowForGame("ChooseFaction");if(popup==null)return;
            popup.Present("Choose your starting faction",
                "NAVY — PLAYABLE\nOcean + Main Base. Build foundations and naval defenses; defend against ship waves.\n\nARMY — UNAVAILABLE\nLeft Land + Center Land + Main Base. Towers and ground enemies are in development.\n\nSKY / AIR FORCE — UNAVAILABLE\nRight Land + Center Land + Main Base. Air gameplay is in development.",
                "Navy — Play",()=>ChooseStartingFaction(Faction.Navy),"Army",null,"Sky / Air Force",null);
        }
        public void FinishTutorial(){if(State==NavyGameState.Tutorial&&Tutorial.Completed)SetState(NavyGameState.Preparation);}
        public void EnterWave(){SetState(NavyGameState.WaveActive);Panel.Tutorials.ReleaseGameControl();}
        public bool StartNextWave()
        {
            bool result=Waves.TryStart();if(!result&&State!=NavyGameState.WaveActive)Panel.StatusText.text="Finish onboarding and enter preparation before starting a wave.";
            Refresh();return result;
        }
        void ActionCompleted(ConstructionAction action,PlacedStructure target,ConstructionDefinition definition,Vector3 position)
        {if(action==ConstructionAction.Built)Tutorial.OnBuilt(target);}
        void ResourcesChanged()
        {
            if(State==NavyGameState.Initializing||State==NavyGameState.FactionSelection||State==NavyGameState.GameOver)return;
            int rank=Session.RankNumber;if(rank<=awardedRank)return;int earned=rank-awardedRank;awardedRank=rank;
            if(Settings.UpgradePointsPerRank>0)Session.Resources.AddUpgradePoints(earned*Settings.UpgradePointsPerRank);
            NavyEffectPool.Instance?.Play(NavyEffectId.Upgraded,Panel.RankText.transform.position,scale:.1f);
            Panel.StatusText.text="Promoted to "+Session.RankName+" — "+(Session.NextRankExp<0?"maximum Navy rank":Session.NextRankExp+" EXP for next rank");Refresh();
        }
        void WaveCompleted(int number)
        {
            if(State!=NavyGameState.WaveActive)return;SetState(NavyGameState.WaveCompleted);
            var reward=Settings.Wave(number);Session.Resources.AddMoney(reward.Money);Session.Resources.AddExp(reward.Exp);Session.Resources.AddUpgradePoints(reward.UpgradePoints);
            Construction.GetComponent<NavyInteractionEffects>()?.NotifyWaveCleared(number);
            completion=StartCoroutine(AfterWave(number));
        }
        IEnumerator AfterWave(int number)
        {
            yield return new WaitForSeconds(Settings.WaveCompletionPause);completion=null;if(State==NavyGameState.GameOver)yield break;
            if(number>=10&&!secondUnlock){SetState(NavyGameState.FactionUnlock);ShowUnlockChoice();}
            else if(number>=20&&!finalUnlock)
            {
                finalUnlock=true;var remaining=Enum.GetValues(typeof(Faction)).Cast<Faction>().FirstOrDefault(f=>!Session.FactionActive(f));
                finalRegionUnlocked=remaining;
                Regions.Unlock(remaining);Session.RegisterFaction(remaining);SetState(NavyGameState.FactionUnlock);ShowFinalUnlock(remaining);
            }
            else SetState(NavyGameState.Preparation);
        }
        public bool ChooseRegionUnlock(Faction faction)
        {
            if(State!=NavyGameState.FactionUnlock||secondUnlock||faction==Faction.Navy||Session.FactionActive(faction))return false;
            secondUnlock=true;Regions.Unlock(faction);Session.RegisterFaction(faction);Panel.Tutorials.ReleaseGameControl();SetState(NavyGameState.Preparation);return true;
        }
        public void ShowUnlockChoice()
        {
            var popup=Panel.Tutorials.ShowForGame("ChooseFaction");if(popup==null)return;
            popup.Present("Wave 10 — unlock another region",
                "Choose one remaining faction's regions.\n\nARMY: Left Land + Center Land.\nSKY: Right Land + Center Land.\n\nThese are REGION-ONLY unlocks. Their towers, enemies and combat are not implemented. Navy gameplay and all current regions remain available.",
                "Navy — owned",null,"Army regions",()=>ChooseRegionUnlock(Faction.Army),"Sky regions",()=>ChooseRegionUnlock(Faction.Sky));
        }
        void ShowFinalUnlock(Faction faction)
        {
            var popup=Panel.Tutorials.ShowForGame("ChooseFaction");if(popup==null)return;
            popup.Present("Wave 20 — final faction regions unlocked",faction+" regions are now open. All previous regions stay unlocked.\n\nArmy and Sky gameplay remain unavailable; continue defending with Navy structures.","Continue",()=>{Panel.Tutorials.ReleaseGameControl();SetState(NavyGameState.Preparation);},null,null,null,null);
        }
        public void Resume()
        {
            if(State==NavyGameState.Tutorial)Tutorial.ShowCurrent();else if(State==NavyGameState.FactionSelection)ShowFactionSelection();
            else if(State==NavyGameState.FactionUnlock){if(finalUnlock)ShowFinalUnlock(finalRegionUnlocked);else ShowUnlockChoice();}
            else if(State==NavyGameState.GameOver)ShowGameOver();else Tutorial.Begin(true);
        }
        void BaseDamaged(DamageInfo damage)
        {
            if(State==NavyGameState.GameOver)return;
            var target=Base.SharedBase;if(target==null)return;
            NavyEffectPool.Instance?.Play(NavyEffectId.CannonImpact1,Base.NavalEntry.position+Vector3.up*.8f,scale:.45f);
            baseOutline=target.GetComponent<NavyOutlineTarget>();if(baseOutline==null)baseOutline=target.gameObject.AddComponent<NavyOutlineTarget>();
            baseOutline.Show(new Color(1,.25f,.08f),.075f);baseFlashUntil=Time.unscaledTime+.22f;
            if(!critical&&target.Health.CurrentHealth/target.Health.MaxHealth<=Settings.CriticalHealthFraction&&target.Health.IsAlive)
            {critical=true;Panel.StatusText.text="WARNING — Main Base health critical!";Panel.ShowPhysicalPanel();View.Warn();}
            Refresh();
        }
        void Update(){if(baseOutline!=null&&baseFlashUntil>0&&Time.unscaledTime>=baseFlashUntil){baseOutline.Hide();baseFlashUntil=0;}}
        void GameOver()
        {
            if(State==NavyGameState.GameOver)return;SetState(NavyGameState.GameOver);
            if(completion!=null){StopCoroutine(completion);completion=null;}Waves.StopWave();Panel.Preview.Cancel();
            foreach(var projectile in Object.FindObjectsByType<NavyProjectile>(FindObjectsSortMode.None))Destroy(projectile.gameObject);
            foreach(var runtime in Construction.StructureRoot.GetComponentsInChildren<TowerRuntime>())
            {
                runtime.enabled=false;var weapon=runtime.GetComponent<NavyWeapon>();if(weapon!=null)weapon.enabled=false;
                var oil=runtime.GetComponent<OilExtractor>();if(oil!=null)oil.enabled=false;var support=runtime.GetComponent<NavySupportTower>();if(support!=null)support.enabled=false;
            }
            NavyEffectPool.Instance?.Play(NavyEffectId.DeathLarge,Base.NavalEntry.position+Vector3.up*2,scale:2);ShowGameOver();
        }
        void ShowGameOver()
        {
            Panel.SetCommander(false);var popup=Panel.Tutorials.ShowForGame("ChooseFaction");if(popup==null)return;
            popup.Present("GAME OVER — Main Base destroyed","Final wave reached: "+Waves.CurrentWave+"\n\nRestart returns to faction selection and resets enemies, structures, resources, ranks, regions, upgrades and wave progress.","Restart game",Restart,null,null,null,null);
        }
        public void Restart()
        {
            if(restarting)return;restarting=true;Waves.StopWave();NavyEffectPool.Instance?.Clear();SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex,LoadSceneMode.Single);
        }
        public void InspectRegion(Vector3 point)
        {
            var region=Regions.At(point);if(region!=null&&!Regions.IsUnlocked(region.Value))
            {Panel.StatusText.text=region+" is locked. Other faction regions unlock after waves 10 and 20.";Panel.ShowPhysicalPanel();}
        }
        public bool SpaceAvailable(ConstructionDefinition entry,GridCell cell,out string reason)
        {
            var source=entry.Tiers[Session.GlobalTier(entry)].Prefab.GetComponent<BoxCollider>();
            if(source==null){reason=string.Empty;return true;}
            Vector3 point=!entry.IsFoundation&&cell.Foundation!=null?cell.Foundation.Foundation.TowerMount.position:Construction.Grid.World(cell);
            var half=Vector3.Max(source.size*.5f-Vector3.one*.035f,Vector3.one*.03f);
            foreach(var obstacle in Physics.OverlapBox(point+source.center,half,Quaternion.identity,~(1<<2|1<<5|1<<8),QueryTriggerInteraction.Ignore))
            {
                if(obstacle.name=="OceanFloor"||obstacle.name=="PlayerWaterSupport")continue;
                if(obstacle.gameObject.layer==9&&Regions.Regions.Any(r=>r.LockedBarrier!=null&&obstacle.transform.IsChildOf(r.LockedBarrier.transform)))continue;
                if(cell.Foundation!=null&&obstacle.transform.IsChildOf(cell.Foundation.transform))continue;
                if(obstacle.transform.IsChildOf(Panel.Preview.transform))continue;
                reason="Placement overlaps "+obstacle.name;return false;
            }
            reason=string.Empty;return true;
        }
        public void Refresh(){Changed?.Invoke();if(started)View.Refresh();}
        void SetState(NavyGameState value){State=value;Session.NotifyChanged();Refresh();}
        void OnDisable()
        {
            if(Session==null)return;Construction.Completed-=ActionCompleted;Session.Resources.Changed-=ResourcesChanged;Waves.WaveCompleted-=WaveCompleted;
            if(Base!=null&&Base.SharedBase!=null){Base.SharedBase.Health.Damaged-=BaseDamaged;Base.SharedBase.Health.Died-=GameOver;}
            if(Panel!=null)Panel.Tutorials.ReleaseGameControl();if(View!=null)View.gameObject.SetActive(false);
        }
    }
}
