using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Gameplay.RE4
{
    /// <summary>
    /// Animal de granja (vaca o cerdo). Los modelos importados no traen animaciones, asi que el paso,
    /// el pastar y la caida se hacen por codigo sobre el hijo "Modelo".
    ///  - Deambula alrededor de su corral (home) y se para a pastar.
    ///  - Huye de los disparos (NoiseSystem), de Leon si corre cerca y cuando le hieren.
    ///  - Se puede abatir: sangra, chilla y cae de lado. "startDead" lo deja como cadaver (terror).
    /// </summary>
    public class FarmAnimal : MonoBehaviour
    {
        public enum Kind { Cow, Pig }

        public Kind kind = Kind.Cow;
        public float health = 80f;
        public float walkSpeed = 1.1f;
        public float fleeSpeed = 4.5f;
        [Tooltip("Radio alrededor de su sitio por el que deambula.")]
        public float homeRadius = 7f;
        [Tooltip("Aparece ya muerto, tumbado (cadaver).")]
        public bool startDead;
        [Tooltip("Marcar si el animal camina hacia atras (el modelo mira al reves).")]
        public bool flipForward;
        public Transform visual;

        public bool IsDead { get; private set; }

        private enum State { Idle, Graze, Walk, Flee }
        private State _state = State.Idle;
        private float _stateUntil;
        private NavMeshAgent _agent;
        private Vector3 _home;
        private Vector3 _visualPos;
        private Quaternion _visualRot;
        private float _phase;
        private float _graze;
        private float _nextSound;
        private Transform _player;

        private void Awake()
        {
            _home = transform.position;
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null)
            {
                if (flipForward) visual.localRotation = Quaternion.Euler(0f, 180f, 0f) * visual.localRotation;
                _visualPos = visual.localPosition;
                _visualRot = visual.localRotation;
            }
        }

        private void OnEnable() => NoiseSystem.OnNoise += OnNoise;
        private void OnDisable() => NoiseSystem.OnNoise -= OnNoise;

        private void Start()
        {
            if (PlayerHealth.Instance != null) _player = PlayerHealth.Instance.transform;
            _nextSound = Time.time + Random.Range(3f, 15f);
            if (startDead)
            {
                Die(true);
                return;
            }
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                _agent = gameObject.AddComponent<NavMeshAgent>();
                _agent.radius = kind == Kind.Cow ? 0.55f : 0.35f;
                _agent.height = kind == Kind.Cow ? 1.5f : 0.8f;
                _agent.speed = walkSpeed;
                _agent.angularSpeed = 200f;
                _agent.acceleration = 5f;
                _agent.stoppingDistance = 0.3f;
                _agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
                _agent.autoBraking = true;
            }
            SetState(State.Idle, Random.Range(1f, 4f));
        }

        // ------------------------------------------------------------ Comportamiento

        private void Update()
        {
            if (IsDead) return;
            float dt = Time.deltaTime;

            // Leon corriendo cerca (o pegado) lo asusta
            if (_player != null && _state != State.Flee)
            {
                float d = Vector3.Distance(_player.position, transform.position);
                var fps = FirstPersonController.Instance;
                bool sprinting = fps != null && fps.IsSprinting;
                if ((sprinting && d < 7f) || d < 1.8f) Flee(_player.position, 3f);
            }

            if (Time.time >= _stateUntil)
            {
                switch (_state)
                {
                    case State.Flee:
                    case State.Walk:
                        SetState(Random.value < 0.6f ? State.Graze : State.Idle, Random.Range(3f, 8f));
                        break;
                    default:
                        if (Random.value < 0.55f) WalkSomewhere();
                        else SetState(Random.value < 0.6f ? State.Graze : State.Idle, Random.Range(2f, 6f));
                        break;
                }
            }
            if (_state == State.Walk && _agent != null && !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
                SetState(State.Graze, Random.Range(3f, 7f));

            // Sonidos de vez en cuando
            if (Time.time >= _nextSound)
            {
                _nextSound = Time.time + Random.Range(kind == Kind.Cow ? 10f : 6f, kind == Kind.Cow ? 25f : 14f);
                AudioClip c = kind == Kind.Cow ? ProceduralAudio.Moo(Random.Range(0, 3)) : ProceduralAudio.Oink(Random.Range(0, 3));
                ProceduralAudio.PlayAt(c, transform.position + Vector3.up, kind == Kind.Cow ? 0.55f : 0.4f, 0.08f, kind == Kind.Cow ? 45f : 25f);
            }

            Animate(dt);
        }

        private void SetState(State s, float seconds)
        {
            _state = s;
            _stateUntil = Time.time + seconds;
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.speed = s == State.Flee ? fleeSpeed : walkSpeed;
                if (s == State.Idle || s == State.Graze) _agent.ResetPath();
            }
        }

        private void WalkSomewhere()
        {
            if (_agent == null || !_agent.isOnNavMesh) { SetState(State.Idle, 3f); return; }
            Vector2 r = Random.insideUnitCircle * homeRadius;
            Vector3 target = _home + new Vector3(r.x, 0f, r.y);
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
                SetState(State.Walk, 12f);
            }
            else SetState(State.Idle, 2f);
        }

        private void Flee(Vector3 from, float seconds)
        {
            if (IsDead) return;
            if (_state != State.Flee)
                ProceduralAudio.PlayAt(kind == Kind.Cow ? ProceduralAudio.Moo(2) : ProceduralAudio.Squeal(), transform.position + Vector3.up, 0.6f, 0.1f, 40f);
            SetState(State.Flee, seconds);
            if (_agent == null || !_agent.isOnNavMesh) return;
            Vector3 away = transform.position - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            Vector3 target = transform.position + away.normalized * 12f + Random.insideUnitSphere * 3f;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 5f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
        }

        private void OnNoise(Vector3 pos, float radius)
        {
            if (IsDead) return;
            // los animales oyen los disparos desde mas lejos que los aldeanos
            if ((pos - transform.position).sqrMagnitude <= radius * radius * 1.4f) Flee(pos, Random.Range(3f, 6f));
        }

        // ------------------------------------------------------------ Animacion por codigo

        private void Animate(float dt)
        {
            if (visual == null) return;
            float speed = _agent != null ? _agent.velocity.magnitude : 0f;
            float k = Mathf.Clamp01(speed / Mathf.Max(0.1f, walkSpeed));
            float freq = kind == Kind.Cow ? 1.6f : 3.2f;
            _phase += dt * freq * Mathf.PI * 2f * Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01(speed / fleeSpeed)) * (speed > 0.05f ? 1f : 0f);
            _graze = Mathf.MoveTowards(_graze, _state == State.Graze ? 1f : 0f, dt * 1.2f);

            float bob = Mathf.Abs(Mathf.Sin(_phase)) * (kind == Kind.Cow ? 0.035f : 0.05f) * k;
            float roll = Mathf.Sin(_phase) * 2.2f * k;
            float pitch = Mathf.Sin(_phase * 2f) * 1.2f * k + _graze * (kind == Kind.Cow ? 7f : 10f) + Mathf.Sin(Time.time * 0.8f) * 0.6f * _graze;
            visual.localPosition = _visualPos + new Vector3(0f, bob - _graze * 0.03f, 0f);
            visual.localRotation = Quaternion.Euler(pitch, 0f, roll) * _visualRot;
        }

        // ------------------------------------------------------------ Daño y muerte

        public void Hit(float damage, Vector3 point, Vector3 direction)
        {
            if (IsDead) return;
            health -= damage;
            if (FXManager.Instance != null) FXManager.Instance.FleshImpact(point, direction, damage > 60f);
            if (health <= 0f)
            {
                Die(false);
                return;
            }
            Flee(point - direction * 3f, Random.Range(4f, 7f));
        }

        private void Die(bool instant)
        {
            if (IsDead) return;
            IsDead = true;
            if (_agent != null) { _agent.enabled = false; }
            if (!instant)
                ProceduralAudio.PlayAt(ProceduralAudio.Squeal(), transform.position + Vector3.up, 0.7f, 0.05f, 40f);
            StartCoroutine(FallOver(instant ? 0f : 0.7f));
        }

        private IEnumerator FallOver(float seconds)
        {
            if (visual == null) yield break;
            float side = Random.value < 0.5f ? -1f : 1f;
            Quaternion from = visual.localRotation;
            Quaternion to = Quaternion.Euler(0f, 0f, 88f * side) * _visualRot;
            // al tumbarse, el lomo queda a la altura de medio cuerpo: bajar el modelo
            Vector3 fromPos = visual.localPosition;
            BoxCollider box = GetComponent<BoxCollider>();
            Vector3 size = box != null ? box.size : new Vector3(0.8f, 1.4f, 2.2f);
            Vector3 toPos = _visualPos + new Vector3(side * size.y * 0.3f, -size.y * 0.5f + size.x * 0.5f, 0f);
            if (box != null)
            {
                // el cuerpo tumbado ocupa otra forma
                box.size = new Vector3(size.y, size.x, size.z);
                box.center = new Vector3(side * size.y * 0.3f, size.x * 0.5f, box.center.z);
            }
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float e = t / seconds;
                e = e * e;
                visual.localRotation = Quaternion.Slerp(from, to, e);
                visual.localPosition = Vector3.Lerp(fromPos, toPos, e);
                yield return null;
            }
            visual.localRotation = to;
            visual.localPosition = toPos;
            if (FXManager.Instance != null)
                FXManager.Instance.SpawnDecal(transform.position + Vector3.up * 0.1f, Vector3.up, kind == Kind.Cow ? 2.2f : 1.3f);
        }
    }
}
