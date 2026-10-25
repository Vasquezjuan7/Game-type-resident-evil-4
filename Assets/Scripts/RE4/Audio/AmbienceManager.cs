using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Ambiente nocturno: viento y bosque en bucle, crujidos y golpes lejanos al azar alrededor de Leon,
    /// y un zumbido de tension que sube durante el asedio.
    /// </summary>
    public class AmbienceManager : MonoBehaviour
    {
        public float windVolume = 0.35f;
        public float forestVolume = 0.25f;
        public float droneVolume = 0.07f; // la tension la lleva ahora la musica (MusicDirector)
        public Vector2 oneShotDelay = new Vector2(6f, 16f);

        private AudioSource _wind, _forest, _drone, _shot;
        private float _nextShot;
        private float _droneLevel;

        private void Start()
        {
            // la escena guardaba el zumbido alto: ahora la tension la pone la musica
            droneVolume = Mathf.Min(droneVolume, 0.07f);
            SoundBank bank = SoundBank.Current;
            _wind = Loop(bank != null ? SoundBank.Pick(bank.windLoops) : null, windVolume);
            _forest = Loop(bank != null ? SoundBank.Pick(bank.forestLoops) : null, forestVolume);
            _drone = Loop(bank != null ? SoundBank.Pick(bank.tensionDrones) : null, 0f);
            _shot = new GameObject("AmbientOneShot").AddComponent<AudioSource>();
            _shot.transform.SetParent(transform, false);
            _shot.spatialBlend = 1f;
            _shot.rolloffMode = AudioRolloffMode.Linear;
            _shot.minDistance = 5f;
            _shot.maxDistance = 60f;
            _nextShot = Time.time + Random.Range(oneShotDelay.x, oneShotDelay.y);
        }

        private AudioSource Loop(AudioClip clip, float volume)
        {
            if (clip == null) return null;
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.volume = volume;
            s.spatialBlend = 0f;
            s.playOnAwake = false;
            s.time = Random.Range(0f, clip.length * 0.9f);
            s.Play();
            return s;
        }

        private void Update()
        {
            HordeWaveManager h = HordeWaveManager.Instance;
            bool tense = h != null && h.SiegeActive && !h.Victory;
            _droneLevel = Mathf.MoveTowards(_droneLevel, tense ? 1f : 0.15f, Time.deltaTime * 0.15f);
            if (_drone != null) _drone.volume = droneVolume * _droneLevel;

            if (Time.time < _nextShot) return;
            _nextShot = Time.time + Random.Range(oneShotDelay.x, oneShotDelay.y);
            SoundBank bank = SoundBank.Current;
            AudioClip c = bank != null ? SoundBank.Pick(bank.randomOneShots) : null;
            Camera cam = Camera.main;
            if (c == null || cam == null) return;
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(12f, 35f);
            _shot.transform.position = cam.transform.position + new Vector3(r.x, Random.Range(0f, 5f), r.y);
            _shot.pitch = Random.Range(0.85f, 1.1f);
            _shot.PlayOneShot(c, Random.Range(0.35f, 0.7f));
        }
    }
}
