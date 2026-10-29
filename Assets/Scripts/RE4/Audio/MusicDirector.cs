using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Musica de suspenso: tema de exploracion (cuerdas graves, piano disonante) y tema de combate
    /// (ostinato de cuerdas, timbales y metales) con fundido segun el peligro, y un golpe orquestal
    /// cuando los aldeanos descubren a Leon. Se crea solo al empezar la partida.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        public float exploreVolume = 0.32f;
        public float combatVolume = 0.42f;
        public float fadeSpeed = 0.35f;

        private AudioSource _explore, _combat, _sting;
        private float _combatLevel;
        private bool _wasDanger;
        private float _dangerHold;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<MusicDirector>() != null) return;
            if (FindAnyObjectByType<HordeWaveManager>() == null && FindAnyObjectByType<PlayerHealth>() == null) return;
            new GameObject("Musica").AddComponent<MusicDirector>();
        }

        private void Start()
        {
            _explore = Track("RE4Audio/Musica/Musica_Exploracion", true);
            _combat = Track("RE4Audio/Musica/Musica_Combate", true);
            _sting = Track("RE4Audio/Musica/Musica_Alerta", false);
            if (_explore != null) { _explore.volume = 0f; _explore.Play(); }
            if (_combat != null) { _combat.volume = 0f; _combat.Play(); }
        }

        private AudioSource Track(string path, bool loop)
        {
            AudioClip clip = Resources.Load<AudioClip>(path);
            if (clip == null) return null;
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.spatialBlend = 0f;
            s.playOnAwake = false;
            s.priority = 16;
            s.ignoreListenerPause = true;
            return s;
        }

        /// <summary>Hay peligro si hay asedio o algun enemigo persigue a Leon cerca.</summary>
        private static bool Danger()
        {
            HordeWaveManager h = HordeWaveManager.Instance;
            if (h != null && h.SiegeActive && !h.Victory) return true;
            Transform p = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null;
            if (p == null) return false;
            foreach (var z in ZombieAI.All)
            {
                if (z == null || z.IsDead || z.State == GanadoState.Idle) continue;
                if ((z.transform.position - p.position).sqrMagnitude < 30f * 30f) return true;
            }
            foreach (var d in ZombieDogAI.All)
            {
                if (d == null || d.IsDead || d.State == DogState.Idle) continue;
                if ((d.transform.position - p.position).sqrMagnitude < 30f * 30f) return true;
            }
            return false;
        }

        private void Update()
        {
            bool danger = Danger();
            if (danger) _dangerHold = 6f; // no cortar la musica de combate de golpe
            else _dangerHold -= Time.unscaledDeltaTime;
            bool combat = danger || _dangerHold > 0f;

            if (danger && !_wasDanger && _sting != null) _sting.PlayOneShot(_sting.clip, 0.8f);
            _wasDanger = danger;

            _combatLevel = Mathf.MoveTowards(_combatLevel, combat ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
            bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;
            float master = dead ? 0.3f : 1f;
            if (_explore != null) _explore.volume = exploreVolume * (1f - _combatLevel) * master;
            if (_combat != null) _combat.volume = combatVolume * _combatLevel * master;
        }
    }
}
