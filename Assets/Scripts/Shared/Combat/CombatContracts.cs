using UnityEngine;

namespace TowerDefence.Shared
{
    public enum Faction { Army, Navy, Sky }
    [System.Flags]
    public enum TargetDomain { None = 0, Ground = 1, Water = 2, Air = 4, All = 7 }
    public enum TowerKind { Turret, Cannon, Slow, Support, Resource, Foundation }
    public enum StructureCategory { Attack, Defense, Support, Building }
    public enum EnemyKind { PatrolBoat, Gunboat, Destroyer, Battleship, FastAssaultBoat, Custom = 100 }
    public enum DamageKind { Kinetic, Energy, Poison }

    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly DamageKind Kind;
        public readonly float ArmorPenetration;
        public readonly Object Source;
        public DamageInfo(float amount, DamageKind kind = DamageKind.Kinetic,
            Object source = null, float armorPenetration = 0)
        {
            Amount = Mathf.Max(0, amount);
            Kind = kind;
            Source = source;
            ArmorPenetration = Mathf.Max(0, armorPenetration);
        }
    }

    // Billy's combat implementation can implement these contracts or use an adapter.
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo damage);
    }

    public interface IEnemyTarget : IDamageable
    {
        Transform AimTransform { get; }
        TargetDomain Domain { get; }
        string EnemyId { get; }
    }

    public interface IStatusEffectReceiver
    {
        void ApplySlow(Object sourceId, float strength, float duration);
        void ApplyPoison(Object sourceId, float damagePerTick, float duration, float tickInterval);
    }

    public readonly struct TowerBuff
    {
        public readonly float Damage, Range, DefenseFrequency;
        public TowerBuff(float damage = 0, float range = 0, float defenseFrequency = 0)
        {
            Damage = Mathf.Max(0, damage);
            Range = Mathf.Max(0, range);
            DefenseFrequency = Mathf.Max(0, defenseFrequency);
        }
    }

    public interface ITowerBuffReceiver
    {
        Faction Faction { get; }
        TowerKind Kind { get; }
        bool CanReceiveBuff { get; }
        Transform BuffTransform { get; }
        void SetBuff(Object sourceId, TowerBuff buff);
        void RemoveBuff(Object sourceId);
    }

    public interface ITowerRangeModifier
    {
        float GetRangeMultiplier(Faction faction);
    }
}
