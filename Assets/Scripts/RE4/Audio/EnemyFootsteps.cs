using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.RE4
{
    /// <summary>Pasos en 3D de aldeanos, perros y del Tyrant segun lo rapido que se mueven.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyFootsteps : MonoBehaviour
    {
        public bool dog;
        [Tooltip("Pasos por metro recorrido.")]
        public float stepsPerMeter = 1.3f;
        public float volume = 0.55f;
        public float pitch = 1f;

        private NavMeshAgent _agent;
        private AudioSource _src;
        private float _acc;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _src = gameObject.AddComponent<AudioSource>();
            _src.spatialBlend = 1f;
            _src.rolloffMode = AudioRolloffMode.Linear;
            _src.minDistance = 1.5f;
            _src.maxDistance = dog ? 22f : 26f;
            _src.playOnAwake = false;
        }

        private void Update()
        {
            if (_agent == null || !_agent.enabled) return;
            float speed = new Vector3(_agent.velocity.x, 0f, _agent.velocity.z).magnitude;
            if (speed < 0.2f) return;
            _acc += speed * Time.deltaTime * stepsPerMeter;
            if (_acc < 1f) return;
            _acc = 0f;

            SoundBank bank = SoundBank.Current;
            AudioClip clip = bank != null ? SoundBank.Pick(dog && bank.dogSteps.Length > 0 ? bank.dogSteps : bank.enemySteps) : null;
            if (clip == null) return;
            _src.pitch = pitch * Random.Range(0.9f, 1.1f);
            _src.PlayOneShot(clip, volume * Mathf.Clamp(speed / 3f, 0.5f, 1.2f));
        }

        /// <summary>Comprueba si el agente se está desplazando con velocidad suficiente para emitir pasos.</summary>
        public bool IsMoving => _agent != null && _agent.enabled && new Vector3(_agent.velocity.x, 0f, _agent.velocity.z).sqrMagnitude >= 0.04f;
    }
}
