using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Gameplay.RE4;

namespace Gameplay
{
    public enum GanadoState
    {
        Idle,
        Alert,
        Chase,
        Attack,
        Grab,
        Flinch,
        Stagger,
        Down,
        GettingUp,
        Dead
    }

    /// <summary>
    /// Aldeano infectado ("Ganado") al estilo Resident Evil 4.
    /// Usa NavMeshAgent (nunca atraviesa el suelo), percibe por vista y ruido, alerta a los demas,
    /// ataca con golpes temporizados, agarra al jugador, se aturde con disparos a la cabeza/piernas
    /// (momento para la patada) y puede ser derribado.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieAI : MonoBehaviour
    {
        public static readonly List<ZombieAI> All = new List<ZombieAI>();
        private static ZombieAI _currentGrabber;

        [Header("Movimiento")]
        public float walkSpeed = 1.35f;
        public float runSpeed = 3.8f;
        public bool isRunner;
        public float wanderRadius = 7f;

        [Header("Percepcion")]
        public float sightRange = 28f;
        public float sightAngle = 75f;
        public float closeSenseRange = 6f;
        public bool startAlerted;

        [Header("Ataque")]
        public float attackRange = 1.55f;
        public float attackCooldown = 1.9f;
        public float attackDamage = 12f;
        public float attackHitTime = 0.55f;
        public float attackDuration = 1.4f;
        [Range(0f, 1f)] public float grabChance = 0.3f;

        [Header("Reacciones")]
        public float staggerDuration = 2.3f;
        public float flinchDuration = 0.55f;
        public float downDuration = 2.4f;

        [Header("Armas arrojadizas (hoz, hacha, dinamita)")]
        public bool thrower;
        public bool dynamite;
        public float throwMinRange = 5f;
        public float throwMaxRange = 17f;
        public float throwCooldown = 4.5f;
        public float throwDamage = 14f;

        [Header("Como en RE4")]
        [Tooltip("Probabilidad de apartarse de un salto cuando Leon les apunta a la cabeza.")]
        [Range(0f, 1f)] public float dodgeChance = 0.35f;
        public bool carriesTorch;
        public bool meleeWeapon;
        /// <summary>Maximo de aldeanos golpeando a la vez; el resto rodea a Leon esperando su turno.</summary>
        public const int MaxAttackers = 3;

        [Header("Enemigo pesado (Tyrant)")]
        [Tooltip("No retrocede con disparos normales, no se le puede patear ni derribar salvo con explosiones.")]
        public bool heavy;
        [Tooltip("Dano acumulado necesario para que el enemigo pesado se tambalee.")]
        public float heavyStaggerDamage = 320f;
        private float _heavyDamage;

        public GanadoState State { get; private set; } = GanadoState.Idle;
        public bool IsDead => State == GanadoState.Dead;
        public bool IsDowned => State == GanadoState.Down;
        public bool CanBeKicked => State == GanadoState.Stagger && !heavy;
        public bool CanBeStealthKilled => State == GanadoState.Idle && !heavy && !IsDead;

        private NavMeshAgent _agent;
        private Animator _anim;
        private ZombieHealth _health;
        private CapsuleCollider _bodyCollider;
        private Transform _player;
        private AudioSource _voice;
        private HashSet<int> _params;

        private Vector3 _home;
        private float _stateTime;
        private float _nextAttackTime;
        private float _nextRepath;
        private float _nextWander;
        private float _nextGrowl;
        private bool _attackHitDone;
        private Vector3 _knockVelocity;
        private float _speedMul = 1f;
        private static int _attackers;
        private bool _hasToken;
        private bool _throwing;
        private float _nextThrow, _nextDodge;
        private Transform _handItem;
        private string _barkVoice;
        private bool _female;
        private float _voicePitch = 1f;
        private AudioSource _speech;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int AttackHash = Animator.StringToHash("Attack");
        private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int StaggerHash = Animator.StringToHash("Stagger");
        private static readonly int DownHash = Animator.StringToHash("Down");
        private static readonly int GrabHash = Animator.StringToHash("Grab");
        private static readonly int RoarHash = Animator.StringToHash("Roar");
        private static readonly int DeadHash = Animator.StringToHash("Dead");
        private static readonly int DeathIndexHash = Animator.StringToHash("DeathIndex");

        // ================================================================ Ciclo de vida

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _anim = GetComponentInChildren<Animator>();
            _health = GetComponent<ZombieHealth>();
            _bodyCollider = GetComponent<CapsuleCollider>();

            if (_anim != null)
            {
                _anim.applyRootMotion = false; // la causa de que se hundieran en el suelo
                // Siempre animar: con CullUpdateTransforms, si Unity calculaba mal los limites del modelo
                // (escalado al construir el prefab) creia que no se veia y el aldeano se deslizaba congelado
                _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _anim.enabled = true;
                ApplyAnimVariety();
                _params = new HashSet<int>();
                RefreshParams();
                _model = _anim.transform != transform ? _anim.transform : null;
                if (_model != null) _modelBaseY = _model.localPosition.y;
            }

            // Ya no se usa CharacterController: el NavMeshAgent mantiene al aldeano pegado al suelo
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            _voice = gameObject.AddComponent<AudioSource>();
            _voice.spatialBlend = 1f;
            _voice.rolloffMode = AudioRolloffMode.Linear;
            _voice.maxDistance = 30f;
            _voice.minDistance = 2f;
            _voice.playOnAwake = false;

            // Voz hablada de infectado: rasposa, algo distorsionada y sin brillo
            var speech = new GameObject("VozGanado");
            speech.transform.SetParent(transform, false);
            speech.transform.localPosition = Vector3.up * 1.6f;
            _speech = speech.AddComponent<AudioSource>();
            _speech.spatialBlend = 1f;
            _speech.rolloffMode = AudioRolloffMode.Linear;
            _speech.maxDistance = 32f;
            _speech.minDistance = 2.5f;
            _speech.playOnAwake = false;
            speech.AddComponent<AudioDistortionFilter>().distortionLevel = 0.55f;
            speech.AddComponent<AudioLowPassFilter>().cutoffFrequency = 4200f;

            // Voz de los gritos: mujer si el modelo es femenino
            bool female = false;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh != null && r.sharedMesh.name.ToLowerInvariant().Contains("female")) { female = true; break; }
            }
            _female = female;
            _barkVoice = female ? NpcVoice.VillagerFemale[0] : NpcVoice.VillagerMale[0];
            _voicePitch = female ? Random.Range(0.92f, 1.05f) : Random.Range(0.84f, 1.0f);

            _speedMul = Random.Range(0.85f, 1.15f);
            _agent.speed = walkSpeed * _speedMul;
            _agent.angularSpeed = 360f;
            _agent.acceleration = 10f;
            _agent.stoppingDistance = attackRange * 0.8f;
            _agent.autoBraking = true;
            _agent.avoidancePriority = Random.Range(30, 70);
            // un poco mas anchos y con mejor esquiva: no se apilan unos dentro de otros
            _agent.radius = Mathf.Max(_agent.radius, 0.42f);
            _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            // cada aldeano rodea a Leon por su propio lado y a su propia distancia
            _flankAngle = Random.Range(-70f, 70f);
            _flankRadius = Random.Range(2.2f, 4.5f);
        }

        // ================================================================ Variedad de animaciones

        private Transform _model;
        private float _modelBaseY;
        private float _groundOffset;
        private float _flankAngle, _flankRadius;
        private float _getUpTime = 1.1f;
        private static readonly int StandUpSpeedHash = Animator.StringToHash("StandUpSpeed");
        private const float StandUpSeconds = 1.6f;

        /// <summary>
        /// Cada aldeano escoge su propia combinacion de animaciones (reposo, andar, correr, golpes, reaccion,
        /// grito, levantarse) entre las originales y las del paquete de zombis, y una velocidad algo distinta.
        /// </summary>
        private void ApplyAnimVariety()
        {
            ZombieAnimLibrary lib = ZombieAnimLibrary.Current;
            RuntimeAnimatorController baseCtrl = _anim.runtimeAnimatorController;
            if (lib == null || baseCtrl == null || heavy) return;
            if (baseCtrl is AnimatorOverrideController) return; // ya preparado

            var over = new AnimatorOverrideController(baseCtrl);
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(over.overridesCount);
            over.GetOverrides(pairs);
            AnimationClip standUp = null;
            for (int i = 0; i < pairs.Count; i++)
            {
                AnimationClip key = pairs[i].Key;
                if (key == null) continue;
                string n = key.name;
                AnimationClip pick = null;
                if (n.StartsWith("idle")) pick = ZombieAnimLibrary.Pick(lib.idles, key, 0.25f);
                else if (n == "walk") pick = ZombieAnimLibrary.Pick(lib.walks, key, 0.3f);
                else if (n == "run") pick = ZombieAnimLibrary.Pick(lib.runs, key, 0.5f);
                else if (n.StartsWith("atack")) pick = ZombieAnimLibrary.Pick(lib.attacks, key, 0.35f);
                else if (n == "gethit") pick = ZombieAnimLibrary.Pick(lib.hits, key, 0.35f);
                else if (n == "roar") pick = ZombieAnimLibrary.Pick(lib.screams, key, 0.4f);
                else if (n == ZombieAnimLibrary.StandUpBaseClip) standUp = pick = ZombieAnimLibrary.Pick(lib.standUps, key, 0.3f);
                if (pick != null && pick != key) pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(key, pick);
                else if (n == ZombieAnimLibrary.StandUpBaseClip) standUp = key;
            }
            over.ApplyOverrides(pairs);
            _anim.runtimeAnimatorController = over;
            // pequeñas diferencias de ritmo entre aldeanos
            _anim.speed = Random.Range(0.9f, 1.1f);
            if (standUp != null)
            {
                _getUpTime = StandUpSeconds;
                _standUpSpeed = standUp.length / StandUpSeconds; // se aplica en Start (el Animator aun no esta listo)
            }
        }

        private float _standUpSpeed = -1f;

        // ================================================================ Pies en el suelo

        /// <summary>
        /// La NavMesh queda unos centimetros por encima del terreno real (y en las cuestas no lo sigue bien):
        /// por eso parecian levitar. Se baja o sube solo el modelo hasta el suelo real con un rayo.
        /// </summary>
        private void LateUpdate()
        {
            // muerto: el cuerpo se queda con el ultimo ajuste (CorpseVanish lo hunde despues)
            if (_model == null || State == GanadoState.Dead) return;
            float target = 0f;
            Vector3 origin = transform.position + Vector3.up * 1.0f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2.2f, GroundMask, QueryTriggerInteraction.Ignore))
                target = Mathf.Clamp(hit.point.y - transform.position.y, -0.45f, 0.35f);
            _groundOffset = Mathf.Lerp(_groundOffset, target, Time.deltaTime * 12f);
            Vector3 p = _model.localPosition;
            p.y = _modelBaseY + _groundOffset / Mathf.Max(0.0001f, transform.lossyScale.y);
            _model.localPosition = p;
        }

        private static int _groundMask = -1;

        /// <summary>Solo escenario: sin aldeanos, zonas de impacto, arma de Leon ni Ignore Raycast.</summary>
        private static int GroundMask
        {
            get
            {
                if (_groundMask != -1) return _groundMask;
                int m = RE4Layers.ShotMask;
                if (RE4Layers.Hitbox >= 0) m &= ~(1 << RE4Layers.Hitbox);
                int player = LayerMask.NameToLayer("Player");
                if (player >= 0) m &= ~(1 << player);
                _groundMask = m;
                return m;
            }
        }

        private void OnEnable()
        {
            All.Add(this);
            NoiseSystem.OnNoise += OnNoise;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _attackers = 0;

        private void ReleaseToken()
        {
            if (!_hasToken) return;
            _hasToken = false;
            _attackers = Mathf.Max(0, _attackers - 1);
        }

        private void OnDisable()
        {
            ReleaseToken();
            All.Remove(this);
            NoiseSystem.OnNoise -= OnNoise;
            if (_currentGrabber == this) _currentGrabber = null;
        }

        private void Start()
        {
            SnapToNavMesh();
            _home = transform.position;
            FindPlayer();
            _nextGrowl = Time.time + Random.Range(2f, 8f);
            if (startAlerted) BecomeAlerted(false);
            EquipHandItems();
            _nextThrow = Time.time + Random.Range(1.5f, 4f);
            if (_standUpSpeed > 0f && Has(StandUpSpeedHash)) _anim.SetFloat(StandUpSpeedHash, _standUpSpeed);
        }

        private void FindPlayer()
        {
            if (PlayerHealth.Instance != null) _player = PlayerHealth.Instance.transform;
            else
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p == null) p = GameObject.Find("Leon");
                if (p != null) _player = p.transform;
            }
        }

        private void SnapToNavMesh()
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
            {
                if (_agent.isOnNavMesh) _agent.Warp(hit.position);
                else
                {
                    transform.position = hit.position;
                    _agent.enabled = false;
                    _agent.enabled = true;
                }
            }
        }

        // ================================================================ Animator helpers

        private bool Has(int hash)
        {
            if (_anim == null || _params == null) return false;
            if (_params.Count == 0) RefreshParams();
            return _params.Contains(hash);
        }

        /// <summary>
        /// Lee los parametros del Animator. Al instanciar el zombi el Animator a veces aun no esta iniciado
        /// ("Animator is not playing an AnimatorController"): la lista salia vacia, la IA creia que no habia
        /// animaciones y el aldeano se deslizaba sin animar. Ahora se fuerza el inicio y se reintenta despues.
        /// </summary>
        private void RefreshParams()
        {
            if (_anim.runtimeAnimatorController == null) return;
            if (!_anim.isInitialized) _anim.Rebind();
            if (!_anim.isInitialized) return;
            foreach (var p in _anim.parameters) _params.Add(p.nameHash);
        }
        private void SetFloat(int h, float v) { if (Has(h)) _anim.SetFloat(h, v, 0.12f, Time.deltaTime); }
        private void SetBool(int h, bool v) { if (Has(h)) _anim.SetBool(h, v); }
        private void SetInt(int h, int v) { if (Has(h)) _anim.SetInteger(h, v); }
        private void Trigger(int h) { if (Has(h)) _anim.SetTrigger(h); }

        private void Enter(GanadoState s)
        {
            State = s;
            _stateTime = 0f;
        }

        // ================================================================ Update

        private void Update()
        {
            if (State == GanadoState.Dead) return;
            if (_player == null) FindPlayer();

            _stateTime += Time.deltaTime;

            // Seguridad: si algo lo saca de la malla, devolverlo al suelo
            if (transform.position.y < -20f || (_agent.enabled && !_agent.isOnNavMesh)) SnapToNavMesh();

            bool playerDead = PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;

            switch (State)
            {
                case GanadoState.Idle: UpdateIdle(); break;
                case GanadoState.Alert: UpdateAlert(); break;
                case GanadoState.Chase: if (playerDead) GoIdle(); else UpdateChase(); break;
                case GanadoState.Attack: UpdateAttack(); break;
                case GanadoState.Grab: UpdateGrab(); break;
                case GanadoState.Flinch: if (_stateTime > flinchDuration) Resume(); break;
                case GanadoState.Stagger: UpdateStagger(); break;
                case GanadoState.Down: UpdateDown(); break;
                case GanadoState.GettingUp: if (_stateTime > _getUpTime) Resume(); break;
            }

            // Locomocion
            float v = _agent.enabled && _agent.isOnNavMesh ? _agent.velocity.magnitude : 0f;
            // Valor continuo (0 quieto, 1 caminar, 2 correr): la animacion acompaña a la velocidad real y los pies no patinan
            float speedParam = v < 0.08f ? 0f
                : v <= walkSpeed ? v / walkSpeed
                : 1f + Mathf.Clamp01((v - walkSpeed) / Mathf.Max(0.1f, runSpeed - walkSpeed));
            SetFloat(SpeedHash, speedParam);

            Growl();
        }

        private void GoIdle()
        {
            Enter(GanadoState.Idle);
            StopAgent(false);
            _agent.speed = walkSpeed * _speedMul;
        }

        private void Resume()
        {
            ReleaseToken();
            _throwing = false;
            if (_handItem != null && !_handItem.gameObject.activeSelf) StartCoroutine(ShowHandItem(1.5f));
            if (Has(HitHash)) _anim.ResetTrigger(HitHash);
            if (Has(AttackHash)) _anim.ResetTrigger(AttackHash);
            if (Has(RoarHash)) _anim.ResetTrigger(RoarHash);
            SetBool(StaggerHash, false);
            SetBool(DownHash, false);
            SetBool(GrabHash, false);
            Enter(GanadoState.Chase);
            StopAgent(false);
        }

        private void StopAgent(bool stop)
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

        // ---------------------------------------------------------------- Estados

        private void UpdateIdle()
        {
            if (CanSeePlayer())
            {
                BecomeAlerted(true);
                return;
            }
            if (Time.time > _nextWander && _agent.isOnNavMesh)
            {
                _nextWander = Time.time + Random.Range(4f, 9f);
                Vector3 target = _home + Random.insideUnitSphere * wanderRadius;
                if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    _agent.speed = walkSpeed * 0.6f * _speedMul;
                    _agent.SetDestination(hit.position);
                }
            }
        }

        private bool CanSeePlayer()
        {
            if (_player == null) return false;
            Vector3 eye = transform.position + Vector3.up * 1.6f;
            Vector3 target = _player.position + Vector3.up * 1.5f;
            Vector3 to = target - eye;
            float d = to.magnitude;
            float angle = Vector3.Angle(transform.forward, to);
            // De cerca lo notan, salvo que Leon se acerque despacio por la espalda (sigilo)
            bool sneaking = FirstPersonController.Instance != null && !FirstPersonController.Instance.IsSprinting;
            if (d < closeSenseRange && (angle < 110f || !sneaking || d < 0.9f)) return true;
            if (d > sightRange) return false;
            if (angle > sightAngle) return false;
            if (Physics.Raycast(eye, to.normalized, out RaycastHit hit, d, RE4Layers.ShotMask, QueryTriggerInteraction.Ignore))
            {
                return hit.transform == _player || hit.transform.IsChildOf(_player);
            }
            return true;
        }

        private void OnNoise(Vector3 pos, float radius)
        {
            if (State != GanadoState.Idle) return;
            if ((pos - transform.position).sqrMagnitude <= radius * radius) BecomeAlerted(true);
        }

        /// <summary>Se alerta, ruge y avisa a los aldeanos cercanos.</summary>
        public void BecomeAlerted(bool roar)
        {
            if (State != GanadoState.Idle) return;
            if (roar && Random.value < 0.6f)
            {
                Enter(GanadoState.Alert);
                StopAgent(true);
                Trigger(RoarHash);
                if (!Bark(VillagerBarks.Alert)) Ganado(GanadoSound.Scream, 0.85f, ProceduralAudio.Scream());
            }
            else
            {
                Enter(GanadoState.Chase);
                StopAgent(false);
            }

            foreach (var other in All)
            {
                if (other == this || other.State != GanadoState.Idle) continue;
                if ((other.transform.position - transform.position).sqrMagnitude < 22f * 22f)
                {
                    other.StartCoroutine(other.DelayedAlert(Random.Range(0.3f, 1.2f)));
                }
            }
        }

        private IEnumerator DelayedAlert(float delay)
        {
            yield return new WaitForSeconds(delay);
            BecomeAlerted(Random.value < 0.3f);
        }

        private void UpdateAlert()
        {
            FacePlayer(5f);
            if (_stateTime > 1.3f) Resume();
        }

        private void UpdateChase()
        {
            if (_player == null || !_agent.isOnNavMesh) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            bool run = isRunner ? dist > 3f : dist > 14f;
            _agent.speed = (run ? runSpeed : walkSpeed) * _speedMul;

            if (Time.time > _nextRepath)
            {
                _nextRepath = Time.time + Random.Range(0.2f, 0.35f);
                _agent.SetDestination(ChaseTarget(dist));
            }

            if (dist < attackRange + 0.4f) FacePlayer(6f);

            TryDodge(dist);

            // Lanzadores: hoz, hacha o dinamita desde lejos
            if (thrower && !heavy && Time.time >= _nextThrow && dist >= throwMinRange && dist <= throwMaxRange && CanSeePlayer())
            {
                StartThrow();
                return;
            }

            if (dist <= attackRange && Time.time >= _nextAttackTime)
            {
                Vector3 to = _player.position - transform.position;
                to.y = 0f;
                if (Vector3.Angle(transform.forward, to) < 50f) StartAttack();
            }
        }

        /// <summary>
        /// A donde camina el aldeano. Lejos: hacia un punto a su lado de Leon (cada uno con su angulo), asi
        /// llegan abiertos en abanico y no en fila. Cerca: solo los que pueden golpear van a por el; el resto
        /// espera en corro a su distancia. Ademas se separan de los vecinos para no amontonarse.
        /// </summary>
        private Vector3 ChaseTarget(float dist)
        {
            Vector3 player = _player.position;
            Vector3 away = transform.position - player;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -_player.forward;
            away.Normalize();

            bool canStrike = heavy || _hasToken || _attackers < MaxAttackers;
            Vector3 target;
            if (dist > 7f)
            {
                Vector3 side = Quaternion.Euler(0f, _flankAngle, 0f) * away;
                target = player + side * _flankRadius;
            }
            else if (canStrike) target = player;
            else
            {
                // esperar turno rodeando a Leon (gira despacio por su lado)
                Vector3 side = Quaternion.Euler(0f, _flankAngle * 0.5f + Mathf.Sin(Time.time * 0.4f + _flankAngle) * 25f, 0f) * away;
                target = player + side * Mathf.Max(2.6f, _flankRadius);
            }

            // separacion de los vecinos
            Vector3 push = Vector3.zero;
            foreach (var other in All)
            {
                if (other == this || other == null || other.IsDead) continue;
                Vector3 d = transform.position - other.transform.position;
                d.y = 0f;
                float m = d.magnitude;
                if (m > 0.01f && m < 1.6f) push += d / m * (1.6f - m);
            }
            target += Vector3.ClampMagnitude(push, 1.5f);

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return hit.position;
            return player;
        }

        private void StartAttack()
        {
            bool canGrab = _currentGrabber == null && PlayerHealth.Instance != null && !PlayerHealth.Instance.IsGrabbed
                           && Random.value < grabChance;
            if (canGrab)
            {
                StartGrab();
                return;
            }

            // Turnos: si ya hay tres pegando, esperar rodeando a Leon
            if (!heavy)
            {
                if (_attackers >= MaxAttackers)
                {
                    _nextAttackTime = Time.time + Random.Range(0.4f, 0.9f);
                    return;
                }
                _attackers++;
                _hasToken = true;
            }

            Enter(GanadoState.Attack);
            StopAgent(true);
            _attackHitDone = false;
            SetInt(AttackIndexHash, Random.Range(0, 4));
            Trigger(AttackHash);
            if (Random.value > 0.5f || !Bark(VillagerBarks.Attack)) Ganado(GanadoSound.Scream, 0.9f, ProceduralAudio.Growl(Random.Range(0, 6)));
        }

        private void StartThrow()
        {
            Enter(GanadoState.Attack);
            StopAgent(true);
            _throwing = true;
            _attackHitDone = false;
            SetInt(AttackIndexHash, Random.Range(0, 4));
            Trigger(AttackHash);
            if (!Bark(VillagerBarks.Attack)) Ganado(GanadoSound.Scream, 0.9f, ProceduralAudio.Scream());
        }

        private void UpdateThrow()
        {
            FacePlayer(7f);
            if (!_attackHitDone && _stateTime >= 0.55f)
            {
                _attackHitDone = true;
                ThrowKind kind = dynamite && Random.value < 0.6f ? ThrowKind.Dynamite : (Random.value < 0.5f ? ThrowKind.Sickle : ThrowKind.Axe);
                Vector3 from = _handItem != null ? _handItem.position : transform.position + Vector3.up * 1.7f + transform.right * 0.3f;
                ThrownWeapon.Throw(kind, from, throwDamage);
                if (_handItem != null) _handItem.gameObject.SetActive(false);
            }
            if (_stateTime >= 1.1f)
            {
                _throwing = false;
                _nextThrow = Time.time + throwCooldown * Random.Range(0.8f, 1.4f);
                if (_handItem != null) StartCoroutine(ShowHandItem(1.5f));
                Resume();
            }
        }

        private IEnumerator ShowHandItem(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_handItem != null && !IsDead) _handItem.gameObject.SetActive(true);
        }

        /// <summary>Como en RE4: si Leon les apunta a la cabeza, a veces se apartan de un salto.</summary>
        private void TryDodge(float dist)
        {
            if (heavy || Time.time < _nextDodge || dist < 3.5f || dist > 25f) return;
            WeaponSystem ws = WeaponSystem.Instance;
            Camera cam = Camera.main;
            if (ws == null || cam == null || !ws.IsAiming) return;
            Vector3 head = transform.position + Vector3.up * 1.65f;
            if (Vector3.Angle(cam.transform.forward, head - cam.transform.position) > 3.5f) return;
            if (Random.value < dodgeChance)
            {
                _nextDodge = Time.time + Random.Range(3f, 5f);
                float side = Random.value < 0.5f ? -1f : 1f;
                StartCoroutine(Shove(transform.right * side * 2.4f, 0.35f));
            }
            else _nextDodge = Time.time + 1.4f;
        }

        // ---------------------------------------------------------------- Objetos en la mano

        private void EquipHandItems()
        {
            if (heavy || _anim == null || !_anim.isHuman) return;
            Transform right = _anim.GetBoneTransform(HumanBodyBones.RightHand);
            Transform left = _anim.GetBoneTransform(HumanBodyBones.LeftHand);
            if (thrower || meleeWeapon)
            {
                ThrowKind k = thrower && dynamite ? ThrowKind.Dynamite : (Random.value < 0.5f ? ThrowKind.Sickle : ThrowKind.Axe);
                if (right != null) _handItem = Attach(ThrownWeapon.BuildModel(k).transform, right, new Vector3(0f, 0.05f, 0.03f), new Vector3(0f, 0f, -90f));
                if (meleeWeapon) attackDamage += 4f;
            }
            if (carriesTorch && left != null)
            {
                // antorcha encendida: da miedo y ayuda a verlos de noche
                GameObject torch = new GameObject("Antorcha");
                GameObject stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(stick.GetComponent<Collider>());
                stick.transform.SetParent(torch.transform, false);
                stick.transform.localScale = new Vector3(0.04f, 0.3f, 0.04f);
                stick.GetComponent<Renderer>().material.color = new Color(0.25f, 0.16f, 0.08f);
                GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(flame.GetComponent<Collider>());
                flame.transform.SetParent(torch.transform, false);
                flame.transform.localPosition = new Vector3(0f, 0.36f, 0f);
                flame.transform.localScale = new Vector3(0.12f, 0.18f, 0.12f);
                Renderer fr = flame.GetComponent<Renderer>();
                fr.material.color = new Color(1f, 0.6f, 0.2f);
                fr.material.EnableKeyword("_EMISSION");
                fr.material.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.15f) * 5f);
                Light l = flame.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.58f, 0.25f);
                l.range = 7f;
                l.intensity = 1.8f;
                l.shadows = LightShadows.None;
                flame.AddComponent<FlickerLight>().baseIntensity = 1.8f;
                Attach(torch.transform, left, new Vector3(0f, 0.04f, 0.02f), new Vector3(0f, 0f, 90f));
            }
        }

        private static Transform Attach(Transform item, Transform bone, Vector3 localPos, Vector3 euler)
        {
            float s = 1f / Mathf.Max(0.0001f, bone.lossyScale.x);
            item.SetParent(bone, false);
            item.localPosition = localPos * s;
            item.localRotation = Quaternion.Euler(euler);
            item.localScale = Vector3.one * s;
            return item;
        }

        private void UpdateAttack()
        {
            if (_throwing) { UpdateThrow(); return; }
            FacePlayer(4f);
            if (!_attackHitDone && _stateTime >= attackHitTime)
            {
                _attackHitDone = true;
                if (_player != null && PlayerHealth.Instance != null)
                {
                    Vector3 to = _player.position - transform.position;
                    to.y = 0f;
                    bool inRange = to.magnitude <= attackRange + 0.55f;
                    bool inFront = Vector3.Angle(transform.forward, to) < 70f;
                    WeaponSystem ws = WeaponSystem.Instance;
                    if (inRange && inFront && ws != null && ws.TryParry(transform.position, heavy, out bool perfect))
                    {
                        // Parry: el golpe rebota en el cuchillo
                        _nextAttackTime = Time.time + attackCooldown;
                        if (!heavy || perfect)
                        {
                            EnterStagger();
                            StartCoroutine(Shove(-transform.forward * (perfect ? 1.6f : 0.8f), 0.2f));
                        }
                        return;
                    }
                    if (inRange && inFront)
                    {
                        PlayerHealth.Instance.TakeDamage(attackDamage, transform.position);
                        ProceduralAudio.PlayAt(ProceduralAudio.Impact(1.5f), _player.position + Vector3.up, 0.8f);
                    }
                    else
                    {
                        ProceduralAudio.PlayAt(ProceduralAudio.Swoosh(), transform.position + Vector3.up, 0.5f);
                    }
                }
            }
            if (_stateTime >= attackDuration)
            {
                _nextAttackTime = Time.time + attackCooldown * Random.Range(0.8f, 1.3f);
                Resume();
            }
        }

        // ---------------------------------------------------------------- Agarre

        private void StartGrab()
        {
            if (!PlayerHealth.Instance.TryBeginGrab(this)) return;
            _currentGrabber = this;
            Enter(GanadoState.Grab);
            StopAgent(true);
            SetBool(GrabHash, true);
            Ganado(GanadoSound.Scream, 1f, ProceduralAudio.Scream());
        }

        private void UpdateGrab()
        {
            if (_player == null) return;
            // Pegarse al jugador
            Vector3 to = transform.position - _player.position;
            to.y = 0f;
            Vector3 desired = _player.position + to.normalized * 0.95f;
            if (_agent.isOnNavMesh) _agent.Move((desired - transform.position) * Mathf.Clamp01(Time.deltaTime * 8f));
            FacePlayer(10f);

            if (_stateTime > 0.6f && Random.value < Time.deltaTime * 2f)
            {
                Ganado(GanadoSound.Grunt, 0.8f, ProceduralAudio.Growl(Random.Range(0, 6)));
            }
        }

        /// <summary>Llamado por PlayerHealth al terminar el agarre.</summary>
        public void OnGrabReleased(bool playerBrokeFree)
        {
            if (_currentGrabber == this) _currentGrabber = null;
            SetBool(GrabHash, false);
            if (IsDead) return;
            _nextAttackTime = Time.time + attackCooldown * 1.5f;
            if (playerBrokeFree)
            {
                // empujon: queda aturdido y se le puede patear
                Vector3 away = transform.position - (_player != null ? _player.position : transform.position - transform.forward);
                away.y = 0f;
                StartCoroutine(Shove(away.normalized * 2.2f, 0.25f));
                EnterStagger();
            }
            else
            {
                Resume();
            }
        }

        private IEnumerator Shove(Vector3 velocity, float duration)
        {
            float t = 0f;
            while (t < duration && _agent.enabled && _agent.isOnNavMesh)
            {
                t += Time.deltaTime;
                _agent.Move(velocity * Time.deltaTime / duration * (1f - t / duration) * 2f);
                yield return null;
            }
        }

        // ---------------------------------------------------------------- Dano y reacciones

        public void OnDamaged(DamageInfo info)
        {
            if (IsDead) return;

            if (State == GanadoState.Idle || State == GanadoState.Alert) BecomeAlertedSilently();

            if (heavy)
            {
                _heavyDamage += info.amount * (info.part == BodyPart.Head ? 1.5f : 1f);
                if (_heavyDamage >= heavyStaggerDamage && State != GanadoState.Stagger && State != GanadoState.Attack)
                {
                    _heavyDamage = 0f;
                    EnterStagger();
                }
                return;
            }

            if (State == GanadoState.Grab)
            {
                // Dispararle mientras agarra lo suelta
                if (info.part == BodyPart.Head || info.amount > 30f)
                {
                    ForceReleaseGrab();
                    EnterStagger();
                }
                return;
            }

            if (State == GanadoState.Down || State == GanadoState.GettingUp) return;

            float roll = Random.value;
            if (info.part == BodyPart.Head && roll < info.staggerPower * 1.3f) { EnterStagger(); return; }
            if (info.part == BodyPart.Leg && roll < info.staggerPower * 1.1f) { EnterStagger(); return; }
            if (roll < info.staggerPower + 0.15f && State != GanadoState.Stagger)
            {
                Enter(GanadoState.Flinch);
                StopAgent(true);
                Trigger(HitHash);
                if (info.knockback > 0f) StartCoroutine(Shove(new Vector3(info.direction.x, 0f, info.direction.z).normalized * info.knockback, 0.2f));
            }
            Ganado(GanadoSound.Pain, 0.9f, ProceduralAudio.Growl(Random.Range(0, 6)));
        }

        private void BecomeAlertedSilently()
        {
            State = GanadoState.Chase;
            foreach (var other in All)
            {
                if (other != this && other.State == GanadoState.Idle && (other.transform.position - transform.position).sqrMagnitude < 400f)
                    other.BecomeAlerted(false);
            }
        }

        private void ForceReleaseGrab()
        {
            if (_currentGrabber == this) _currentGrabber = null;
            SetBool(GrabHash, false);
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsGrabbed) PlayerHealth.Instance.ReleaseGrab();
        }

        private void EnterStagger()
        {
            Enter(GanadoState.Stagger);
            StopAgent(true);
            SetBool(StaggerHash, true);
            Ganado(GanadoSound.Pain, 0.9f, ProceduralAudio.Scream());
        }

        private void UpdateStagger()
        {
            if (_stateTime > staggerDuration) Resume();
        }

        /// <summary>Derribo por patada o explosion. Se desliza por la NavMesh (sin atravesar el suelo).</summary>
        public void Knockdown(Vector3 impulse)
        {
            if (IsDead) return;
            if (heavy)
            {
                // solo un golpe enorme (cohete) lo tumba
                if (impulse.magnitude < 4f) return;
            }
            if (State == GanadoState.Grab) ForceReleaseGrab();
            Enter(GanadoState.Down);
            StopAgent(true);
            SetBool(StaggerHash, false);
            SetBool(DownHash, true);
            _knockVelocity = new Vector3(impulse.x, 0f, impulse.z);
        }

        private void UpdateDown()
        {
            if (_knockVelocity.sqrMagnitude > 0.01f && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.Move(_knockVelocity * Time.deltaTime);
                _knockVelocity = Vector3.Lerp(_knockVelocity, Vector3.zero, Time.deltaTime * 4f);
            }
            if (_stateTime > downDuration)
            {
                SetBool(DownHash, false);
                Enter(GanadoState.GettingUp);
            }
        }

        public void OnDeath(DamageInfo info)
        {
            ReleaseToken();
            _throwing = false;
            if (State == GanadoState.Grab || _currentGrabber == this) ForceReleaseGrab();
            Enter(GanadoState.Dead);

            SetInt(DeathIndexHash, Random.Range(0, 2));
            SetBool(DeadHash, true);
            SetBool(StaggerHash, false);
            SetBool(DownHash, false);
            SetFloat(SpeedHash, 0f);

            if (_agent.enabled && _agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
            }
            // Se desactiva el agente: el cuerpo se queda exactamente donde cayo (sin hundirse)
            _agent.enabled = false;
            if (_bodyCollider != null) _bodyCollider.enabled = false;

            if (info.kind == DamageKind.Explosion || info.kind == DamageKind.Kick)
            {
                StartCoroutine(SlideCorpse(new Vector3(info.direction.x, 0f, info.direction.z) * 2.5f));
            }

            Ganado(GanadoSound.Death, 1f, ProceduralAudio.Growl(7));
            // Como en RE4: el cuerpo cae, se queda un momento y desaparece hundiendose (el jefe tarda mas)
            CorpseVanish.Begin(gameObject, heavy ? 8f : 3.5f, heavy ? 3f : 1.8f);
        }

        private IEnumerator SlideCorpse(Vector3 velocity)
        {
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                Vector3 next = transform.position + velocity * Time.deltaTime;
                if (NavMesh.SamplePosition(next, out NavMeshHit hit, 1f, NavMesh.AllAreas)) transform.position = hit.position;
                velocity = Vector3.Lerp(velocity, Vector3.zero, Time.deltaTime * 5f);
                yield return null;
            }
        }

        // ---------------------------------------------------------------- Voz

        private void Growl()
        {
            if (Time.time < _nextGrowl) return;
            bool active = State == GanadoState.Chase || State == GanadoState.Alert;
            _nextGrowl = Time.time + (active ? Random.Range(3f, 7f) : Random.Range(7f, 15f));
            // Como en RE4, los aldeanos se gritan cosas mientras te persiguen
            if (active && Random.value < 0.45f && Bark(VillagerBarks.Chase)) return;
            if (!active && Random.value < 0.25f && Bark(VillagerBarks.Idle)) return;
            Ganado(active && Random.value < 0.5f ? GanadoSound.Pant : GanadoSound.Grunt, active ? 0.75f : 0.45f, ProceduralAudio.Growl(Random.Range(0, 6)));
        }

        /// <summary>Grito hablado (si ya esta generado) con un gruñido debajo. Devuelve false si no hay voz.</summary>
        private bool Bark(string[] set)
        {
            if (heavy || _speech == null || _speech.isPlaying) return false;
            AudioClip c = VillagerBarks.Pick(set, _barkVoice);
            if (c == null) return false;
            _speech.pitch = _voicePitch * Random.Range(0.92f, 0.98f);
            _speech.PlayOneShot(c, 1f);
            AudioClip under = GanadoVoices.Pick(GanadoSound.Grunt, _female);
            if (under != null && _voice != null)
            {
                _voice.pitch = _voicePitch * 0.9f;
                _voice.PlayOneShot(under, 0.35f);
            }
            return true;
        }

        /// <summary>Gruñido / grito / dolor con la voz sintetizada del aldeano (o el sonido procedimental si falta).</summary>
        private void Ganado(GanadoSound kind, float volume, AudioClip fallback)
        {
            if (_voice == null) return;
            AudioClip c = GanadoVoices.Pick(kind == GanadoSound.Pant ? GanadoSound.Grunt : kind, _female);
            if (c == null) { PlayVoice(fallback, volume); return; }
            // el Tyrant: la misma garganta pero enorme
            _voice.pitch = heavy ? Random.Range(0.5f, 0.6f) : _voicePitch * Random.Range(0.95f, 1.05f);
            _voice.PlayOneShot(c, volume);
        }

        private void PlayVoice(AudioClip clip, float volume)
        {
            if (_voice == null || clip == null) return;
            _voice.pitch = Random.Range(0.85f, 1.1f);
            _voice.PlayOneShot(clip, volume);
        }
    }
}
