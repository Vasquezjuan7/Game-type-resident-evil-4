using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.RE4
{
    /// <summary>
    /// Sistema de armas en primera persona estilo Resident Evil 4:
    /// apuntado con laser, disparo con dispersion/perdigones/penetracion, cohetes,
    /// recargas animadas por tipo, retroceso con muelle, cuchillo y patada contextual.
    /// </summary>
    [RequireComponent(typeof(AttacheCase))]
    public class WeaponSystem : MonoBehaviour
    {
        public static WeaponSystem Instance { get; private set; }

        [Header("Referencias")]
        public Transform weaponHolder;
        public AudioSource audioSource;

        [Header("Cuchillo")]
        public float knifeDamage = 22f;
        public float knifeRange = 1.9f;
        public float knifeCooldown = 0.45f;

        [Header("Cuchillo de combate (parry, guardia y sigilo)")]
        public float knifeMaxDurability = 100f;
        public float knifeDurability = 100f;
        [Tooltip("Tiempo tras pulsar el cuchillo en el que un golpe enemigo se desvia.")]
        public float parryWindow = 0.35f;
        public float perfectParryWindow = 0.18f;
        public float stealthKillRange = 2.3f;

        [Header("Patada (enemigo aturdido)")]
        public float kickRange = 2.8f;
        public float kickDamage = 60f;
        public float kickRadius = 3.2f;

        // ------------------------------------------------------------ Estado publico (HUD)
        public CaseEntry CurrentEntry { get; private set; }
        public WeaponDefinition Current => CurrentEntry != null && CurrentEntry.item != null ? CurrentEntry.item.weapon : null;
        public int Loaded => CurrentEntry != null ? CurrentEntry.loadedAmmo : 0;
        public int Reserve => Current != null && Current.ammoItem != null ? _case.CountItem(Current.ammoItem) : 0;
        public bool IsAiming { get; private set; }
        public int Capacity => CurrentEntry != null ? CurrentEntry.MagazineSize : 0;
        public bool IsScoped => IsAiming && Current != null && (Current.weaponClass == WeaponClass.Rifle || Current.useScopeOverlay) && _aimBlend > 0.9f;
        public bool IsReloading => _reloadRoutine != null;
        public bool IsBusy => _actionRoutine != null;
        public ZombieAI KickTarget { get; private set; }
        public ZombieAI StealthTarget { get; private set; }
        public bool IsGuarding { get; private set; }
        public bool KnifeBroken => knifeDurability <= 0f;
        public float KnifeNormalized => Mathf.Clamp01(knifeDurability / Mathf.Max(1f, knifeMaxDurability));
        /// <summary>(perfecto) cada vez que se desvia un ataque con el cuchillo.</summary>
        public event Action<bool> OnParry;
        public float AimBlend => _aimBlend;

        public event Action<bool, bool> OnHitConfirmed; // (muerte, cabeza)
        public event Action OnWeaponChanged;

        // ------------------------------------------------------------ Internos
        private class ViewModel
        {
            public WeaponDefinition def;
            public Transform root;
            public Transform muzzle;
            public Transform ejectPort;

            // Piezas animadas (opcionales)
            public Transform slide, magazine;
            public Vector3 slideBase, magBase;
            public Vector3 slideBackLocal, magOutLocal;
            public bool hasSight;
            public Vector3 sight;

            // Tambor del revolver
            public Transform cylinder;
            public Quaternion cylBaseRot;
            public Vector3 cylBasePos, cylAxisLocal, cylOutLocal;
        }

        private AttacheCase _case;
        private FirstPersonController _fps;
        private Camera _cam;
        private readonly Dictionary<WeaponDefinition, ViewModel> _models = new Dictionary<WeaponDefinition, ViewModel>();
        private ViewModel _vm;
        private Transform _pivot;
        private Transform _knife;

        private float _nextFireTime;
        private float _aimBlend;
        private Coroutine _reloadRoutine;
        private Coroutine _actionRoutine;
        private Coroutine _cycleRoutine;
        private bool _cancelReload;
        private float _lastKnifeTime;
        private float _knifePressTime = -10f;
        private bool _guardShown;
        private bool _wasAiming;
        private bool _aimToggled;
        private bool _triggerHeldLastFrame;
        private bool _hideWeapon;

        // Animacion procedimental
        private Vector3 _recoilPos, _recoilPosVel;
        private Vector3 _recoilRot, _recoilRotVel;
        private Vector3 _animPos, _animPosTarget;
        private Vector3 _animRot, _animRotTarget;
        private float _equipBlend;
        private Vector2 _swaySmoothed;
        private float _sprintBlend;

        // Corredera / cargador / rafaga
        private float _slideKick;
        private float _slideManual;
        private bool _slideLocked;
        private float _magOut;
        private int _spray;
        private float _lastShotTime;
        private float _cylAngle, _cylAngleTarget, _cylOut;

        private LineRenderer _laser;
        private Transform _laserDot;

        private void Awake()
        {
            Instance = this;
            _case = GetComponent<AttacheCase>();
            _fps = GetComponent<FirstPersonController>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0f;
                audioSource.playOnAwake = false;
            }
        }

        private void Start()
        {
            _cam = _fps != null && _fps.ViewCamera != null ? _fps.ViewCamera : Camera.main;
            if (weaponHolder == null && _fps != null) weaponHolder = _fps.weaponHolder;

            _pivot = new GameObject("WeaponPivot").transform;
            _pivot.SetParent(weaponHolder, false);
            SetLayer(_pivot.gameObject, RE4Layers.Weapon);

            BuildKnife();
            BuildLaser();

            _case.OnChanged += OnCaseChanged;

            // Equipar la primera arma del atajo 1
            for (int i = 0; i < _case.quickSlots.Length; i++)
            {
                if (_case.quickSlots[i] != null) { Equip(_case.quickSlots[i], true); break; }
            }
        }

        private void OnDestroy()
        {
            if (_case != null) _case.OnChanged -= OnCaseChanged;
        }

        private void OnCaseChanged()
        {
            if (CurrentEntry != null && !_case.entries.Contains(CurrentEntry))
            {
                Holster();
                var weapons = _case.Weapons();
                if (weapons.Count > 0) Equip(weapons[0], false);
            }
            else if (CurrentEntry == null)
            {
                var weapons = _case.Weapons();
                if (weapons.Count > 0) Equip(weapons[0], false);
            }
            OnWeaponChanged?.Invoke();
        }

        // ================================================================= Equipar

        public void Equip(CaseEntry entry, bool instant = false)
        {
            if (entry == null || entry.item == null || entry.item.weapon == null) return;
            if (entry == CurrentEntry && _vm != null) return;

            CancelReload();
            if (_vm != null) _vm.root.gameObject.SetActive(false);

            CurrentEntry = entry;
            _vm = GetViewModel(entry.item.weapon);
            _slideKick = 0f;
            _slideManual = 0f;
            _magOut = 0f;
            _slideLocked = entry.loadedAmmo <= 0 && entry.item.weapon.slideLocksOnEmpty;
            _cylOut = 0f;
            if (_vm != null) _vm.root.gameObject.SetActive(true);
            _equipBlend = instant ? 1f : 0f;
            _nextFireTime = Time.time + (instant ? 0f : 0.35f);
            if (!instant) Snd(entry.item.weapon.equipClip, ProceduralAudio.Click(900f, 60f), 0.5f);
            OnWeaponChanged?.Invoke();
        }

        public void Holster()
        {
            CancelReload();
            if (_vm != null) _vm.root.gameObject.SetActive(false);
            _vm = null;
            CurrentEntry = null;
            OnWeaponChanged?.Invoke();
        }

        private ViewModel GetViewModel(WeaponDefinition def)
        {
            if (_models.TryGetValue(def, out ViewModel vm) && vm.root != null) return vm;

            vm = new ViewModel { def = def };
            GameObject root = new GameObject("VM_" + def.id);
            root.transform.SetParent(_pivot, false);
            vm.root = root.transform;

            GameObject orient = new GameObject("Orientation");
            orient.transform.SetParent(root.transform, false);
            orient.transform.localRotation = Quaternion.Euler(def.modelEulerOffset);

            Bounds b = new Bounds(Vector3.zero, new Vector3(0.05f, 0.1f, def.viewModelLength));
            if (def.viewModelPrefab != null)
            {
                GameObject model = Instantiate(def.viewModelPrefab, orient.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                StripForViewModel(model);

                b = LocalBounds(root.transform, model);
                float length = Mathf.Max(0.001f, b.size.z);
                float scale = def.viewModelLength / length;
                orient.transform.localScale = Vector3.one * scale;
                b = LocalBounds(root.transform, model);
                // centrar: el pivote queda un poco detras del centro (empunadura)
                orient.transform.localPosition = -b.center + new Vector3(0f, 0f, b.size.z * 0.12f);
                b = LocalBounds(root.transform, model);
            }
            else
            {
                GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(placeholder.GetComponent<Collider>());
                placeholder.transform.SetParent(orient.transform, false);
                placeholder.transform.localScale = b.size;
            }

            vm.muzzle = new GameObject("Muzzle").transform;
            vm.muzzle.SetParent(root.transform, false);
            vm.muzzle.localPosition = new Vector3(b.center.x, b.center.y + b.extents.y * 0.45f, b.max.z + 0.01f);
            Transform muzzleMark = FindPart(root.transform, "MuzzlePoint");
            if (muzzleMark != null) vm.muzzle.position = muzzleMark.position;

            Transform sightMark = FindPart(root.transform, "SightPoint");
            if (sightMark != null)
            {
                vm.hasSight = true;
                vm.sight = root.transform.InverseTransformPoint(sightMark.position);
            }

            float travel = def.slideTravel * def.viewModelLength;
            vm.slide = FindPart(root.transform, def.slidePart);
            if (vm.slide != null)
            {
                vm.slideBase = vm.slide.localPosition;
                vm.slideBackLocal = vm.slide.parent.InverseTransformVector(root.transform.TransformVector(Vector3.back * travel));
            }
            vm.cylinder = FindPart(root.transform, def.cylinderPart);
            if (vm.cylinder != null)
            {
                vm.cylBaseRot = vm.cylinder.localRotation;
                vm.cylBasePos = vm.cylinder.localPosition;
                vm.cylAxisLocal = vm.cylinder.parent.InverseTransformDirection(root.transform.forward).normalized;
                vm.cylOutLocal = vm.cylinder.parent.InverseTransformVector(root.transform.TransformVector(new Vector3(-0.035f, -0.012f, 0f)));
            }
            vm.magazine = FindPart(root.transform, def.magazinePart);
            if (vm.magazine != null)
            {
                vm.magBase = vm.magazine.localPosition;
                vm.magOutLocal = vm.magazine.parent.InverseTransformVector(root.transform.TransformVector(new Vector3(0f, -0.2f, -0.03f)));
            }

            vm.ejectPort = new GameObject("EjectPort").transform;
            vm.ejectPort.SetParent(root.transform, false);
            vm.ejectPort.localPosition = new Vector3(b.center.x + 0.02f, b.center.y + b.extents.y * 0.5f, b.center.z + b.size.z * 0.1f);

            SetLayer(root, RE4Layers.Weapon);
            root.SetActive(false);
            _models[def] = vm;
            return vm;
        }

        private static Transform FindPart(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private static void StripForViewModel(GameObject model)
        {
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var r in model.GetComponentsInChildren<Rigidbody>(true)) Destroy(r);
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) a.enabled = false;
            foreach (var m in model.GetComponentsInChildren<MonoBehaviour>(true)) m.enabled = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private static Bounds LocalBounds(Transform space, GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            bool has = false;
            Bounds result = new Bounds();
            foreach (var r in renderers)
            {
                Mesh mesh = null;
                if (r is MeshRenderer)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }
                else if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                if (mesh == null) continue;

                Bounds mb = mesh.bounds;
                Vector3 c = mb.center, e = mb.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = c + Vector3.Scale(e, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (!has) { result = new Bounds(p, Vector3.zero); has = true; }
                    else result.Encapsulate(p);
                }
            }
            return has ? result : new Bounds(Vector3.zero, Vector3.one * 0.1f);
        }

        private static void SetLayer(GameObject go, int layer)
        {
            if (layer < 0) return;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        // ================================================================= Update

        private void Update()
        {
            bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead;
            bool locked = dead || (_fps != null && _fps.InputLocked);

            if (!locked) HandleInput();
            else
            {
                IsAiming = false;
                if (_fps != null) _fps.IsAiming = false;
            }

            UpdateKickTarget();
            Animate();
            UpdateLaser();
        }

        private void LateUpdate()
        {
            if (_fps != null && _fps.weaponCamera != null && _cam != null)
            {
                _fps.weaponCamera.fieldOfView = _cam.fieldOfView;
            }
        }

        private void HandleInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            // Cambiar arma: 1-5 y rueda del raton
            if (kb != null)
            {
                Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
                for (int i = 0; i < keys.Length; i++)
                {
                    if (kb[keys[i]].wasPressedThisFrame) SelectQuickSlot(i);
                }
            }
            if (mouse != null && !IsAiming)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll > 0.1f) CycleWeapon(1);
                else if (scroll < -0.1f) CycleWeapon(-1);
            }
            if (pad != null)
            {
                if (pad.dpad.right.wasPressedThisFrame) CycleWeapon(1);
                if (pad.dpad.left.wasPressedThisFrame) CycleWeapon(-1);
            }

            // Apuntar
            bool aimHeld = (mouse != null && mouse.rightButton.isPressed) || (pad != null && pad.leftTrigger.isPressed);
            // Opciones › Accesibilidad › Apuntar: "Pulsar" alterna en vez de mantener
            if (DeadEcho.Core.SettingsService.Current.holdToToggle)
            {
                if ((mouse != null && mouse.rightButton.wasPressedThisFrame) || (pad != null && pad.leftTrigger.wasPressedThisFrame))
                    _aimToggled = !_aimToggled;
                if (Current == null) _aimToggled = false;
                aimHeld = _aimToggled;
            }
            else _aimToggled = false;
            IsAiming = aimHeld && Current != null && !IsBusy;
            if (_fps != null) _fps.IsAiming = IsAiming;

            if (IsAiming && !_wasAiming && Current != null && Current.aimClip != null) PlayClip(Current.aimClip, 0.45f, 1f, 0.03f);
            _wasAiming = IsAiming;

            // Cuchillo: pulsar = tajo (y ventana de parry); mantener = guardia; por la espalda = asesinato sigiloso
            bool knife = (kb != null && kb.vKey.wasPressedThisFrame) || (mouse != null && mouse.middleButton.wasPressedThisFrame)
                         || (pad != null && pad.rightShoulder.wasPressedThisFrame);
            bool knifeHeld = (kb != null && kb.vKey.isPressed) || (mouse != null && mouse.middleButton.isPressed) || (pad != null && pad.rightShoulder.isPressed);
            if (knife) _knifePressTime = Time.time;
            IsGuarding = knifeHeld && !KnifeBroken && !IsBusy && Time.time - _knifePressTime > 0.25f;
            UpdateGuardPose();
            if (knife && KnifeBroken)
            {
                PlayLocal(ProceduralAudio.Click(400f, 60f), 0.6f);
                PlayerInteractor pi = GetComponent<PlayerInteractor>();
                if (pi != null) pi.ShowMessage("El cuchillo está roto. El mercader puede repararlo.");
            }
            else if (knife && !IsBusy && StealthTarget != null)
            {
                CancelReload();
                _actionRoutine = StartCoroutine(StealthKillRoutine(StealthTarget));
                return;
            }
            else if (knife && !IsBusy && Time.time - _lastKnifeTime > knifeCooldown)
            {
                CancelReload();
                _actionRoutine = StartCoroutine(KnifeRoutine());
                return;
            }

            // Patada contextual
            bool kick = (kb != null && kb.fKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            if (kick && KickTarget != null && !IsBusy)
            {
                CancelReload();
                _actionRoutine = StartCoroutine(KickRoutine(KickTarget));
                return;
            }

            if (Current == null) return;

            // Recargar
            bool reload = (kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);
            if (reload) TryReload();

            // Disparar
            bool triggerHeld = (mouse != null && mouse.leftButton.isPressed && Cursor.lockState == CursorLockMode.Locked)
                               || (pad != null && pad.rightTrigger.isPressed);
            bool triggerDown = triggerHeld && !_triggerHeldLastFrame;
            _triggerHeldLastFrame = triggerHeld;

            if (triggerDown && IsReloading && Current.reloadOneByOne && CurrentEntry.loadedAmmo > 0)
            {
                _cancelReload = true; // interrumpir recarga de escopeta
            }

            bool wantsFire = Current.automatic ? triggerHeld : triggerDown;
            if (wantsFire && !IsReloading && !IsBusy && _cycleRoutine == null && Time.time >= _nextFireTime && _equipBlend > 0.85f)
            {
                if (CurrentEntry.loadedAmmo > 0) Fire();
                else
                {
                    _nextFireTime = Time.time + 0.3f;
                    PlayLocal(ProceduralAudio.Click(2400f, 220f), 0.6f);
                    TryReload();
                }
            }
        }

        public void SelectQuickSlot(int index)
        {
            if (index < 0 || index >= _case.quickSlots.Length) return;
            CaseEntry e = _case.quickSlots[index];
            if (e != null && _case.entries.Contains(e)) Equip(e);
        }

        private void CycleWeapon(int dir)
        {
            var list = new List<CaseEntry>();
            foreach (var q in _case.quickSlots) if (q != null && _case.entries.Contains(q)) list.Add(q);
            if (list.Count == 0) list = _case.Weapons();
            if (list.Count == 0) return;
            int idx = list.IndexOf(CurrentEntry);
            idx = (idx + dir + list.Count) % list.Count;
            Equip(list[idx]);
        }

        // ================================================================= Disparo

        private void Fire()
        {
            WeaponDefinition w = Current;
            CurrentEntry.loadedAmmo--;
            float interval = WeaponStats.FireInterval(w, CurrentEntry);
            _nextFireTime = Time.time + interval;
            OnWeaponChanged?.Invoke();

            // Rafaga: el retroceso de las automaticas sube mientras se mantiene el gatillo
            _spray = Time.time - _lastShotTime < interval * 2.2f ? _spray + 1 : 0;
            _lastShotTime = Time.time;
            float sprayMul = w.automatic ? 1f + Mathf.Min(_spray, 12) * 0.07f : 1f;
            float sprayYaw = w.automatic ? Mathf.Sin(_spray * 0.55f) * w.recoilYaw * 0.6f : 0f;

            // Corredera / cerrojo hacia atras; el tambor gira a la siguiente recamara
            _slideKick = 1f;
            _cylAngleTarget += 360f / Mathf.Max(1, w.cylinderChambers);
            if (CurrentEntry.loadedAmmo <= 0 && w.slideLocksOnEmpty) _slideLocked = true;

            AudioClip shot = SoundBank.Pick(w.shotClips);
            if (shot != null)
            {
                PlayClip(shot, 1f, w.clipPitch, 0.04f);
                AudioClip tail = SoundBank.Pick(w.tailClips);
                if (tail != null) PlayClip(tail, 0.5f, w.clipPitch, 0.04f);
            }
            else PlayLocal(ProceduralAudio.Gunshot(w.shotPitch, w.shotBody, w.shotLength), 1f, 0.05f);
            NoiseSystem.Emit(transform.position, w.noiseRadius);

            if (_vm != null)
            {
                float flashScale = w.weaponClass == WeaponClass.Shotgun ? 1.6f : w.weaponClass == WeaponClass.RocketLauncher ? 2.2f : w.weaponClass == WeaponClass.Handgun ? 0.9f : 1.2f;
                FXManager.Get().MuzzleFlash(_vm.muzzle, flashScale);
                if (w.weaponClass != WeaponClass.Shotgun && w.weaponClass != WeaponClass.RocketLauncher && !w.cycleAfterShot && _vm.cylinder == null)
                {
                    EjectShell(w);
                }
            }

            // Retroceso: arma y camara
            float aimFactor = IsAiming ? 0.6f : 1f;
            _recoilPosVel += new Vector3(UnityEngine.Random.Range(-0.15f, 0.15f), 0.25f, -w.recoilKickBack * 40f) * aimFactor;
            _recoilRotVel += new Vector3(-w.recoilPitch * 40f * sprayMul, (UnityEngine.Random.Range(-w.recoilYaw, w.recoilYaw) + sprayYaw) * 40f, UnityEngine.Random.Range(-3f, 3f) * 20f) * aimFactor;
            if (_fps != null)
            {
                _fps.AddRecoil(w.cameraPitchKick * aimFactor * sprayMul, (UnityEngine.Random.Range(-w.recoilYaw, w.recoilYaw) * 0.4f + sprayYaw * 0.5f) * aimFactor);
                _fps.AddShake(w.weaponClass == WeaponClass.Shotgun || w.weaponClass == WeaponClass.RocketLauncher ? 0.28f : 0.08f);
            }

            if (w.weaponClass == WeaponClass.RocketLauncher) FireRocket(w);
            else FireHitscan(w);

            if (w.cycleAfterShot) _cycleRoutine = StartCoroutine(CycleRoutine(w));

            // Recarga automatica al vaciar (como RE4)
            if (CurrentEntry.loadedAmmo <= 0 && Reserve > 0 && !w.cycleAfterShot) StartCoroutine(DelayedAutoReload(0.25f));
        }

        private IEnumerator DelayedAutoReload(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (CurrentEntry != null && CurrentEntry.loadedAmmo <= 0) TryReload();
        }

        private void EjectShell(WeaponDefinition w)
        {
            if (_vm == null || _cam == null) return;
            float size = w.weaponClass == WeaponClass.Shotgun ? 1.6f : w.weaponClass == WeaponClass.Rifle ? 1.4f : 1f;
            FXManager.Get().EjectShell(_vm.ejectPort.position, _cam.transform.right, _cam.transform.up, size);
        }

        private float CurrentSpread(WeaponDefinition w)
        {
            float spread = Mathf.Lerp(w.hipSpread, w.aimSpread, _aimBlend);
            if (_fps != null)
            {
                float speed = new Vector3(_fps.Velocity.x, 0f, _fps.Velocity.z).magnitude;
                spread += speed * (IsAiming ? 0.25f : 0.6f);
                if (!_fps.IsGrounded) spread += 3f;
                // Cansado tiembla el pulso; agachado se apunta mas estable
                spread += _fps.Fatigue * (IsAiming ? 1.6f : 2.4f);
                spread *= Mathf.Lerp(1f, 0.8f, _fps.CrouchBlend);
            }
            return spread;
        }

        private void FireHitscan(WeaponDefinition w)
        {
            if (_cam == null) return;
            float spread = CurrentSpread(w);
            float damage = WeaponStats.Damage(w, CurrentEntry);
            int pellets = Mathf.Max(1, w.pellets);
            var alreadyHit = new HashSet<ZombieHealth>();
            bool anyKill = false, anyHead = false, anyHit = false;

            for (int p = 0; p < pellets; p++)
            {
                float pelletSpread = pellets > 1 ? Mathf.Max(spread, w.aimSpread) + 4.5f : spread;
                Vector2 rnd = UnityEngine.Random.insideUnitCircle * Mathf.Tan(pelletSpread * Mathf.Deg2Rad);
                Vector3 dir = (_cam.transform.forward + _cam.transform.right * rnd.x + _cam.transform.up * rnd.y).normalized;
                Vector3 origin = _cam.transform.position;

                RaycastHit[] hits = Physics.RaycastAll(origin, dir, w.range, RE4Layers.ShotMask, QueryTriggerInteraction.Collide);
                Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                int penetrationLeft = w.penetration;
                Vector3 end = origin + dir * w.range;
                if (pellets > 1) alreadyHit.Clear();

                foreach (var hit in hits)
                {
                    if (hit.collider.transform.IsChildOf(transform)) continue;

                    Breakable crate = hit.collider.GetComponentInParent<Breakable>();
                    if (crate != null)
                    {
                        crate.Hit(damage, hit.point, dir);
                        end = hit.point;
                        break;
                    }

                    FarmAnimal animal = hit.collider.GetComponentInParent<FarmAnimal>();
                    if (animal != null)
                    {
                        animal.Hit(damage, hit.point, dir);
                        end = hit.point;
                        break;
                    }

                    ZombieHitbox hb = hit.collider.GetComponent<ZombieHitbox>();
                    if (hb != null && hb.health != null)
                    {
                        if (hb.health.IsDead || alreadyHit.Contains(hb.health)) continue;
                        alreadyHit.Add(hb.health);

                        float falloff = pellets > 1 ? Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(4f, w.range, hit.distance)) : 1f;
                        var info = new DamageInfo
                        {
                            amount = damage * falloff,
                            kind = DamageKind.Bullet,
                            part = hb.part,
                            point = hit.point,
                            direction = dir,
                            staggerPower = w.staggerPower * falloff,
                            knockback = w.knockback * falloff,
                            source = gameObject
                        };
                        float mult = hb.part == BodyPart.Head ? w.headMultiplier : (hb.part == BodyPart.Arm || hb.part == BodyPart.Leg) ? w.limbMultiplier : 1f;
                        info.amount *= mult;

                        bool killed = hb.health.ApplyDamage(info);
                        FXManager.Get().FleshImpact(hit.point, dir, damage * mult > 60f || hb.part == BodyPart.Head);
                        anyHit = true;
                        anyKill |= killed;
                        anyHead |= hb.part == BodyPart.Head;

                        if (penetrationLeft-- > 0) continue;
                        end = hit.point;
                        break;
                    }

                    if (hit.collider.isTrigger) continue;

                    end = hit.point;
                    FXManager.Get().SurfaceImpact(hit.point, hit.normal);
                    Rigidbody rb = hit.rigidbody;
                    if (rb != null && !rb.isKinematic) rb.AddForceAtPosition(dir * damage * 0.05f, hit.point, ForceMode.Impulse);
                    break;
                }

                if (p % 3 == 0 && _vm != null) FXManager.Get().Tracer(_vm.muzzle.position, end);
            }

            if (anyHit)
            {
                OnHitConfirmed?.Invoke(anyKill, anyHead);
                PlayLocal(ProceduralAudio.Click(anyKill ? 600f : 1400f, 90f), anyKill ? 0.35f : 0.18f);
            }
        }

        private void FireRocket(WeaponDefinition w)
        {
            if (_vm == null || _cam == null) return;
            Vector3 aimPoint = _cam.transform.position + _cam.transform.forward * 80f;
            if (Physics.Raycast(_cam.transform.position, _cam.transform.forward, out RaycastHit hit, 200f, RE4Layers.ShotMask, QueryTriggerInteraction.Ignore))
            {
                aimPoint = hit.point;
            }
            Vector3 start = _vm.muzzle.position;
            RocketProjectile.Launch(start, (aimPoint - start).normalized, w, gameObject);
        }

        private IEnumerator CycleRoutine(WeaponDefinition w)
        {
            yield return new WaitForSeconds(0.12f);
            Snd(w.rackBackClip != null ? w.rackBackClip : w.rackClip, ProceduralAudio.Rack(), 0.7f);
            bool shotgun = w.weaponClass == WeaponClass.Shotgun;
            float dur = Mathf.Max(0.25f, WeaponStats.FireInterval(w, CurrentEntry) * 0.7f);
            float t = 0f;
            bool ejected = false;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                _animPosTarget = shotgun ? new Vector3(0f, -0.01f, -0.06f) * k : new Vector3(0.02f, 0.01f, -0.03f) * k;
                _animRotTarget = shotgun ? new Vector3(4f, 0f, -6f) * k : new Vector3(0f, 0f, 22f) * k;
                if (!ejected && t > dur * 0.4f)
                {
                    ejected = true;
                    EjectShell(w);
                }
                yield return null;
            }
            _animPosTarget = Vector3.zero;
            _animRotTarget = Vector3.zero;
            _cycleRoutine = null;
            if (CurrentEntry != null && CurrentEntry.loadedAmmo <= 0 && Reserve > 0) TryReload();
        }

        // ================================================================= Recarga

        public void TryReload()
        {
            WeaponDefinition w = Current;
            if (w == null || IsReloading || IsBusy) return;
            if (CurrentEntry.loadedAmmo >= CurrentEntry.MagazineSize) return;
            if (Reserve <= 0) return;
            _cancelReload = false;
            _reloadRoutine = StartCoroutine(w.reloadOneByOne ? ReloadOneByOne(w) : ReloadMagazine(w));
        }

        private void CancelReload()
        {
            if (_reloadRoutine != null) StopCoroutine(_reloadRoutine);
            _reloadRoutine = null;
            _animPosTarget = Vector3.zero;
            _animRotTarget = Vector3.zero;
            _slideManual = 0f;
            _magOut = 0f;
            _cylOut = 0f;
        }

        private IEnumerator Pose(Vector3 pos, Vector3 rot, float duration)
        {
            Vector3 p0 = _animPosTarget, r0 = _animRotTarget;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                _animPosTarget = Vector3.Lerp(p0, pos, k);
                _animRotTarget = Vector3.Lerp(r0, rot, k);
                yield return null;
            }
            _animPosTarget = pos;
            _animRotTarget = rot;
        }

        private IEnumerator ReloadMagazine(WeaponDefinition w)
        {
            float T = WeaponStats.ReloadTime(w, CurrentEntry);
            CaseEntry entry = CurrentEntry;
            bool rocket = w.weaponClass == WeaponClass.RocketLauncher;
            ViewModel vm = _vm;

            if (rocket)
            {
                yield return Pose(new Vector3(0.05f, -0.28f, -0.05f), new Vector3(40f, -10f, 10f), T * 0.3f);
                Snd(w.rackBackClip, ProceduralAudio.Click(500f, 40f), 0.7f);
                yield return new WaitForSeconds(T * 0.35f);
                LoadAmmo(entry, w);
                Snd(w.magInClip != null ? w.magInClip : w.rackClip, ProceduralAudio.Rack(), 0.8f);
                yield return Pose(Vector3.zero, Vector3.zero, T * 0.35f);
            }
            else if (vm != null && vm.cylinder != null)
            {
                int spent = entry.MagazineSize - entry.loadedAmmo;
                // 1. Girar el revolver y abrir el tambor hacia la izquierda
                yield return Pose(new Vector3(0.02f, -0.02f, -0.02f), new Vector3(-10f, 14f, 48f), T * 0.14f);
                Snd(w.rackBackClip, ProceduralAudio.Click(900f, 60f), 0.8f);
                yield return AnimateCylinder(0f, 1f, T * 0.08f);
                // 2. Boca arriba: caen las vainas vacias
                yield return Pose(new Vector3(0.01f, 0.01f, -0.03f), new Vector3(-55f, 10f, 40f), T * 0.14f);
                for (int i = 0; i < Mathf.Max(1, spent); i++)
                {
                    if (_cam != null) FXManager.Get().EjectShell(vm.cylinder.position, -_cam.transform.up * 0.3f + _cam.transform.right * UnityEngine.Random.Range(-0.2f, 0.2f), -_cam.transform.up, 1.1f);
                }
                PlayLocal(ProceduralAudio.ShellDrop(), 0.7f);
                // 3. Meter las balas una a una
                yield return Pose(new Vector3(0.02f, -0.03f, -0.01f), new Vector3(-6f, 16f, 46f), T * 0.1f);
                int rounds = Mathf.Clamp(Mathf.Min(spent, Reserve), 1, 8);
                float per = T * 0.32f / rounds;
                for (int i = 0; i < rounds; i++)
                {
                    yield return new WaitForSeconds(per);
                    _cylAngleTarget += 360f / Mathf.Max(1, w.cylinderChambers);
                    Snd(w.magInClip, ProceduralAudio.Click(1700f, 110f), 0.55f);
                }
                LoadAmmo(entry, w);
                // 4. Cerrar el tambor de un golpe de muneca
                yield return AnimateCylinder(1f, 0f, T * 0.07f);
                Snd(w.rackClip, ProceduralAudio.Rack(), 0.8f);
                _recoilRotVel += new Vector3(-20f, 0f, -40f);
                yield return Pose(Vector3.zero, Vector3.zero, T * 0.15f);
            }
            else if (vm != null && vm.magazine != null)
            {
                bool handgun = w.weaponClass == WeaponClass.Handgun;
                bool needsRack = entry.loadedAmmo <= 0 || _slideLocked;
                Vector3 tiltPos = handgun ? new Vector3(-0.03f, -0.02f, -0.02f) : new Vector3(0f, -0.03f, 0f);
                Vector3 tiltRot = handgun ? new Vector3(-14f, 18f, 32f) : new Vector3(-8f, 16f, 30f);

                // 1. Girar el arma para ver el pozo del cargador
                yield return Pose(tiltPos, tiltRot, T * 0.14f);

                // 2. Soltar el cargador vacio: cae al suelo
                Snd(w.magOutClip, ProceduralAudio.Click(1100f, 70f), 0.7f);
                DropMagazine(vm);
                yield return AnimateMag(0f, 1f, T * 0.1f);
                _recoilPosVel += new Vector3(0f, 0.25f, 0f);

                // 3. La mano trae un cargador nuevo
                if (w.magDrawClip != null) PlayClip(w.magDrawClip, 0.7f, w.clipPitch, 0.03f);
                yield return Pose(tiltPos + new Vector3(0f, -0.015f, 0f), tiltRot + new Vector3(-4f, 0f, 4f), T * 0.16f);

                // 4. Meterlo de un golpe
                yield return AnimateMag(1f, 0.15f, T * 0.14f);
                Snd(w.magInClip, ProceduralAudio.Click(1500f, 80f), 0.85f);
                _magOut = 0f;
                _recoilPosVel += new Vector3(0f, 0.8f, 0f);
                _recoilRotVel += new Vector3(-25f, 0f, 0f);
                LoadAmmo(entry, w);
                yield return Pose(tiltPos * 0.5f, tiltRot * 0.55f, T * 0.12f);

                // 5. Liberar la corredera / tirar de la palanca de carga
                if (needsRack && vm.slide != null)
                {
                    if (_slideLocked)
                    {
                        // corredera bloqueada atras: se suelta y vuelve de golpe
                        yield return new WaitForSeconds(T * 0.05f);
                        _slideLocked = false;
                        _slideKick = 1f;
                    }
                    else
                    {
                        yield return AnimateSlide(0f, 1f, T * 0.1f);
                        _slideManual = 0f;
                        _slideKick = 1f;
                    }
                    Snd(w.rackClip, ProceduralAudio.Rack(), 0.85f);
                    _recoilPosVel += new Vector3(0f, 0f, -1.2f);
                    _recoilRotVel += new Vector3(-30f, 0f, 0f);
                }
                _slideLocked = false;
                yield return Pose(Vector3.zero, Vector3.zero, T * 0.2f);
            }
            else
            {
                // 1. Inclinar arma
                yield return Pose(new Vector3(0.02f, -0.03f, 0f), new Vector3(-8f, 10f, 28f), T * 0.18f);
                // 2. Sacar cargador
                Snd(w.magOutClip, ProceduralAudio.Click(1100f, 70f), 0.7f);
                yield return Pose(new Vector3(0.03f, -0.06f, 0.01f), new Vector3(-12f, 14f, 34f), T * 0.27f);
                // 3. Meter cargador nuevo (golpe hacia arriba)
                Snd(w.magInClip, ProceduralAudio.Click(1500f, 80f), 0.8f);
                _recoilPosVel += new Vector3(0f, 0.6f, 0f);
                LoadAmmo(entry, w);
                yield return Pose(new Vector3(0.01f, -0.02f, 0f), new Vector3(-4f, 6f, 18f), T * 0.2f);
                // 4. Corredera / palanca de carga
                Snd(w.rackClip, ProceduralAudio.Rack(), 0.8f);
                _slideLocked = false;
                _slideKick = 1f;
                _recoilPosVel += new Vector3(0f, 0f, -1.2f);
                _recoilRotVel += new Vector3(-30f, 0f, 0f);
                yield return Pose(Vector3.zero, Vector3.zero, T * 0.35f);
            }

            _reloadRoutine = null;
            OnWeaponChanged?.Invoke();
        }

        private IEnumerator AnimateMag(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                _magOut = Mathf.Lerp(from, to, from < to ? k * k : 1f - (1f - k) * (1f - k));
                yield return null;
            }
            _magOut = to;
        }

        private IEnumerator AnimateCylinder(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _cylOut = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            _cylOut = to;
        }

        private IEnumerator AnimateSlide(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                _slideManual = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            _slideManual = to;
        }

        /// <summary>Copia del cargador que cae al suelo con fisica.</summary>
        private void DropMagazine(ViewModel vm)
        {
            if (vm.magazine == null || !vm.magazine.gameObject.activeInHierarchy) return;
            GameObject copy = Instantiate(vm.magazine.gameObject, vm.magazine.position, vm.magazine.rotation);
            copy.name = "Cargador_Vacio";
            copy.transform.localScale = vm.magazine.lossyScale;
            SetLayer(copy, 0);
            foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            foreach (var mf in copy.GetComponentsInChildren<MeshFilter>(true)) mf.gameObject.AddComponent<BoxCollider>();
            Rigidbody rb = copy.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            Vector3 inherit = _fps != null ? _fps.Velocity : Vector3.zero;
            rb.linearVelocity = inherit + (_cam != null ? -_cam.transform.up * 1.2f + _cam.transform.right * 0.2f : Vector3.down);
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 4f;
            Destroy(copy, 10f);
        }

        private IEnumerator ReloadOneByOne(WeaponDefinition w)
        {
            CaseEntry entry = CurrentEntry;
            yield return Pose(new Vector3(-0.02f, -0.04f, 0f), new Vector3(-14f, -8f, -30f), 0.25f);

            float perShell = WeaponStats.ReloadTime(w, entry) / Mathf.Max(1, w.magazineSize) * 1.4f;
            while (entry.loadedAmmo < entry.MagazineSize && Reserve > 0 && !_cancelReload)
            {
                // empujar cartucho
                float t = 0f;
                while (t < perShell)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Sin(Mathf.Clamp01(t / perShell) * Mathf.PI);
                    _animPosTarget = new Vector3(-0.02f, -0.04f + 0.02f * k, 0.015f * k);
                    yield return null;
                }
                if (_case.Consume(w.ammoItem, 1) > 0)
                {
                    entry.loadedAmmo++;
                    Snd(w.magInClip, ProceduralAudio.Click(1700f, 110f), 0.6f);
                    OnWeaponChanged?.Invoke();
                }
            }

            Snd(w.rackClip, ProceduralAudio.Rack(), 0.8f);
            _recoilPosVel += new Vector3(0f, 0f, -1.4f);
            yield return Pose(Vector3.zero, Vector3.zero, 0.25f);
            _reloadRoutine = null;
        }

        private void LoadAmmo(CaseEntry entry, WeaponDefinition w)
        {
            int need = entry.MagazineSize - entry.loadedAmmo;
            int got = _case.Consume(w.ammoItem, need);
            entry.loadedAmmo += got;
            OnWeaponChanged?.Invoke();
        }

        // ================================================================= Cuchillo y patada

        private void BuildKnife()
        {
            _knife = new GameObject("Knife").transform;
            _knife.SetParent(weaponHolder, false);
            Material mat = FXManager.Get().library != null ? FXManager.Get().library.knife : null;

            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(blade.GetComponent<Collider>());
            blade.transform.SetParent(_knife, false);
            blade.transform.localScale = new Vector3(0.006f, 0.032f, 0.2f);
            blade.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            if (mat != null) blade.GetComponent<Renderer>().sharedMaterial = mat;

            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(handle.GetComponent<Collider>());
            handle.transform.SetParent(_knife, false);
            handle.transform.localScale = new Vector3(0.028f, 0.055f, 0.028f);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            handle.GetComponent<Renderer>().material.color = new Color(0.08f, 0.07f, 0.06f);

            SetLayer(_knife.gameObject, RE4Layers.Weapon);
            foreach (var r in _knife.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _knife.gameObject.SetActive(false);
        }

        private IEnumerator KnifeRoutine()
        {
            _lastKnifeTime = Time.time;
            bool hadWeapon = _vm != null;
            _hideWeapon = true;
            _knife.gameObject.SetActive(true);

            PlayLocal(ProceduralAudio.Swoosh(), 0.8f, 0.15f);
            bool hitDone = false;
            float dur = 0.38f;
            float t = 0f;
            bool fromRight = UnityEngine.Random.value > 0.5f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                float side = fromRight ? 1f : -1f;
                float sweep = Mathf.Lerp(0.35f * side, -0.35f * side, Mathf.SmoothStep(0f, 1f, k));
                _knife.localPosition = new Vector3(sweep, -0.12f + Mathf.Sin(k * Mathf.PI) * 0.06f, 0.35f);
                _knife.localRotation = Quaternion.Euler(10f, -sweep * 120f, 80f * side);
                if (!hitDone && k > 0.4f)
                {
                    hitDone = true;
                    KnifeHit();
                }
                yield return null;
            }

            _knife.gameObject.SetActive(false);
            _hideWeapon = false;
            if (hadWeapon) _equipBlend = 0.4f;
            _actionRoutine = null;
        }

        private void UpdateGuardPose()
        {
            if (_knife == null) return;
            if (IsGuarding && _actionRoutine == null)
            {
                _guardShown = true;
                _hideWeapon = true;
                _knife.gameObject.SetActive(true);
                _knife.localPosition = new Vector3(0.04f, -0.12f + Mathf.Sin(Time.time * 2f) * 0.004f, 0.32f);
                _knife.localRotation = Quaternion.Euler(-25f, -15f, 75f);
            }
            else if (_guardShown && _actionRoutine == null)
            {
                _guardShown = false;
                _hideWeapon = false;
                _knife.gameObject.SetActive(false);
                if (_vm != null) _equipBlend = 0.6f;
            }
        }

        /// <summary>
        /// Lo llaman los enemigos justo antes de golpear. Si Leon acaba de sacar el cuchillo (o esta en guardia)
        /// y el golpe viene de delante, se desvia: no hay dano y se gasta algo de cuchillo.
        /// </summary>
        public bool TryParry(Vector3 attackerPosition, bool heavyAttack, out bool perfect)
        {
            perfect = false;
            if (KnifeBroken || (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead)) return false;
            float since = Time.time - _knifePressTime;
            if (since > parryWindow && !IsGuarding) return false;
            Vector3 to = attackerPosition - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, to) > 80f) return false;

            perfect = since <= perfectParryWindow;
            knifeDurability = Mathf.Max(0f, knifeDurability - (perfect ? 3f : 10f) * (heavyAttack ? 2f : 1f));
            if (_cam != null) FXManager.Get().SurfaceImpact(_cam.transform.position + _cam.transform.forward * 0.55f - _cam.transform.up * 0.08f, -_cam.transform.forward);
            PlayLocal(ProceduralAudio.Click(3200f, 50f), 0.9f, 0.05f);
            PlayLocal(ProceduralAudio.Impact(1.6f), 0.6f, 0.05f);
            if (_fps != null) _fps.AddShake(perfect ? 0.35f : 0.2f);
            _recoilRotVel += new Vector3(-40f, 0f, 30f);
            OnParry?.Invoke(perfect);
            if (KnifeBroken)
            {
                PlayerInteractor pi = GetComponent<PlayerInteractor>();
                if (pi != null) pi.ShowMessage("¡Se ha roto el cuchillo!");
            }
            return true;
        }

        private IEnumerator StealthKillRoutine(ZombieAI target)
        {
            _lastKnifeTime = Time.time;
            if (_fps != null) _fps.PushInputLock();
            _hideWeapon = true;
            _knife.gameObject.SetActive(true);
            PlayLocal(ProceduralAudio.Swoosh(), 0.9f, 0.1f);
            float t = 0f;
            while (t < 0.45f)
            {
                t += Time.deltaTime;
                float k = t / 0.45f;
                _knife.localPosition = new Vector3(0.02f, Mathf.Lerp(0.05f, -0.15f, k), Mathf.Lerp(0.25f, 0.5f, k));
                _knife.localRotation = Quaternion.Euler(Mathf.Lerp(-60f, 40f, k), 0f, 90f);
                yield return null;
            }
            if (target != null && !target.IsDead)
            {
                ZombieHealth h = target.GetComponent<ZombieHealth>();
                if (h != null)
                {
                    var info = new DamageInfo
                    {
                        amount = 99999f,
                        kind = DamageKind.Knife,
                        part = BodyPart.Head,
                        point = target.transform.position + Vector3.up * 1.6f,
                        direction = transform.forward,
                        staggerPower = 1f,
                        source = gameObject
                    };
                    h.ApplyDamage(info);
                    FXManager.Get().FleshImpact(info.point, transform.forward, true);
                    ProceduralAudio.PlayAt(ProceduralAudio.Impact(1f), info.point, 0.8f);
                    OnHitConfirmed?.Invoke(true, true);
                }
                knifeDurability = Mathf.Max(0f, knifeDurability - 5f);
            }
            if (_fps != null) { _fps.AddShake(0.25f); _fps.PopInputLock(); }
            yield return new WaitForSeconds(0.15f);
            _knife.gameObject.SetActive(false);
            _hideWeapon = false;
            if (_vm != null) _equipBlend = 0.4f;
            _actionRoutine = null;
        }

        private void KnifeHit()
        {
            if (_cam == null) return;
            Collider[] cols = Physics.OverlapSphere(_cam.transform.position + _cam.transform.forward * knifeRange * 0.6f, knifeRange * 0.7f, ~0, QueryTriggerInteraction.Collide);
            ZombieHitbox best = null;
            float bestScore = float.MaxValue;
            foreach (var c in cols)
            {
                ZombieHitbox hb = c.GetComponent<ZombieHitbox>();
                if (hb == null || hb.health == null || hb.health.IsDead) continue;
                Vector3 to = c.bounds.center - _cam.transform.position;
                float angle = Vector3.Angle(_cam.transform.forward, to);
                if (angle > 70f) continue;
                float score = to.magnitude + angle * 0.01f + (hb.part == BodyPart.Head ? -0.2f : 0f);
                if (score < bestScore) { bestScore = score; best = hb; }
            }

            if (best == null)
            {
                foreach (var c in cols)
                {
                    Breakable br = c.GetComponentInParent<Breakable>();
                    if (br == null) continue;
                    br.Hit(knifeDamage, c.bounds.center, _cam.transform.forward);
                    knifeDurability = Mathf.Max(0f, knifeDurability - 0.5f);
                    return;
                }
                if (Physics.Raycast(_cam.transform.position, _cam.transform.forward, out RaycastHit wall, knifeRange, RE4Layers.ShotMask, QueryTriggerInteraction.Ignore))
                {
                    FXManager.Get().SurfaceImpact(wall.point, wall.normal);
                    PlayLocal(ProceduralAudio.Click(3000f, 120f), 0.5f);
                }
                return;
            }

            ZombieAI ai = best.health.GetComponent<ZombieAI>();
            float dmg = knifeDamage * (ai != null && ai.IsDowned ? 2.5f : 1f) * (best.part == BodyPart.Head ? 1.5f : 1f);
            var info = new DamageInfo
            {
                amount = dmg,
                kind = DamageKind.Knife,
                part = best.part,
                point = best.transform.position,
                direction = _cam.transform.forward,
                staggerPower = 0.35f,
                knockback = 0.2f,
                source = gameObject
            };
            bool killed = best.health.ApplyDamage(info);
            knifeDurability = Mathf.Max(0f, knifeDurability - 1f);
            FXManager.Get().FleshImpact(best.transform.position, _cam.transform.forward, false);
            ProceduralAudio.PlayAt(ProceduralAudio.Impact(0.7f), best.transform.position, 0.7f);
            OnHitConfirmed?.Invoke(killed, best.part == BodyPart.Head);
            if (_fps != null) _fps.AddShake(0.1f);
        }

        private void UpdateKickTarget()
        {
            StealthTarget = null;
            if (_cam != null && !KnifeBroken)
            {
                float bestStealth = stealthKillRange;
                foreach (var z in ZombieAI.All)
                {
                    if (z == null || !z.CanBeStealthKilled) continue;
                    Vector3 toZ = z.transform.position - transform.position;
                    toZ.y = 0f;
                    float d = toZ.magnitude;
                    if (d > bestStealth) continue;
                    if (Vector3.Angle(transform.forward, toZ) > 45f) continue;
                    // Leon tiene que estar a su espalda
                    if (Vector3.Angle(z.transform.forward, -toZ) < 110f) continue;
                    bestStealth = d;
                    StealthTarget = z;
                }
            }

            KickTarget = null;
            if (_cam == null || ZombieAI.All.Count == 0) return;
            float best = kickRange;
            foreach (var z in ZombieAI.All)
            {
                if (z == null || !z.CanBeKicked) continue;
                Vector3 to = z.transform.position - transform.position;
                to.y = 0f;
                float d = to.magnitude;
                if (d > best) continue;
                if (Vector3.Angle(transform.forward, to) > 55f) continue;
                best = d;
                KickTarget = z;
            }
        }

        private IEnumerator KickRoutine(ZombieAI target)
        {
            if (_fps != null) _fps.PushInputLock();
            bool hadWeapon = _vm != null;

            // Preparacion: bajar arma y girar camara (patada giratoria)
            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.deltaTime;
                _animPosTarget = new Vector3(0f, -0.35f, -0.1f) * (t / 0.18f);
                if (_fps != null) _fps.SetRoll(-12f * (t / 0.18f));
                yield return null;
            }
            PlayLocal(ProceduralAudio.Swoosh(), 1f, 0.1f);

            // Impacto
            Vector3 center = transform.position + transform.forward * 1.4f;
            if (target != null) center = Vector3.Lerp(center, target.transform.position, 0.5f);
            ProceduralAudio.PlayAt(ProceduralAudio.Impact(2f), center + Vector3.up, 1f);
            if (_fps != null)
            {
                _fps.AddShake(0.55f);
                _fps.SetRoll(18f);
            }

            foreach (var z in new List<ZombieAI>(ZombieAI.All))
            {
                if (z == null || z.IsDead) continue;
                Vector3 to = z.transform.position - transform.position;
                to.y = 0f;
                if (to.magnitude > kickRadius) continue;
                if (z != target && Vector3.Angle(transform.forward, to) > 100f) continue;

                ZombieHealth h = z.GetComponent<ZombieHealth>();
                var info = new DamageInfo
                {
                    amount = z == target ? kickDamage : kickDamage * 0.5f,
                    kind = DamageKind.Kick,
                    part = BodyPart.Body,
                    point = z.transform.position + Vector3.up,
                    direction = to.normalized,
                    staggerPower = 1f,
                    knockback = 1f,
                    source = gameObject
                };
                bool killed = h != null && h.ApplyDamage(info);
                if (!killed) z.Knockdown(to.normalized * 3.5f);
                OnHitConfirmed?.Invoke(killed, false);
            }

            yield return new WaitForSeconds(0.25f);
            t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                _animPosTarget = Vector3.Lerp(new Vector3(0f, -0.35f, -0.1f), Vector3.zero, t / 0.25f);
                yield return null;
            }
            if (_fps != null)
            {
                _fps.SetRoll(0f);
                _fps.PopInputLock();
            }
            _animPosTarget = Vector3.zero;
            if (hadWeapon) _equipBlend = 0.5f;
            _actionRoutine = null;
        }

        // ================================================================= Animacion procedimental

        private void Animate()
        {
            if (_pivot == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            WeaponDefinition w = Current;

            float aimTime = w != null ? Mathf.Max(0.05f, w.aimTime) : 0.2f;
            _aimBlend = Mathf.MoveTowards(_aimBlend, IsAiming ? 1f : 0f, dt / aimTime);
            float aimK = Mathf.SmoothStep(0f, 1f, _aimBlend);

            bool sprint = _fps != null && _fps.IsSprinting && !IsAiming && !IsReloading;
            _sprintBlend = Mathf.MoveTowards(_sprintBlend, sprint ? 1f : 0f, dt * 5f);
            _equipBlend = Mathf.MoveTowards(_equipBlend, 1f, dt * 3.2f);

            // Muelle del retroceso
            const float stiffness = 180f, damping = 16f;
            _recoilPosVel += (-_recoilPos * stiffness - _recoilPosVel * damping) * dt;
            _recoilPos += _recoilPosVel * dt;
            _recoilRotVel += (-_recoilRot * stiffness - _recoilRotVel * damping) * dt;
            _recoilRot += _recoilRotVel * dt;

            _animPos = Vector3.Lerp(_animPos, _animPosTarget, dt * 14f);
            _animRot = Vector3.Lerp(_animRot, _animRotTarget, dt * 14f);

            // Balanceo por mirada
            Vector2 look = _fps != null ? _fps.LookDelta : Vector2.zero;
            _swaySmoothed = Vector2.Lerp(_swaySmoothed, look, dt * 8f);
            float swayScale = IsAiming ? 0.25f : 1f;
            Vector3 swayPos = new Vector3(-_swaySmoothed.x * 0.0025f, -_swaySmoothed.y * 0.0025f, 0f) * swayScale;
            Vector3 swayRot = new Vector3(_swaySmoothed.y * 0.6f, -_swaySmoothed.x * 0.6f, _swaySmoothed.x * 0.9f) * swayScale;

            // Respiracion y paso
            float breathe = Mathf.Sin(Time.time * 1.6f) * 0.003f * (1f - aimK * 0.8f);
            float phase = _fps != null ? _fps.BobPhase : 0f;
            float moveK = _fps != null ? Mathf.Clamp01(new Vector3(_fps.Velocity.x, 0f, _fps.Velocity.z).magnitude / Mathf.Max(0.1f, _fps.walkSpeed)) : 0f;
            float bobMul = (1f - aimK * 0.85f) * (1f + _sprintBlend * 0.8f);
            Vector3 bob = new Vector3(Mathf.Sin(phase * 0.5f) * 0.012f, -Mathf.Abs(Mathf.Cos(phase * 0.5f)) * 0.01f, 0f) * moveK * bobMul;
            Vector3 bobRot = new Vector3(Mathf.Abs(Mathf.Cos(phase * 0.5f)) * 1.5f, 0f, Mathf.Sin(phase * 0.5f) * 1.6f) * moveK * bobMul;

            Vector3 hip = w != null ? w.hipPosition : new Vector3(0.18f, -0.17f, 0.38f);
            Vector3 aim = w != null ? w.aimPosition : hip;
            // Alinear las miras del arma con el centro de la pantalla
            if (w != null && _vm != null && _vm.hasSight) aim = new Vector3(-_vm.sight.x, -_vm.sight.y, w.aimPosition.z);
            Vector3 sprintRot = w != null ? w.sprintEuler : new Vector3(18f, -38f, 12f);

            Vector3 pos = Vector3.Lerp(hip, aim, aimK);
            pos += new Vector3(-0.04f, -0.05f, -0.03f) * _sprintBlend;
            pos += new Vector3(0f, -0.35f, -0.05f) * (1f - _equipBlend);
            // Pulso cansado: el arma se mueve al ritmo del jadeo (visible al apuntar)
            float tired = _fps != null ? _fps.Fatigue : 0f;
            Vector3 tiredSway = new Vector3(Mathf.Sin(Time.time * 2.7f) * 0.006f, Mathf.Sin(Time.time * 3.4f + 1f) * 0.008f, 0f) * tired;
            pos += swayPos + bob + new Vector3(0f, breathe, 0f) + tiredSway + _animPos + _recoilPos * 0.01f;

            Vector3 rot = sprintRot * _sprintBlend + new Vector3(35f, -10f, 0f) * (1f - _equipBlend) + swayRot + bobRot + _animRot + _recoilRot * 0.01f;

            _pivot.localPosition = pos;
            _pivot.localRotation = Quaternion.Euler(rot);

            // Zoom al apuntar
            if (_fps != null)
            {
                _fps.TargetFovOverride = w != null && _aimBlend > 0.01f ? Mathf.Lerp(_fps.baseFov, w.aimFov, aimK) : 0f;
            }

            // En mira telescopica ocultar el arma
            if (_vm != null) _vm.root.gameObject.SetActive(!IsScoped && !_hideWeapon);

            AnimateParts(dt, w);
        }

        private void AnimateParts(float dt, WeaponDefinition w)
        {
            if (_vm == null || w == null) return;
            float back = Mathf.Max(0.04f, Mathf.Min(0.09f, WeaponStats.FireInterval(w, CurrentEntry) * 0.6f));
            _slideKick = Mathf.MoveTowards(_slideKick, 0f, dt / back);
            if (_vm.slide != null)
            {
                float k = _slideLocked ? 1f : Mathf.Max(_slideManual, Mathf.Sin(_slideKick * Mathf.PI * 0.5f));
                _vm.slide.localPosition = _vm.slideBase + _vm.slideBackLocal * k;
            }
            if (_vm.cylinder != null)
            {
                _cylAngle = Mathf.MoveTowards(_cylAngle, _cylAngleTarget, dt * 900f);
                _vm.cylinder.localRotation = Quaternion.AngleAxis(_cylAngle, _vm.cylAxisLocal) * _vm.cylBaseRot;
                _vm.cylinder.localPosition = _vm.cylBasePos + _vm.cylOutLocal * _cylOut;
            }
            if (_vm.magazine != null)
            {
                _vm.magazine.localPosition = _vm.magBase + _vm.magOutLocal * _magOut;
                bool visible = _magOut < 0.97f;
                if (_vm.magazine.gameObject.activeSelf != visible) _vm.magazine.gameObject.SetActive(visible);
            }
        }

        private void BuildLaser()
        {
            Material laserMat = FXManager.Get().library != null ? FXManager.Get().library.laser : null;
            Material dotMat = FXManager.Get().library != null ? FXManager.Get().library.laserDot : null;

            GameObject lgo = new GameObject("LaserSight");
            _laser = lgo.AddComponent<LineRenderer>();
            _laser.positionCount = 2;
            _laser.startWidth = 0.004f;
            _laser.endWidth = 0.002f;
            _laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _laser.receiveShadows = false;
            if (laserMat != null) _laser.sharedMaterial = laserMat;
            else
            {
                _laser.material = new Material(Shader.Find("Sprites/Default"));
                _laser.material.color = new Color(1f, 0f, 0f, 0.6f);
            }
            _laser.enabled = false;

            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "LaserDot";
            Destroy(dot.GetComponent<Collider>());
            dot.layer = 2;
            dot.transform.localScale = Vector3.one * 0.03f;
            Renderer dr = dot.GetComponent<Renderer>();
            dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (dotMat != null) dr.sharedMaterial = dotMat;
            else dr.material.color = Color.red;
            _laserDot = dot.transform;
            dot.SetActive(false);
        }

        private void UpdateLaser()
        {
            if (_laser == null || _cam == null) return;
            WeaponDefinition w = Current;
            bool show = w != null && _vm != null && _aimBlend > 0.6f && !IsReloading && !IsScoped
                        && (w.weaponClass == WeaponClass.Handgun || w.weaponClass == WeaponClass.Shotgun || w.weaponClass == WeaponClass.SMG);
            _laser.enabled = show;
            _laserDot.gameObject.SetActive(show || IsScoped);
            if (!show && !IsScoped) return;

            Vector3 origin = _cam.transform.position;
            Vector3 dir = _cam.transform.forward;
            Vector3 end = origin + dir * 60f;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, 60f, RE4Layers.ShotMask, QueryTriggerInteraction.Collide))
            {
                end = hit.point;
            }
            if (show)
            {
                _laser.SetPosition(0, _vm.muzzle.position);
                _laser.SetPosition(1, end);
            }
            _laserDot.position = end - dir * 0.02f;
            float dist = Vector3.Distance(origin, end);
            _laserDot.localScale = Vector3.one * Mathf.Clamp(dist * 0.006f, 0.012f, 0.12f);
        }

        // ================================================================= Audio

        /// <summary>Sonido grabado del arma si existe; si no, el procedimental.</summary>
        private void Snd(AudioClip recorded, AudioClip procedural, float volume)
        {
            if (recorded != null) PlayClip(recorded, volume, Current != null ? Current.clipPitch : 1f, 0.03f);
            else PlayLocal(procedural, volume);
        }

        private void PlayClip(AudioClip clip, float volume, float pitch, float pitchVar)
        {
            if (clip == null || audioSource == null) return;
            audioSource.pitch = pitch * (1f + UnityEngine.Random.Range(-pitchVar, pitchVar));
            audioSource.PlayOneShot(clip, volume);
        }

        private void PlayLocal(AudioClip clip, float volume, float pitchVar = 0.04f)
        {
            if (clip == null || audioSource == null) return;
            audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchVar, pitchVar);
            audioSource.PlayOneShot(clip, volume);
        }
    }
}
