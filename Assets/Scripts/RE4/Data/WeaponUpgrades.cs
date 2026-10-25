using System;
using UnityEngine;

namespace Gameplay.RE4
{
    public enum UpgradeStat
    {
        Power,
        FireRate,
        ReloadSpeed,
        Capacity
    }

    /// <summary>Niveles de mejora de un arma concreta (viajan con el arma dentro del maletin).</summary>
    [Serializable]
    public class WeaponUpgrades
    {
        public int power;
        public int fireRate;
        public int reloadSpeed;
        public int capacity;

        public int Get(UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.Power: return power;
                case UpgradeStat.FireRate: return fireRate;
                case UpgradeStat.ReloadSpeed: return reloadSpeed;
                default: return capacity;
            }
        }

        public void Set(UpgradeStat stat, int value)
        {
            switch (stat)
            {
                case UpgradeStat.Power: power = value; break;
                case UpgradeStat.FireRate: fireRate = value; break;
                case UpgradeStat.ReloadSpeed: reloadSpeed = value; break;
                default: capacity = value; break;
            }
        }

        public WeaponUpgrades Clone() => (WeaponUpgrades)MemberwiseClone();
    }

    /// <summary>Estadisticas reales de un arma teniendo en cuenta sus mejoras.</summary>
    public static class WeaponStats
    {
        public static readonly UpgradeStat[] AllStats = { UpgradeStat.Power, UpgradeStat.FireRate, UpgradeStat.ReloadSpeed, UpgradeStat.Capacity };

        public static string StatName(UpgradeStat stat)
        {
            switch (stat)
            {
                case UpgradeStat.Power: return "Potencia";
                case UpgradeStat.FireRate: return "Cadencia";
                case UpgradeStat.ReloadSpeed: return "Vel. de recarga";
                default: return "Capacidad";
            }
        }

        private static int Level(CaseEntry e, UpgradeStat s) => e != null && e.upgrades != null ? e.upgrades.Get(s) : 0;

        public static float Damage(WeaponDefinition w, CaseEntry e) => Damage(w, Level(e, UpgradeStat.Power));
        public static float Damage(WeaponDefinition w, int level) => w.damage * (1f + w.powerPerLevel * level);

        public static float FireInterval(WeaponDefinition w, CaseEntry e) => FireInterval(w, Level(e, UpgradeStat.FireRate));
        public static float FireInterval(WeaponDefinition w, int level) => w.fireInterval / (1f + w.ratePerLevel * level);

        public static float ReloadTime(WeaponDefinition w, CaseEntry e) => ReloadTime(w, Level(e, UpgradeStat.ReloadSpeed));
        public static float ReloadTime(WeaponDefinition w, int level) => w.reloadTime * Mathf.Max(0.35f, 1f - w.reloadPerLevel * level);

        public static int MagazineSize(WeaponDefinition w, CaseEntry e) => MagazineSize(w, Level(e, UpgradeStat.Capacity));
        public static int MagazineSize(WeaponDefinition w, int level) => w.magazineSize + w.capacityPerLevel * level;

        /// <summary>El lanzacohetes no se mejora (como en RE4).</summary>
        public static bool CanUpgrade(WeaponDefinition w) => w != null && w.weaponClass != WeaponClass.RocketLauncher && w.maxUpgradeLevel > 0;

        /// <summary>Coste del siguiente nivel; la cadencia y la capacidad cuestan algo distinto que la potencia.</summary>
        public static int Cost(WeaponDefinition w, UpgradeStat stat, int currentLevel)
        {
            float factor = stat == UpgradeStat.Power ? 1.25f : stat == UpgradeStat.FireRate ? 1.1f : stat == UpgradeStat.Capacity ? 0.85f : 0.7f;
            int cost = Mathf.RoundToInt(w.upgradeBaseCost * factor * (1f + currentLevel * 0.75f) / 500f) * 500;
            return Mathf.Max(500, cost);
        }

        /// <summary>Texto "actual -> siguiente" para la tienda.</summary>
        public static string Describe(WeaponDefinition w, UpgradeStat stat, int level)
        {
            switch (stat)
            {
                case UpgradeStat.Power: return $"{Damage(w, level):0}{(w.pellets > 1 ? $" x{w.pellets}" : "")}";
                case UpgradeStat.FireRate: return $"{1f / Mathf.Max(0.01f, FireInterval(w, level)):0.0} disp/s";
                case UpgradeStat.ReloadSpeed: return $"{ReloadTime(w, level):0.00} s";
                default: return $"{MagazineSize(w, level)} balas";
            }
        }

        /// <summary>Suma total de niveles de mejora aplicados sobre un arma.</summary>
        public static int TotalUpgradeLevels(CaseEntry e)
        {
            if (e == null || e.upgrades == null) return 0;
            return e.upgrades.power + e.upgrades.fireRate + e.upgrades.reloadSpeed + e.upgrades.capacity;
        }
    }
}
