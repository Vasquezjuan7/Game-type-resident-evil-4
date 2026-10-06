using System.Collections;
using UnityEngine;

namespace Gameplay
{
    /// <summary>
    /// Desaparicion del cadaver al estilo Resident Evil 4: deja terminar la animacion de muerte,
    /// congela la pose y el cuerpo se hunde en el suelo hasta desaparecer.
    /// </summary>
    public class CorpseVanish : MonoBehaviour
    {
        [Tooltip("Segundos que el cuerpo queda en el suelo antes de empezar a desaparecer.")]
        public float delay = 3.5f;
        [Tooltip("Duracion del hundimiento.")]
        public float sinkTime = 1.8f;
        [Tooltip("Metros que baja el cuerpo.")]
        public float sinkDepth = 1.4f;

        /// <summary>Empieza la desaparicion del cadaver (no hace nada si ya habia empezado).</summary>
        public static CorpseVanish Begin(GameObject corpse, float delay = 3.5f, float sinkTime = 1.8f)
        {
            if (corpse == null) return null;
            CorpseVanish v = corpse.GetComponent<CorpseVanish>();
            if (v != null) return v;
            v = corpse.AddComponent<CorpseVanish>();
            v.delay = delay;
            v.sinkTime = sinkTime;
            return v;
        }

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(delay);

            // pose final congelada: sin esto la animacion podria volver a levantarlo mientras se hunde
            foreach (var a in GetComponentsInChildren<Animator>()) a.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            // las sombras de un cuerpo bajo el suelo se verian flotando
            foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Vector3 from = transform.position;
            Vector3 to = from + Vector3.down * sinkDepth;
            float t = 0f;
            while (t < sinkTime)
            {
                t += Time.deltaTime;
                float k = t / sinkTime;
                transform.position = Vector3.Lerp(from, to, k * k);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
