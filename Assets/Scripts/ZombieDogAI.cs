using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Gameplay.RE4;

namespace Gameplay
{
    public enum DogState
    {
        Idle,
        Chase,
        Circle,
        Crouch,
        Leap,
        Bite,
        Recover,
        Flinch,
        Dead
    }

    /// <summary>
    /// Perro infectado (los "Colmillos" de RE4): corre en manada, rodea a Leon, se agazapa y salta a por el
    /// o le muerde si esta pegado. El modelo no trae animaciones: el esqueleto creado por el menu RE4
    /// se anima por codigo (trote/galope, salto, mordisco, dano y muerte).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieDogAI : MonoBehaviour
    {
        public static readonly List<ZombieDogAI> All = new List<ZombieDogAI>();

        [Header("Movimiento")]
        public float walkSpeed = 1.6f;
        public float runSpeed = 7.2f;
        public float wanderRadius = 9f;

        [Header("Percepcion")]
        public float sightRange = 32f;
        public float closeSenseRange = 8f;
        public bool startAlerted;

        [Header("Ataque")]
        public float biteRange = 1.45f;
        public float biteDamage = 9f;
        public float pounceMinRange = 2.6f;
        public float pounceMaxRange = 6.5f;
        public float pounceDamage = 16f;
        public float pounceCooldown = 3.2f;
        public float crouchTime = 0.45f;
        public float leapTime = 0.5f;
        public float leapHeight = 0.55f;

        [Header("Esqueleto (lo rellena el menu RE4)")]
        public Transform model;
        public Transform hips, spine, chest, neck, head;
        public Transform[] upperLegs = new Transform[4]; // FL, FR, RL, RR
        public Transform[] lowerLegs = new Transform[4];

        public DogState State { get; private set; } = DogState.Idle;
        public bool IsDead => State == DogState.Dead;

        private NavMeshAgent _agent;
        private ZombieHealth _health;
        private Collider _body;
        private Transform _player;
        private AudioSource _voice;

        private float _stateTime;
        private float _nextAttack;
        private float _nextRepath;
        private float _nextWander;
        private float _nextGrowl;
        private bool _hitDone;
        private Vector3 _leapStart, _leapTarget;
        private Vector3 _circleCenterOffset;
        private float _circleDir = 1f;
        private float _circleUntil;
        private Vector3 _home;

        // Animacion procedimental
        private readonly Dictionary<Transform, Quaternion> _bind = new Dictionary<Transform, Quaternion>();
        private Vector3 _modelBasePos;
        private Quaternion _modelBaseRot;
        private float _phase;
        private float _moveBlend;
        private float _crouch, _leapPose, _bite, _hit, _death;
        private float _headYaw, _headPitch;
        private float _speedMul;

        private static readonly float[] LegPhaseGallop = { 0f, 0.12f, 0.52f, 0.62f };
        private static readonly float[] LegPhaseTrot = { 0f, 0.5f, 0.5f, 0f };

        // ================================================================ Ciclo de vida

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<ZombieHealth>();
            _body = GetComponent<Collider>();

            if (model == null && transform.childCount > 0) model = transform.GetChild(0);
            if (model != null)
            {
                _modelBasePos = model.localPosition;
                _modelBaseRot = model.localRotation;
            }
            foreach (var t in new[] { hips, spine, chest, neck, head }) Remember(t);
            foreach (var t in upperLegs) Remember(t);
            foreach (var t in lowerLegs) Remember(t);

            _voice = gameObject.AddComponent<AudioSource>();
            _voice.spatialBlend = 1f;
            _voice.rolloffMode = AudioRolloffMode.Linear;
            _voice.maxDistance = 30f;
            _voice.minDistance = 2f;
            _voice.playOnAwake = false;

            _speedMul = Random.Range(0.9f, 1.12f);
            _agent.speed = walkSpeed;
            _agent.angularSpeed = 540f;
            _agent.acceleration = 22f;
            _agent.stoppingDistance = biteRange * 0.7f;
            _agent.autoBraking = false;
            _agent.avoidancePriority = Random.Range(20, 50);
            _circleDir = Random.value < 0.5f ? -1f : 1f;
            _phase = Random.Range(0f, 10f);
        }

        private void Remember(Transform t)
        {
            if (t != null && !_bind.ContainsKey(t)) _bind[t] = t.localRotation;
        }

        private void OnEnable()
        {
            All.Add(this);
            NoiseSystem.OnNoise += OnNoise;
        }

        private void OnDisable()
        {
            All.Remove(this);
            NoiseSystem.OnNoise -= OnNoise;
        }

        private void Start()
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6f, NavMesh.AllAreas)) _agent.Warp(hit.position);
            _home = transform.position;
            if (PlayerHealth.Instance != null) _player = PlayerHealth.Instance.transform;
            _nextGrowl = Time.time + Random.Range(1f, 5f);
            if (startAlerted) Alert();
        }

        private void Enter(DogState s)
        {
            State = s;
            _stateTime = 0f;
            _hitDone = false;
        }

        // ================================================================ Logica

        private void Update()
        {
            if (State == DogState.Dead) return;
            if (_player == null && PlayerHealth.Instance != null) _player = PlayerHealth.Instance.transform;
            _stateTime += Time.deltaTime;

            bool playerDead = PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;
            if (playerDead && State != DogState.Idle) { Enter(DogState.Idle); SetStopped(false); }

            switch (State)
            {
                case DogState.Idle: UpdateIdle(); break;
                case DogState.Chase: UpdateChase(); break;
                case DogState.Circle: UpdateCircle(); break;
                case DogState.Crouch: UpdateCrouch(); break;
                case DogState.Leap: UpdateLeap(); break;
                case DogState.Bite: UpdateBite(); break;
                case DogState.Recover: if (_stateTime > 0.7f) Enter(DogState.Chase); break;
                case DogState.Flinch: if (_stateTime > 0.4f) Enter(DogState.Chase); break;
            }

            if (Time.time > _nextGrowl)
            {
                bool active = State != DogState.Idle;
                _nextGrowl = Time.time + (active ? Random.Range(1.5f, 4f) : Random.Range(5f, 11f));
                DogVoice("Perro_Grunido", ProceduralAudio.Growl(Random.Range(0, 6)), active ? 0.6f : 0.3f, 1.35f);
            }
        }

        private float DistToPlayer()
        {
            if (_player == null) return float.MaxValue;
            Vector3 d = _player.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        private void SetStopped(bool stop)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh) return;
            _agent.isStopped = stop;
            if (stop) _agent.velocity = Vector3.zero;
        }

        private void FacePlayer(float speed)
        {
            if (_player == null) return;
            Vector3 to = _player.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * speed);
        }

        private bool CanSeePlayer()
        {
            if (_player == null) return false;
            Vector3 eye = transform.position + Vector3.up * 0.8f;
            Vector3 target = _player.position + Vector3.up * 1.2f;
            Vector3 to = target - eye;
            float d = to.magnitude;
            float crouch = FirstPersonController.Instance != null ? FirstPersonController.Instance.CrouchBlend : 0f;
            if (d < closeSenseRange * Mathf.Lerp(1f, 0.6f, crouch)) return true; // el olfato del perro engaña menos
            if (d > sightRange * Mathf.Lerp(1f, 0.75f, crouch) || Vector3.Angle(transform.forward, to) > 100f) return false;
            if (Physics.Raycast(eye, to.normalized, out RaycastHit hit, d, RE4Layers.ShotMask, QueryTriggerInteraction.Ignore))
                return hit.transform == _player || hit.transform.IsChildOf(_player);
            return true;
        }

        private void OnNoise(Vector3 pos, float radius)
        {
            if (State == DogState.Idle && (pos - transform.position).sqrMagnitude <= radius * radius * 1.2f) Alert();
        }

        public void Alert()
        {
            if (State != DogState.Idle || IsDead) return;
            Enter(DogState.Chase);
            SetStopped(false);
            DogVoice("Perro_Ladrido", ProceduralAudio.Scream(), 0.55f, 1.6f);
            foreach (var other in All)
            {
                if (other != this && other.State == DogState.Idle && (other.transform.position - transform.position).sqrMagnitude < 30f * 30f) other.Alert();
            }
        }

        private void UpdateIdle()
        {
            bool playerDead = PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;
            if (!playerDead && CanSeePlayer()) { Alert(); return; }
            if (Time.time > _nextWander && _agent.isOnNavMesh)
            {
                _nextWander = Time.time + Random.Range(3f, 7f);
                Vector3 target = _home + Random.insideUnitSphere * wanderRadius;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    _agent.speed = walkSpeed * _speedMul;
                    _agent.SetDestination(hit.position);
                }
            }
        }

        private void UpdateChase()
        {
            if (_player == null || !_agent.isOnNavMesh) return;
            if (_agent.isStopped) _agent.isStopped = false;
            float dist = DistToPlayer();
            _agent.speed = (dist > 3f ? runSpeed : runSpeed * 0.6f) * _speedMul;

            if (Time.time > _nextRepath)
            {
                _nextRepath = Time.time + 0.2f;
                _agent.SetDestination(_player.position);
            }

            if (Time.time < _nextAttack) return;

            if (dist <= biteRange)
            {
                StartBite();
            }
            else if (dist >= pounceMinRange && dist <= pounceMaxRange && CanSeePlayer())
            {
                // A veces rodea a la presa antes de saltar (como en RE4)
                if (Random.value < 0.35f && Time.time > _circleUntil + 2f)
                {
                    Enter(DogState.Circle);
                    _circleUntil = Time.time + Random.Range(1.2f, 2.4f);
                    _circleDir = -_circleDir;
                }
                else StartCrouch();
            }
        }

        private void UpdateCircle()
        {
            if (_player == null || !_agent.isOnNavMesh) { Enter(DogState.Chase); return; }
            if (_agent.isStopped) _agent.isStopped = false;
            Vector3 from = transform.position - _player.position;
            from.y = 0f;
            if (from.sqrMagnitude < 0.01f) from = -transform.forward;
            float radius = Mathf.Clamp(from.magnitude, 3.5f, 5.5f);
            Vector3 dir = Quaternion.Euler(0f, 55f * _circleDir, 0f) * from.normalized;
            _agent.speed = runSpeed * 0.55f;
            if (Time.time > _nextRepath)
            {
                _nextRepath = Time.time + 0.15f;
                Vector3 target = _player.position + dir * radius;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas)) _agent.SetDestination(hit.position);
            }
            if (Time.time > _circleUntil || DistToPlayer() < biteRange) StartCrouch();
        }

        private void StartCrouch()
        {
            Enter(DogState.Crouch);
            SetStopped(true);
            DogVoice("Perro_Grunido", ProceduralAudio.Growl(Random.Range(0, 6)), 0.8f, 1.5f);
        }

        private void UpdateCrouch()
        {
            FacePlayer(10f);
            if (_stateTime < crouchTime) return;
            if (DistToPlayer() <= biteRange) { StartBite(); return; }

            Enter(DogState.Leap);
            _leapStart = transform.position;
            Vector3 to = _player != null ? _player.position - transform.position : transform.forward * 4f;
            to.y = 0f;
            float travel = Mathf.Min(to.magnitude + 0.4f, pounceMaxRange + 1f);
            _leapTarget = transform.position + to.normalized * travel;
            DogVoice("Perro_Ataque", ProceduralAudio.Scream(), 0.7f, 1.7f);
        }

        private void UpdateLeap()
        {
            float k = Mathf.Clamp01(_stateTime / leapTime);
            Vector3 desired = Vector3.Lerp(_leapStart, _leapTarget, k);
            Vector3 delta = desired - transform.position;
            delta.y = 0f;
            if (_agent.enabled && _agent.isOnNavMesh) _agent.Move(delta);

            if (!_hitDone && k > 0.35f && _player != null && DistToPlayer() < 1.35f)
            {
                _hitDone = true;
                HitPlayer(pounceDamage, 2.5f, 0.45f);
            }
            if (k >= 1f)
            {
                _nextAttack = Time.time + pounceCooldown * Random.Range(0.8f, 1.3f);
                Enter(DogState.Recover);
                SetStopped(false);
            }
        }

        private void StartBite()
        {
            Enter(DogState.Bite);
            SetStopped(true);
            DogVoice("Perro_Ataque", ProceduralAudio.Growl(Random.Range(0, 6)), 0.9f, 1.6f);
        }

        private void UpdateBite()
        {
            FacePlayer(12f);
            if (!_hitDone && _stateTime > 0.22f)
            {
                _hitDone = true;
                Vector3 to = _player != null ? _player.position - transform.position : Vector3.zero;
                to.y = 0f;
                if (_player != null && to.magnitude <= biteRange + 0.5f && Vector3.Angle(transform.forward, to) < 65f) HitPlayer(biteDamage, 1.5f, 0.2f);
                else ProceduralAudio.PlayAt(ProceduralAudio.Swoosh(), transform.position + Vector3.up * 0.6f, 0.4f);
            }
            if (_stateTime > 0.7f)
            {
                _nextAttack = Time.time + Random.Range(0.7f, 1.3f);
                Enter(DogState.Chase);
                SetStopped(false);
            }
        }

        private void HitPlayer(float damage, float push, float shake)
        {
            if (PlayerHealth.Instance == null) return;
            WeaponSystem ws = WeaponSystem.Instance;
            if (ws != null && ws.TryParry(transform.position, false, out bool perfect))
            {
                // el cuchillo lo aparta de un golpe
                _hit = 1f;
                Enter(DogState.Flinch);
                SetStopped(true);
                if (_agent.enabled && _agent.isOnNavMesh) _agent.Move(-transform.forward * (perfect ? 1.4f : 0.7f));
                _nextAttack = Time.time + pounceCooldown;
                return;
            }
            PlayerHealth.Instance.TakeDamage(damage, transform.position);
            ProceduralAudio.PlayAt(ProceduralAudio.Impact(1.2f), _player.position + Vector3.up, 0.8f);
            FirstPersonController fps = FirstPersonController.Instance;
            if (fps != null)
            {
                Vector3 dir = _player.position - transform.position;
                dir.y = 0f;
                fps.Push(dir.normalized * push);
                fps.AddShake(shake);
            }
        }

        // ================================================================ Dano

        public void OnDamaged(DamageInfo info)
        {
            if (IsDead) return;
            if (State == DogState.Idle) Alert();
            _hit = 1f;
            DogVoice("Perro_Dolor", ProceduralAudio.Scream(), 0.6f, 1.9f);

            // Un buen impacto lo saca del salto o de la carrera
            if (State != DogState.Leap && Random.value < info.staggerPower + 0.25f)
            {
                Enter(DogState.Flinch);
                SetStopped(true);
                if (_agent.enabled && _agent.isOnNavMesh)
                {
                    Vector3 push = new Vector3(info.direction.x, 0f, info.direction.z).normalized * (0.25f + info.knockback * 0.4f);
                    _agent.Move(push);
                }
            }
            else if (State == DogState.Leap && info.staggerPower > 0.4f)
            {
                Enter(DogState.Flinch);
                SetStopped(true);
            }
        }

        public void OnDeath(DamageInfo info)
        {
            Enter(DogState.Dead);
            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
            _agent.enabled = false;
            if (_body != null) _body.enabled = false;
            DogVoice("Perro_Muerte", ProceduralAudio.Growl(7), 0.8f, 1.5f);
            CorpseVanish.Begin(gameObject, 3f, 1.5f);
        }

        /// <summary>Sonido grabado del perro (Resources/RE4Audio/Perro, ElevenLabs) o el procedimental si falta.</summary>
        private void DogVoice(string key, AudioClip fallback, float volume, float fallbackPitch)
        {
            AudioClip c = BundledAudio.Pick("Perro", key);
            if (c != null) PlayVoice(c, Mathf.Min(1f, volume + 0.15f), 1f);
            else PlayVoice(fallback, volume, fallbackPitch);
        }

        private void PlayVoice(AudioClip clip, float volume, float pitch)
        {
            if (_voice == null || clip == null) return;
            _voice.pitch = pitch * Random.Range(0.92f, 1.08f);
            _voice.PlayOneShot(clip, volume);
        }

        // ================================================================ Animacion procedimental

        private void LateUpdate()
        {
            if (model == null) return;
            float dt = Time.deltaTime;

            float speed = _agent.enabled ? new Vector3(_agent.velocity.x, 0f, _agent.velocity.z).magnitude : 0f;
            if (State == DogState.Leap) speed = 0f;
            _moveBlend = Mathf.MoveTowards(_moveBlend, Mathf.Clamp01(speed / runSpeed), dt * 3f);
            float stride = Mathf.Lerp(0.75f, 1.7f, _moveBlend);
            _phase += speed / stride * dt;

            _crouch = Mathf.MoveTowards(_crouch, State == DogState.Crouch ? 1f : 0f, dt * (State == DogState.Crouch ? 3f : 6f));
            _leapPose = Mathf.MoveTowards(_leapPose, State == DogState.Leap ? 1f : 0f, dt * 8f);
            _bite = State == DogState.Bite ? Mathf.Clamp01(_stateTime / 0.7f) : 0f;
            _hit = Mathf.MoveTowards(_hit, 0f, dt * 4f);
            _death = Mathf.MoveTowards(_death, State == DogState.Dead ? 1f : 0f, dt * 2.2f);

            bool moving = speed > 0.15f && State != DogState.Dead;
            float gallop = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.7f, _moveBlend));
            float swing = moving ? Mathf.Lerp(20f, 42f, _moveBlend) : 0f;
            float bend = moving ? Mathf.Lerp(28f, 55f, _moveBlend) : 0f;
            float tau = Mathf.PI * 2f;

            // Patas
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                float offset = Mathf.Lerp(LegPhaseTrot[i], LegPhaseGallop[i], gallop);
                float p = (_phase + offset) * tau;
                float s = Mathf.Sin(p);
                float lift = Mathf.Max(0f, -Mathf.Cos(p));

                float upper = -s * swing;
                float lower = (front ? 1f : -1f) * lift * bend;

                // agazapado: patas traseras muy dobladas, delanteras adelantadas
                upper += _crouch * (front ? -18f : 26f);
                lower += _crouch * (front ? 22f : -48f);
                // en el aire: estiradas hacia delante y atras
                upper += _leapPose * (front ? -55f : 50f);
                lower += _leapPose * (front ? 10f : -10f);
                // muerte: patas rigidas
                upper = Mathf.Lerp(upper, front ? -25f : 25f, _death);
                lower = Mathf.Lerp(lower, Mathf.Sin(Time.time * 18f + i) * 6f * (1f - _death * 0.8f) * _death, _death);

                Rotate(upperLegs[i], upper, 0f, 0f);
                Rotate(lowerLegs[i], lower, 0f, 0f);
            }

            // Columna (flexion del galope)
            float flex = Mathf.Sin(_phase * tau * 1f) * 7f * gallop * (moving ? 1f : 0f);
            float breathe = Mathf.Sin(Time.time * (State == DogState.Idle ? 2f : 7f)) * 1.5f;
            Rotate(hips, -flex - _crouch * 10f + _leapPose * 8f, 0f, 0f);
            Rotate(spine, flex * 0.5f + breathe, 0f, Mathf.Sin(Time.time * 30f) * 8f * _hit);
            Rotate(chest, flex + _crouch * 14f - _leapPose * 10f - _hit * 12f, 0f, 0f);

            // Cabeza: mira a Leon y gruñe; mordisco = latigazo hacia delante
            float targetYaw = 0f, targetPitch = 0f;
            if (_player != null && State != DogState.Idle && State != DogState.Dead)
            {
                Vector3 to = _player.position + Vector3.up * 1.2f - (head != null ? head.position : transform.position);
                Vector3 local = transform.InverseTransformDirection(to);
                targetYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -50f, 50f);
                targetPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -40f, 25f);
            }
            _headYaw = Mathf.LerpAngle(_headYaw, targetYaw, dt * 8f);
            _headPitch = Mathf.LerpAngle(_headPitch, targetPitch, dt * 8f);
            float snap = State == DogState.Bite ? Mathf.Sin(_bite * Mathf.PI * 3f) * 28f * (1f - _bite) : 0f;
            float shake = State == DogState.Bite ? Mathf.Sin(Time.time * 45f) * 14f * (1f - _bite) : 0f;
            float snarl = State != DogState.Idle && State != DogState.Dead ? Mathf.PerlinNoise(Time.time * 6f, 3.1f) * 6f - 3f : 0f;
            float headBob = moving ? Mathf.Sin(_phase * tau * 2f) * 4f * _moveBlend : 0f;
            Rotate(neck, _headPitch * 0.45f + snap * 0.6f + headBob - _crouch * 8f + _death * 35f, _headYaw * 0.5f, 0f);
            Rotate(head, _headPitch * 0.55f + snap * 0.4f + snarl - _leapPose * 15f, _headYaw * 0.5f, shake);

            // Cuerpo: rebote al correr, agacharse, arco del salto y caida de lado al morir
            float bob = moving ? Mathf.Abs(Mathf.Sin(_phase * tau)) * Mathf.Lerp(0.015f, 0.06f, _moveBlend) : 0f;
            float arc = State == DogState.Leap ? Mathf.Sin(Mathf.Clamp01(_stateTime / leapTime) * Mathf.PI) * leapHeight : 0f;
            float lean = State == DogState.Leap ? Mathf.Lerp(-18f, 14f, Mathf.Clamp01(_stateTime / leapTime)) : 0f;
            model.localPosition = _modelBasePos + new Vector3(0f, bob - _crouch * 0.1f + arc + _death * 0.12f, 0f);
            model.localRotation = _modelBaseRot * Quaternion.Euler(lean, 0f, Mathf.SmoothStep(0f, 88f, _death));
        }

        private void Rotate(Transform bone, float x, float y, float z)
        {
            if (bone == null || !_bind.TryGetValue(bone, out Quaternion b)) return;
            bone.localRotation = b * Quaternion.Euler(x, y, z);
        }

        /// <summary>Indica si el perro zombi ya descubrió al jugador y está en combate activo.</summary>
        public bool IsAggressive => !IsDead && State != DogState.Idle;
    }
}
