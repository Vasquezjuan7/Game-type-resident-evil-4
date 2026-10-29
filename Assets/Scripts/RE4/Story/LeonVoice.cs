using System.Collections;
using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Voz de Leon (Gemini TTS): habla en las conversaciones y suelta frases cortas en combate
    /// (recargar rodeado, malherido, agarrado, parry, primer perro, el Tyrant...). Se crea sola en el jugador.
    /// </summary>
    public class LeonVoice : MonoBehaviour
    {
        public static LeonVoice Instance { get; private set; }

        private static readonly string[] Reload = { "¡Recargando!", "Necesito un segundo...", "Vamos, vamos..." };
        private static readonly string[] Hurt = { "Esto se está poniendo feo.", "Necesito curarme.", "No aguantaré mucho así." };
        private static readonly string[] Grabbed = { "¡Suéltame!", "¡Quítate de encima!" };
        private static readonly string[] Parry = { "¡Ni lo sueñes!", "¡Buen intento!", "¡Atrás!" };
        private static readonly string[] Kill = { "Lo siento, amigo.", "Uno menos.", "Descansa en paz." };
        private static readonly string[] Dogs = { "¿Perros? Genial..." };
        private static readonly string[] Boss = { "¿Pero qué demonios es eso?" };
        private static readonly string[] NoAmmo = { "Me estoy quedando sin balas." };

        public bool IsSpeaking => _src != null && _src.isPlaying;
        /// <summary>Hay una frase de Leon generandose todavia.</summary>
        public bool Pending { get; private set; }

        private AudioSource _src;
        private float _next;
        private bool _wasReloading, _wasDanger, _wasGrabbed, _sawDog, _sawBoss, _saidNoAmmo;
        private WeaponSystem _weapons;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (PlayerHealth.Instance == null || PlayerHealth.Instance.GetComponent<LeonVoice>() != null) return;
            PlayerHealth.Instance.gameObject.AddComponent<LeonVoice>();
        }

        private void Awake()
        {
            Instance = this;
            _src = gameObject.AddComponent<AudioSource>();
            _src.spatialBlend = 0f;
            _src.playOnAwake = false;
            _src.volume = 0.95f;
        }

        private void Start()
        {
            _weapons = GetComponent<WeaponSystem>();
            if (_weapons != null)
            {
                _weapons.OnParry += p => { if (p || Random.value < 0.5f) Quip(Parry); };
                _weapons.OnHitConfirmed += (kill, head) => { if (kill && head && Random.value < 0.15f) Quip(Kill); };
            }
            if (!NpcVoice.AllowBackgroundVoices) return;
            NpcVoice v = NpcVoice.Get();
            foreach (var set in new[] { Reload, Hurt, Grabbed, Parry, Kill, Dogs, Boss, NoAmmo })
                foreach (var line in set) v.Prefetch(line, NpcVoice.VoiceLeon);
        }

        /// <summary>
        /// Dice la frase solo si su voz ya esta generada; si no, la deja en cola para mas adelante.
        /// En las conversaciones asi no se gasta el limite de Gemini antes de la respuesta del NPC.
        /// </summary>
        public void SayIfCached(string text)
        {
            if (!NpcVoice.Enabled || string.IsNullOrWhiteSpace(text) || _src == null) return;
            AudioClip clip = NpcVoice.Get().Cached(text, NpcVoice.VoiceLeon);
            if (clip == null)
            {
                if (NpcVoice.AllowBackgroundVoices) NpcVoice.Get().Prefetch(text, NpcVoice.VoiceLeon);
                return;
            }
            _src.Stop();
            _src.pitch = 1f;
            _src.clip = clip;
            _src.Play();
        }

        /// <summary>Dice una frase concreta (conversaciones). Se genera si hace falta.</summary>
        public void SayNow(string text)
        {
            if (!NpcVoice.Enabled || string.IsNullOrWhiteSpace(text)) return;
            StartCoroutine(SayRoutine(text));
        }

        private IEnumerator SayRoutine(string text)
        {
            Pending = true;
            AudioClip clip = null;
            yield return NpcVoice.Get().GetClip(text, NpcVoice.VoiceLeon, c => clip = c);
            Pending = false;
            if (clip == null) yield break;
            _src.Stop();
            _src.pitch = 1f;
            _src.clip = clip;
            _src.Play();
        }

        /// <summary>Frase corta de combate: solo si ya esta generada (para que no llegue tarde).</summary>
        private void Quip(string[] set)
        {
            if (!NpcVoice.Enabled || Time.time < _next || IsSpeaking) return;
            NpcVoice v = NpcVoice.Get();
            int start = Random.Range(0, set.Length);
            for (int i = 0; i < set.Length; i++)
            {
                AudioClip c = v.Cached(set[(start + i) % set.Length], NpcVoice.VoiceLeon);
                if (c == null) continue;
                _src.pitch = 1f;
                _src.PlayOneShot(c, 0.95f);
                _next = Time.time + Random.Range(7f, 12f);
                return;
            }
        }

        private void Update()
        {
            PlayerHealth ph = PlayerHealth.Instance;
            if (ph == null || ph.IsDead) return;

            bool reloading = _weapons != null && _weapons.IsReloading;
            if (reloading && !_wasReloading && EnemyNear(12f) && Random.value < 0.6f) Quip(Reload);
            _wasReloading = reloading;

            bool danger = ph.Status == HealthStatus.Danger;
            if (danger && !_wasDanger) Quip(Hurt);
            _wasDanger = danger;

            if (ph.IsGrabbed && !_wasGrabbed) { _next = 0f; Quip(Grabbed); }
            _wasGrabbed = ph.IsGrabbed;

            if (_weapons != null && _weapons.Current != null && !_saidNoAmmo && _weapons.Loaded == 0 && _weapons.Reserve == 0)
            {
                _saidNoAmmo = true;
                Quip(NoAmmo);
            }
            if (_weapons != null && _weapons.Reserve > 0) _saidNoAmmo = false;

            if (!_sawDog)
            {
                foreach (var d in ZombieDogAI.All)
                {
                    if (d != null && d.State != DogState.Idle && (d.transform.position - transform.position).sqrMagnitude < 400f)
                    {
                        _sawDog = true;
                        _next = 0f;
                        Quip(Dogs);
                        break;
                    }
                }
            }
            HordeWaveManager h = HordeWaveManager.Instance;
            if (!_sawBoss && h != null && h.Boss != null && !h.Boss.IsDead && (h.Boss.transform.position - transform.position).sqrMagnitude < 900f)
            {
                _sawBoss = true;
                _next = 0f;
                Quip(Boss);
            }
        }

        private bool EnemyNear(float radius)
        {
            foreach (var z in ZombieAI.All)
            {
                if (z == null || z.IsDead || z.State == GanadoState.Idle) continue;
                if ((z.transform.position - transform.position).sqrMagnitude < radius * radius) return true;
            }
            return false;
        }
    }
}
