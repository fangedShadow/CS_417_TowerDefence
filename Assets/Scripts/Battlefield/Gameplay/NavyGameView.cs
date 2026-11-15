using TMPro;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefence.Battlefield
{
    public sealed class NavyGameView : MonoBehaviour
    {
        public TMP_Text HealthWave, RegionLegend;
        public Button Primary;
        public AudioSource Warning;
        PlayableNavyGame game;
        public void Bind(PlayableNavyGame owner)
        {
            game=owner;Primary.onClick.RemoveAllListeners();Primary.onClick.AddListener(()=>
            {if(game.State==NavyGameState.Preparation)game.StartNextWave();else game.Resume();});
            game.Waves.Changed+=Refresh;game.Regions.Changed+=Refresh;Refresh();
        }
        public void Refresh()
        {
            if(game==null)return;
            var health=game.Base.SharedBase!=null?game.Base.SharedBase.Health:null;
            HealthWave.text="BASE "+(health!=null?health.CurrentHealth.ToString("0")+" / "+health.MaxHealth.ToString("0"):"DESTROYED")+"\n"+
                (game.State==NavyGameState.GameOver?"FINAL WAVE "+game.Waves.CurrentWave:
                game.State==NavyGameState.WaveActive?"WAVE "+game.Waves.CurrentWave+" · "+game.Waves.ActiveCount+" ships":"NEXT WAVE "+(game.Waves.CurrentWave+1));
            HealthWave.color=health!=null&&health.CurrentHealth/health.MaxHealth<=game.Settings.CriticalHealthFraction?new Color(1,.28f,.15f):Color.white;
            Primary.interactable=game.State!=NavyGameState.WaveActive&&game.State!=NavyGameState.WaveCompleted;
            var feedback=Primary.GetComponent<UnavailableActionFeedback>();if(feedback!=null)feedback.Configure(game.Panel,"A wave is already in progress.");
            Primary.GetComponentInChildren<TMP_Text>().text=game.State==NavyGameState.Preparation?"Start wave "+(game.Waves.CurrentWave+1):
                game.State==NavyGameState.Tutorial?"Resume tutorial":game.State==NavyGameState.GameOver?"Game over / Restart":game.State==NavyGameState.FactionUnlock?"Faction regions":
                game.State==NavyGameState.WaveActive?"Wave in progress":game.State==NavyGameState.WaveCompleted?"Wave cleared":"Choose faction";
            if(RegionLegend!=null)RegionLegend.text=string.Join("\n",new[]{BattlefieldRegion.MainBase,BattlefieldRegion.Ocean,BattlefieldRegion.LeftLand,BattlefieldRegion.CenterLand,BattlefieldRegion.RightLand}.Select(r=>r+": "+(game.Regions.IsUnlocked(r)?"OPEN":"LOCKED")));
        }
        public void Warn(){if(Warning!=null&&game.Settings.WarningClip!=null){Warning.clip=game.Settings.WarningClip;Warning.volume=.35f;Warning.Play();}}
        void OnDestroy(){if(game!=null){game.Waves.Changed-=Refresh;game.Regions.Changed-=Refresh;}}
    }
}
