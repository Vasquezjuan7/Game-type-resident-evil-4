using UnityEngine;
using Gameplay;

namespace Gameplay.RE4
{
    public enum ThrowKind
    {
        Sickle,
        Axe,
        Dynamite
    }

    /// <summary>
    /// Arma que lanza un aldeano (hoz, hacha o dinamita), como en RE4. Vuela en arco hacia donde va a estar Leon,
    /// se puede derribar de un disparo o desviar con el cuchillo (parry). La dinamita explota al caer y la mecha
    /// se agota, y tambien hiere a los aldeanos cercanos.
    /// </summary>
    public class ThrownWeapon : MonoBehaviour
    {
        public ThrowKind kind;
        public float damage = 14f;

        private Vector3 _velocity;
        private float _fuse = 2.4f;
        private bool _stuck;
        private bool _exploded;
        private float _life;
        private Transform _model;
        private Vector3 _spinAxis;

        private const float Gravity = -14f;

        /// <summary>Modelo sencillo del arma (tambien se usa para el arma que llevan en la mano).</summary>
        public static GameObject BuildModel(ThrowKind kind)
        {
            GameObject root = new GameObject(kind.ToString());
            Color wood = new Color(0.33f, 0.22f, 0.12f), metal = new Color(0.45f, 0.45f, 0.47f);
            switch (kind)
            {
                case ThrowKind.Sickle:
                    Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, -0.12f, 0f), new Vector3(0.03f, 0.12f, 0.03f), Vector3.zero, wood);
                    Part(PrimitiveType.Cube, root.transform, new Vector3(0.09f, 0.03f, 0f), new Vector3(0.2f, 0.025f, 0.006f), new Vector3(0f, 0f, 25f), metal);
                    Part(PrimitiveType.Cube, root.transform, new Vector3(0.19f, -0.04f, 0f), new Vector3(0.12f, 0.02f, 0.006f), new Vector3(0f, 0f, -55f), metal);
                    break;
                case ThrowKind.Axe:
                    Part(PrimitiveType.Cylinder, root.transform, new Vector3(0f, -0.05f, 0f), new Vector3(0.035f, 0.25f, 0.035f), Vector3.zero, wood);
                    Part(PrimitiveType.Cube, root.transform, new Vector3(0.06f, 0.17f, 0f), new Vector3(0.12f, 0.1f, 0.02f), Vector3.zero, metal);
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                        Part(PrimitiveType.Cylinder, root.transform, new Vector3((i - 1) * 0.035f, 0f, 0f), new Vector3(0.033f, 0.09f, 0.033f), Vector3.zero, new Color(0.7f, 0.12f, 0.08f));
                    Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0f, 0f), new Vector3(0.11f, 0.03f, 0.04f), Vector3.zero, new Color(0.25f, 0.2f, 0.15f));
                    GameObject spark = Part(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.12f, 0f), Vector3.one * 0.025f, Vector3.zero, new Color(1f, 0.8f, 0.3f));
                    Renderer sr = spark.GetComponent<Renderer>();
                    sr.material.EnableKeyword("_EMISSION");
                    sr.material.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.2f) * 4f);
                    Light l = spark.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = new Color(1f, 0.6f, 0.25f);
                    l.range = 3f;
                    l.intensity = 2f;
                    break;
            }
            return root;
        }

        private static GameObject Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Color c)
        {
            GameObject g = GameObject.CreatePrimitive(t);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localRotation = Quaternion.Euler(euler);
            g.GetComponent<Renderer>().material.color = c;
            return g;
        }

        /// <summary>Lanza el arma hacia Leon, apuntando a donde estara cuando llegue.</summary>
        public static ThrownWeapon Throw(ThrowKind kind, Vector3 from, float damage)
        {
            PlayerHealth ph = PlayerHealth.Instance;
            if (ph == null) return null;
            Vector3 target = ph.transform.position + Vector3.up * (kind == ThrowKind.Dynamite ? 0.2f : 1.2f);
            FirstPersonController fps = FirstPersonController.Instance;
            float dist = Vector3.Distance(from, target);
            float flight = Mathf.Clamp(dist / 13f, 0.55f, 1.5f) * (kind == ThrowKind.Dynamite ? 1.2f : 1f);
            if (fps != null) target += new Vector3(fps.Velocity.x, 0f, fps.Velocity.z) * flight * 0.8f; // anticipa el movimiento
            target += Random.insideUnitSphere * (kind == ThrowKind.Dynamite ? 1.2f : 0.5f);

            GameObject go = new GameObject("Lanzado_" + kind);
            go.transform.position = from;
            ThrownWeapon tw = go.AddComponent<ThrownWeapon>();
            tw.kind = kind;
            tw.damage = damage;
            tw._velocity = (target - from - 0.5f * Gravity * flight * flight * Vector3.up) / flight;
            tw._model = BuildModel(kind).transform;
            tw._model.SetParent(go.transform, false);
            tw._spinAxis = Vector3.Cross(Vector3.up, tw._velocity).normalized;

            // se puede derribar de un disparo
            SphereCollider col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.28f;
            go.layer = 0;
            Breakable br = go.AddComponent<Breakable>();
            br.health = 1f;
            br.lootChance = 0f;
            br.debris = false;
            br.onBroken = tw.ShotDown;

            ProceduralAudio.PlayAt(ProceduralAudio.Swoosh(), from, 0.7f, 0.15f, 25f);
            return tw;
        }

        private void ShotDown()
        {
            if (kind == ThrowKind.Dynamite) Explode();
            else ProceduralAudio.PlayAt(ProceduralAudio.Click(3200f, 60f), transform.position, 0.7f, 0.1f, 25f);
        }

        private void Update()
        {
            _life += Time.deltaTime;
            if (kind == ThrowKind.Dynamite)
            {
                _fuse -= Time.deltaTime;
                if (_fuse <= 0f) { Explode(); return; }
            }
            if (_stuck)
            {
                if (_life > 10f) Destroy(gameObject);
                return;
            }

            Vector3 prev = transform.position;
            _velocity += Vector3.up * Gravity * Time.deltaTime;
            Vector3 next = prev + _velocity * Time.deltaTime;
            if (kind != ThrowKind.Dynamite) _model.Rotate(_spinAxis, 900f * Time.deltaTime, Space.World);
            else _model.Rotate(_spinAxis, 300f * Time.deltaTime, Space.World);

            // ¿alcanza a Leon?
            PlayerHealth ph = PlayerHealth.Instance;
            if (ph != null && kind != ThrowKind.Dynamite)
            {
                Vector3 chest = ph.transform.position + Vector3.up * 1.2f;
                if (DistanceToSegment(chest, prev, next) < 0.6f)
                {
                    WeaponSystem ws = WeaponSystem.Instance;
                    if (ws != null && ws.TryParry(prev, false, out _))
                    {
                        Destroy(gameObject); // desviada con el cuchillo
                        return;
                    }
                    ph.TakeDamage(damage, prev);
                    ProceduralAudio.PlayAt(ProceduralAudio.Impact(1.3f), chest, 0.8f);
                    Destroy(gameObject);
                    return;
                }
            }

            // choca con el escenario: se clava (hoz/hacha) o rueda y se para (dinamita)
            Vector3 d = next - prev;
            if (Physics.Raycast(prev, d.normalized, out RaycastHit hit, d.magnitude + 0.05f, RE4Layers.ShotMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + hit.normal * 0.05f;
                if (kind == ThrowKind.Dynamite)
                {
                    _velocity = Vector3.Reflect(_velocity, hit.normal) * 0.25f;
                    if (_velocity.magnitude < 1.5f) _stuck = true;
                }
                else
                {
                    _stuck = true;
                    ProceduralAudio.PlayAt(ProceduralAudio.Impact(0.8f), hit.point, 0.6f, 0.1f, 25f);
                }
                return;
            }
            transform.position = next;
            if (transform.position.y < -10f || _life > 8f) Destroy(gameObject);
        }

        private void Explode()
        {
            if (_exploded) return;
            _exploded = true;
            Vector3 p = transform.position;
            const float radius = 4.5f;
            FXManager.Get().Explosion(p, radius);
            NoiseSystem.Emit(p, 40f);
            PlayerHealth ph = PlayerHealth.Instance;
            if (ph != null)
            {
                float dist = Vector3.Distance(p, ph.transform.position + Vector3.up);
                if (dist < radius) ph.TakeDamage(Mathf.Lerp(45f, 12f, dist / radius), p);
            }
            // tambien hiere a los aldeanos (como en RE4)
            var hurt = new System.Collections.Generic.HashSet<ZombieHealth>();
            foreach (var c in Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Collide))
            {
                ZombieHitbox hb = c.GetComponent<ZombieHitbox>();
                if (hb == null || hb.health == null || hb.health.IsDead || !hurt.Add(hb.health)) continue;
                float k = Mathf.Clamp01(1f - Vector3.Distance(p, hb.health.transform.position) / radius);
                var info = new DamageInfo
                {
                    amount = Mathf.Lerp(30f, 160f, k),
                    kind = DamageKind.Explosion,
                    part = BodyPart.Body,
                    point = hb.health.transform.position + Vector3.up,
                    direction = (hb.health.transform.position - p).normalized,
                    staggerPower = 1f,
                    knockback = 1f,
                    source = gameObject
                };
                if (!hb.health.ApplyDamage(info))
                {
                    ZombieAI ai = hb.health.GetComponent<ZombieAI>();
                    if (ai != null) ai.Knockdown(info.direction * 5f * k);
                }
            }
            Breakable.Explode(p, radius * 0.8f);
            Destroy(gameObject);
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
