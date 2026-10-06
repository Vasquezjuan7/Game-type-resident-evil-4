using System.Collections.Generic;
using UnityEngine;
using Gameplay.RE4;

namespace Gameplay
{
    /// <summary>
    /// Vida del aldeano con dano por zona. Crea automaticamente las zonas de impacto
    /// sobre los huesos humanoides si el prefab no las trae.
    /// </summary>
    public class ZombieHealth : MonoBehaviour
    {
        [Header("Vida")]
        public float maxHealth = 120f;
        public float currentHealth = 120f;
        public bool isDead;

        [Header("Botin (estilo RE4)")]
        [Range(0f, 1f)] public float dropChance = 0.45f;

        public bool IsDead => isDead;
        public float Normalized => Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));

        private ZombieAI _ai;
        private ZombieDogAI _dog;
        private readonly List<Collider> _hitboxes = new List<Collider>();

        private void Awake()
        {
            _ai = GetComponent<ZombieAI>();
            _dog = GetComponent<ZombieDogAI>();
            currentHealth = maxHealth;
            EnsureHitboxes();
        }

        public void SetMaxHealth(float hp)
        {
            maxHealth = hp;
            currentHealth = hp;
        }

        /// <summary>Aplica dano. Devuelve true si el golpe lo mato.</summary>
        public bool ApplyDamage(DamageInfo info)
        {
            if (isDead) return false;
            currentHealth -= info.amount;
            if (currentHealth <= 0f)
            {
                Die(info);
                return true;
            }
            if (_ai != null) _ai.OnDamaged(info);
            if (_dog != null) _dog.OnDamaged(info);
            return false;
        }

        private void Die(DamageInfo info)
        {
            if (isDead) return;
            isDead = true;
            currentHealth = 0f;

            if (_ai != null) _ai.OnDeath(info);
            if (_dog != null) _dog.OnDeath(info);

            foreach (var c in _hitboxes) if (c != null) c.enabled = false;

            if (HordeWaveManager.Instance != null)
            {
                HordeWaveManager.Instance.NotifyZombieKilled(this, transform.position);
            }
        }

        // ------------------------------------------------------------ Zonas de impacto

        public void EnsureHitboxes()
        {
            _hitboxes.Clear();
            foreach (var hb in GetComponentsInChildren<ZombieHitbox>(true))
            {
                hb.health = this;
                Collider c = hb.GetComponent<Collider>();
                if (c != null) _hitboxes.Add(c);
            }
            if (_hitboxes.Count > 0) return;

            Animator anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                AddSphere(anim, HumanBodyBones.Head, BodyPart.Head, 0.13f, new Vector3(0f, 0.06f, 0.02f));
                AddCapsuleBetween(anim, HumanBodyBones.Hips, HumanBodyBones.Neck, BodyPart.Body, 0.2f);
                AddCapsuleBetween(anim, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, BodyPart.Arm, 0.07f);
                AddCapsuleBetween(anim, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, BodyPart.Arm, 0.06f);
                AddCapsuleBetween(anim, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, BodyPart.Arm, 0.07f);
                AddCapsuleBetween(anim, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, BodyPart.Arm, 0.06f);
                AddCapsuleBetween(anim, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, BodyPart.Leg, 0.1f);
                AddCapsuleBetween(anim, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, BodyPart.Leg, 0.08f);
                AddCapsuleBetween(anim, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, BodyPart.Leg, 0.1f);
                AddCapsuleBetween(anim, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, BodyPart.Leg, 0.08f);
            }

            if (_hitboxes.Count == 0)
            {
                // Respaldo para modelos no humanoides: cabeza y cuerpo por altura
                AddFallback("Hitbox_Body", BodyPart.Body, new Vector3(0f, 0.9f, 0f), new Vector3(0.55f, 1.5f, 0.4f));
                AddFallback("Hitbox_Head", BodyPart.Head, new Vector3(0f, 1.65f, 0.05f), new Vector3(0.26f, 0.28f, 0.26f));
            }
        }

        private void Register(GameObject go, BodyPart part, Collider c)
        {
            c.isTrigger = true;
            int layer = RE4Layers.Hitbox;
            if (layer >= 0) go.layer = layer;
            ZombieHitbox hb = go.AddComponent<ZombieHitbox>();
            hb.part = part;
            hb.health = this;
            _hitboxes.Add(c);
        }

        private void AddSphere(Animator anim, HumanBodyBones bone, BodyPart part, float radius, Vector3 offset)
        {
            Transform t = anim.GetBoneTransform(bone);
            if (t == null) return;
            GameObject go = new GameObject("Hitbox_" + bone);
            go.transform.SetParent(t, false);
            float s = Mathf.Max(0.0001f, t.lossyScale.x);
            SphereCollider sc = go.AddComponent<SphereCollider>();
            sc.radius = radius / s;
            go.transform.position = t.position + transform.TransformVector(offset);
            Register(go, part, sc);
        }

        private void AddCapsuleBetween(Animator anim, HumanBodyBones a, HumanBodyBones b, BodyPart part, float radius)
        {
            Transform ta = anim.GetBoneTransform(a);
            Transform tb = anim.GetBoneTransform(b);
            if (ta == null || tb == null) return;
            GameObject go = new GameObject("Hitbox_" + a);
            go.transform.SetParent(ta, false);
            Vector3 mid = (ta.position + tb.position) * 0.5f;
            go.transform.position = mid;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, (tb.position - ta.position).normalized);
            float s = Mathf.Max(0.0001f, go.transform.lossyScale.x);
            CapsuleCollider cc = go.AddComponent<CapsuleCollider>();
            cc.direction = 1;
            cc.radius = radius / s;
            cc.height = (Vector3.Distance(ta.position, tb.position) + radius * 2f) / s;
            Register(go, part, cc);
        }

        private void AddFallback(string name, BodyPart part, Vector3 localPos, Vector3 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            Register(go, part, bc);
        }
    }
}
