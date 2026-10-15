using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Caja o barril de madera que se rompe con disparos, cuchillo o explosiones y suelta botin
    /// (pesetas, municion de las armas que llevas, hierbas o recursos para fabricar), como en RE4.
    /// </summary>
    public class Breakable : MonoBehaviour
    {
        public static readonly List<Breakable> All = new List<Breakable>();

        public float health = 12f;
        [Range(0f, 1f)] public float lootChance = 0.75f;
        [Tooltip("Astillas de madera al romperse.")]
        public bool debris = true;
        /// <summary>Se llama al romperse (la dinamita explota al dispararle).</summary>
        public System.Action onBroken;

        private bool _broken;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        public void Hit(float damage, Vector3 point, Vector3 direction)
        {
            if (_broken) return;
            health -= damage;
            FXManager.Get().SurfaceImpact(point, -direction);
            if (health <= 0f) Break(direction);
            else ProceduralAudio.PlayAt(ProceduralAudio.Impact(0.6f), point, 0.5f);
        }

        public void Break(Vector3 direction)
        {
            if (_broken) return;
            _broken = true;
            Bounds b = new Bounds(transform.position, Vector3.one * 0.6f);
            var rs = GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
            }
            Material mat = rs.Length > 0 ? rs[0].sharedMaterial : null;

            onBroken?.Invoke();
            if (!debris)
            {
                Destroy(gameObject);
                return;
            }

            // Astillas con fisica
            for (int i = 0; i < 9; i++)
            {
                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s.name = "Astilla";
                s.transform.position = b.center + Vector3.Scale(Random.insideUnitSphere, b.extents);
                s.transform.rotation = Random.rotation;
                s.transform.localScale = new Vector3(Random.Range(0.04f, 0.09f), Random.Range(0.02f, 0.04f), Random.Range(0.25f, 0.5f)) * Mathf.Max(0.6f, b.size.y);
                if (mat != null) s.GetComponent<Renderer>().sharedMaterial = mat;
                Rigidbody rb = s.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                rb.linearVelocity = (direction.normalized * 2f + Random.insideUnitSphere * 2.5f + Vector3.up * 2f);
                rb.angularVelocity = Random.insideUnitSphere * 8f;
                Destroy(s, 6f);
            }
            ProceduralAudio.PlayAt(ProceduralAudio.Impact(1.4f), b.center, 0.9f);
            SoundBank bank = SoundBank.Current;
            if (bank != null && bank.randomOneShots.Length > 0) ProceduralAudio.PlayAt(SoundBank.Pick(bank.randomOneShots), b.center, 0.5f, 0.1f, 30f);

            if (Random.value < lootChance && HordeWaveManager.Instance != null)
            {
                HordeWaveManager.Instance.DropLoot(new Vector3(b.center.x, b.min.y, b.center.z), 1f);
            }
            Destroy(gameObject);
        }

        /// <summary>Danio en area (cohetes).</summary>
        public static void Explode(Vector3 point, float radius)
        {
            foreach (var br in new List<Breakable>(All))
            {
                if (br == null) continue;
                Vector3 d = br.transform.position - point;
                if (d.magnitude <= radius) br.Break(d);
            }
        }
    }
}
