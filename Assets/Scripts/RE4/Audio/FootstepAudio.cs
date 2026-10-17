using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Pasos de Leon segun la superficie que pisa y respiracion agitada al correr mucho o con poca vida.
    /// </summary>
    public class FootstepAudio : MonoBehaviour
    {
        public float walkVolume = 0.45f;
        public float runVolume = 0.7f;

        private FirstPersonController _fps;
        private AudioSource _steps, _breath;
        private float _sprintTime;
        private float _nextBreath;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private void Awake()
        {
            _fps = GetComponent<FirstPersonController>();
            _steps = gameObject.AddComponent<AudioSource>();
            _steps.spatialBlend = 0f;
            _steps.playOnAwake = false;
            _breath = gameObject.AddComponent<AudioSource>();
            _breath.spatialBlend = 0f;
            _breath.playOnAwake = false;
        }

        private void OnEnable()
        {
            if (_fps != null) _fps.OnFootstep += Step;
        }

        private void OnDisable()
        {
            if (_fps != null) _fps.OnFootstep -= Step;
        }

        public string SurfaceUnder()
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, 1.6f, ~0, QueryTriggerInteraction.Collide);
            string best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(transform)) continue;
                SurfaceTag tag = _hits[i].collider.GetComponentInParent<SurfaceTag>();
                if (tag != null) return tag.surface;
                if (_hits[i].collider.isTrigger) continue;
                if (_hits[i].distance < bestDist)
                {
                    bestDist = _hits[i].distance;
                    best = Guess(_hits[i].collider.name);
                }
            }
            return best ?? "dirt";
        }

        private static string Guess(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("wood") || n.Contains("deck") || n.Contains("plank") || n.Contains("floor") || n.Contains("madera") || n.Contains("stable")) return "wood";
            if (n.Contains("stone") || n.Contains("stair") || n.Contains("church") || n.Contains("rock") || n.Contains("piedra") || n.Contains("tomb")) return "stone";
            if (n.Contains("grass") || n.Contains("cesped")) return "grass";
            return "dirt";
        }

        private void Step(bool running)
        {
            SoundBank bank = SoundBank.Current;
            SurfaceSteps s = bank != null ? bank.Steps(SurfaceUnder()) : null;
            AudioClip clip = s != null ? SoundBank.Pick(running && s.run.Length > 0 ? s.run : s.walk) : null;
            if (clip == null)
            {
                ProceduralAudio.PlayAt(ProceduralAudio.Impact(0.4f), transform.position, running ? 0.22f : 0.12f, 0.2f, 15f);
                return;
            }
            _steps.pitch = Random.Range(0.93f, 1.07f);
            _steps.PlayOneShot(clip, running ? runVolume : walkVolume);
        }

        private void Update()
        {
            if (_fps == null) return;
            _sprintTime = _fps.IsSprinting ? _sprintTime + Time.deltaTime : Mathf.Max(0f, _sprintTime - Time.deltaTime * 0.5f);
            bool tired = _sprintTime > 4f;
            bool hurt = PlayerHealth.Instance != null && PlayerHealth.Instance.Status == HealthStatus.Danger;
            if ((tired || hurt) && Time.time > _nextBreath)
            {
                SoundBank bank = SoundBank.Current;
                AudioClip c = bank != null ? SoundBank.Pick(bank.breathing) : null;
                if (c != null) _breath.PlayOneShot(c, hurt ? 0.5f : 0.35f);
                _nextBreath = Time.time + (c != null ? c.length : 1f) + Random.Range(0.1f, 0.5f);
            }
        }
    }
}
