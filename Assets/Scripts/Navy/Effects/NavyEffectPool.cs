using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefence.Navy
{
    // Presentation-only service: it never owns a projectile, hit test, resource timer, or progression state.
    public sealed class NavyEffectPool : MonoBehaviour
    {
        static NavyEffectPool instance;
        public NavyEffectCatalog Catalog { get; private set; }
        readonly Dictionary<NavyEffect,Stack<NavyEffect>> inactive=new Dictionary<NavyEffect,Stack<NavyEffect>>();
        readonly List<NavyEffect> active=new List<NavyEffect>();
        readonly Dictionary<AudioClip,float> nextClip=new Dictionary<AudioClip,float>();
        readonly List<NavyEffect> voices=new List<NavyEffect>();
        readonly Dictionary<NavyEffectId,int> played=new Dictionary<NavyEffectId,int>();
        public int ActiveCount=>active.Count;
        public int ActiveAudioVoices=>voices.Count;
        public int InstancesCreated { get; private set; }
        public int PlayCount(NavyEffectId id)=>played.TryGetValue(id,out int count)?count:0;
        public static NavyEffectPool Instance
        {
            get
            {
                if(instance==null&&Application.isPlaying)
                {
                    var catalog=Resources.Load<NavyEffectCatalog>("NavyEffects");
                    if(catalog==null)return null;
                    var go=new GameObject("Navy Presentation Pool");instance=go.AddComponent<NavyEffectPool>();instance.Initialize(catalog);
                }
                return instance;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic()=>instance=null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Startup(){_=Instance;}
        void Initialize(NavyEffectCatalog catalog)
        {
            Catalog=catalog;DontDestroyOnLoad(gameObject);SceneManager.sceneLoaded+=SceneLoaded;
            foreach(var prefab in catalog.Prefabs)
            {
                if(prefab==null)continue;
                var stack=new Stack<NavyEffect>();inactive[prefab]=stack;
                for(int i=0;i<catalog.PrewarmPerPrefab;i++)stack.Push(Create(prefab));
            }
        }
        NavyEffect Create(NavyEffect prefab)
        {
            var effect=Instantiate(prefab,transform);effect.gameObject.SetActive(false);effect.Prepare(this,prefab);InstancesCreated++;return effect;
        }
        public NavyEffect Play(NavyEffectId id,Vector3 position,Quaternion rotation=default,Transform follow=null,float scale=1)
        {
            if(Catalog==null)return null;
            var prefab=Catalog.Find(id);if(prefab==null||active.Count>=Catalog.MaximumActiveEffects)return null;
            int same=0;foreach(var current in active)if(current.Source==prefab)same++;
            if(same>=prefab.MaximumConcurrent)return null;
            if(!inactive.TryGetValue(prefab,out var stack)){stack=new Stack<NavyEffect>();inactive.Add(prefab,stack);}
            var effect=stack.Count>0?stack.Pop():Create(prefab);
            active.Add(effect);played[id]=PlayCount(id)+1;
            effect.Begin(position,rotation==default?Quaternion.identity:rotation,follow,scale);
            return effect;
        }
        internal bool StartAudio(NavyEffect effect)
        {
            var cue=effect.Audio;if(cue.Clip==null||cue.Volume<=0)return false;
            CullVoices();
            int group=0;foreach(var voice in voices)if(voice.Audio.Group==cue.Group)group++;
            int cap=cue.Group==NavyAudioGroup.Weapon?Catalog.MaximumWeaponVoices:cue.Group==NavyAudioGroup.Machinery?Catalog.MaximumMachineryVoices:5;
            int maximum=cue.Group==NavyAudioGroup.Interaction?Catalog.MaximumAudioVoices:Catalog.MaximumAudioVoices-2;
            if(voices.Count>=maximum||group>=cap||nextClip.TryGetValue(cue.Clip,out float ready)&&Time.unscaledTime<ready)return false;
            nextClip[cue.Clip]=Time.unscaledTime+cue.RetriggerDelay;voices.Add(effect);return true;
        }
        void CullVoices(){for(int i=voices.Count-1;i>=0;i--)if(!voices[i].HasAudioLease)voices.RemoveAt(i);}
        internal void Release(NavyEffect effect)
        {
            if(!active.Remove(effect))return;voices.Remove(effect);effect.End();
            inactive[effect.Source].Push(effect);
        }
        void SceneLoaded(Scene scene,LoadSceneMode mode){if(mode!=LoadSceneMode.Single)return;for(int i=active.Count-1;i>=0;i--)Release(active[i]);nextClip.Clear();voices.Clear();}
        void OnDestroy(){SceneManager.sceneLoaded-=SceneLoaded;if(instance==this)instance=null;}
        public void Clear(){SceneLoaded(default,default);}
    }
}
