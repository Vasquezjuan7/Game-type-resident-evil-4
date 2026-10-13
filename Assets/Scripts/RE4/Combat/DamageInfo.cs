using System;
using UnityEngine;

namespace Gameplay.RE4
{
    public enum BodyPart
    {
        Body,
        Head,
        Arm,
        Leg
    }

    public enum DamageKind
    {
        Bullet,
        Knife,
        Kick,
        Explosion
    }

    public struct DamageInfo
    {
        public float amount;
        public DamageKind kind;
        public BodyPart part;
        public Vector3 point;
        public Vector3 direction;
        public float staggerPower;
        public float knockback;
        public GameObject source;
    }

    /// <summary>
    /// Ruido de combate: los enemigos que lo escuchan se alertan.
    /// </summary>
    public static class NoiseSystem
    {
        public static event Action<Vector3, float> OnNoise;

        public static void Emit(Vector3 position, float radius)
        {
            OnNoise?.Invoke(position, radius);
        }
    }

    /// <summary>
    /// Capas usadas por el sistema de combate (creadas por el menu RE4).
    /// </summary>
    public static class RE4Layers
    {
        public const string WeaponLayerName = "FPWeapon";
        public const string HitboxLayerName = "Hitbox";
        public const string EnemyBodyLayerName = "EnemyBody";

        public static int Weapon => LayerMask.NameToLayer(WeaponLayerName);
        public static int Hitbox => LayerMask.NameToLayer(HitboxLayerName);
        public static int EnemyBody => LayerMask.NameToLayer(EnemyBodyLayerName);

        /// <summary>Mascara para los disparos: todo menos el arma y la capsula del cuerpo enemigo.</summary>
        public static int ShotMask
        {
            get
            {
                int mask = ~0;
                if (Weapon >= 0) mask &= ~(1 << Weapon);
                if (EnemyBody >= 0) mask &= ~(1 << EnemyBody);
                mask &= ~(1 << 2); // Ignore Raycast
                return mask;
            }
        }

        /// <summary>Indica si todas las capas personalizadas del sistema RE4 existen en el proyecto.</summary>
        public static bool AreLayersConfigured => Weapon >= 0 && Hitbox >= 0 && EnemyBody >= 0;
    }
}
