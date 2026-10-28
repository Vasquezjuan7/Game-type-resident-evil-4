using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>Cohete del lanzacohetes: vuela en linea recta y explota al impactar.</summary>
    public class RocketProjectile : MonoBehaviour
    {
        private WeaponDefinition _weapon;
        private GameObject _owner;
        private Vector3 _velocity;
        private float _life = 6f;
        private bool _exploded;

        public static void Launch(Vector3 position, Vector3 direction, WeaponDefinition weapon, GameObject owner)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Rocket";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(0.09f, 0.22f, 0.09f);
            go.GetComponent<Renderer>().material.color = new Color(0.25f, 0.28f, 0.2f);
            go.layer = 2;

            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.6f;
            trail.startWidth = 0.18f;
            trail.endWidth = 0.6f;
            CombatFXLibrary lib = FXManager.Get().library;
            if (lib != null && lib.smoke != null) trail.sharedMaterial = lib.smoke;
            trail.startColor = new Color(1f, 0.7f, 0.4f, 0.9f);
            trail.endColor = new Color(0.3f, 0.3f, 0.3f, 0f);

            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.3f);
            l.range = 5f;
            l.intensity = 3f;

            RocketProjectile r = go.AddComponent<RocketProjectile>();
            r._weapon = weapon;
            r._owner = owner;
            r._velocity = direction.normalized * Mathf.Max(5f, weapon.projectileSpeed);
        }

        private void Update()
        {
            if (_exploded) return;
            float dt = Time.deltaTime;
            Vector3 step = _velocity * dt;
            if (Physics.SphereCast(transform.position, 0.12f, _velocity.normalized, out RaycastHit hit, step.magnitude, RE4Layers.ShotMask, QueryTriggerInteraction.Collide))
            {
                if (_owner == null || !hit.collider.transform.IsChildOf(_owner.transform))
                {
                    if (!hit.collider.isTrigger || hit.collider.GetComponent<ZombieHitbox>() != null)
                    {
                        Explode(hit.point - _velocity.normalized * 0.2f);
                        return;
                    }
                }
            }
            transform.position += step;
            _life -= dt;
            if (_life <= 0f) Explode(transform.position);
        }

        private void Explode(Vector3 point)
        {
            _exploded = true;
            float radius = Mathf.Max(1f, _weapon.explosionRadius);
            FXManager.Get().Explosion(point, radius);
            Breakable.Explode(point, radius);
            NoiseSystem.Emit(point, 80f);

            var damaged = new HashSet<ZombieHealth>();
            foreach (var c in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Collide))
            {
                ZombieHitbox hb = c.GetComponent<ZombieHitbox>();
                if (hb == null || hb.health == null || hb.health.IsDead || damaged.Contains(hb.health)) continue;
                damaged.Add(hb.health);
                float d = Vector3.Distance(point, hb.health.transform.position);
                float k = Mathf.Clamp01(1f - d / radius);
                var info = new DamageInfo
                {
                    amount = _weapon.damage * Mathf.Lerp(0.35f, 1f, k),
                    kind = DamageKind.Explosion,
                    part = BodyPart.Body,
                    point = hb.health.transform.position + Vector3.up,
                    direction = (hb.health.transform.position - point).normalized,
                    staggerPower = 1f,
                    knockback = 1f,
                    source = _owner
                };
                bool killed = hb.health.ApplyDamage(info);
                if (!killed)
                {
                    ZombieAI ai = hb.health.GetComponent<ZombieAI>();
                    if (ai != null) ai.Knockdown(info.direction * 6f * k);
                }
            }

            PlayerHealth ph = PlayerHealth.Instance;
            if (ph != null)
            {
                float d = Vector3.Distance(point, ph.transform.position + Vector3.up);
                if (d < radius) ph.TakeDamage(Mathf.Lerp(60f, 10f, d / radius), point);
            }

            Destroy(gameObject);
        }

        /// <summary>Indica si el cohete sigue activo en vuelo y devuelve el tiempo restante antes de detonar.</summary>
        public bool IsInFlight => !_exploded && _life > 0f;
        public float RemainingFlightTime => Mathf.Max(0f, _life);
    }
}
