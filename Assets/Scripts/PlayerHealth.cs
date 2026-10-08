using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Gameplay.RE4;

namespace Gameplay
{
    public enum HealthStatus
    {
        Fine,
        Caution,
        Danger
    }

    /// <summary>
    /// Vida de Leon al estilo RE4: estados FINE / CAUTION / DANGER, agarres con QTE
    /// (pulsar repetidamente para liberarse), curacion por hierbas y pantalla de muerte.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        [Header("Vida")]
        public float maxHealth = 100f;
        public float currentHealth = 100f;
        public float invulnerableAfterHit = 0.3f;

        [Header("Agarre (QTE)")]
        public float grabDamagePerSecond = 6f;
        public float grabTimeout = 3.5f;
        public float grabBiteDamage = 22f;
        public float mashPerPress = 0.13f;
        public float mashDecay = 0.35f;

        public bool IsDead => currentHealth <= 0f;
        public bool IsGrabbed => _grabber != null;
        public float GrabProgress { get; private set; }
        public float GrabTimeLeft => Mathf.Max(0f, grabTimeout - _grabTime);
        public float Normalized => Mathf.Clamp01(currentHealth / maxHealth);
        public float LastHitTime { get; private set; } = -10f;
        public float DeathTime { get; private set; }

        public HealthStatus Status
        {
            get
            {
                float n = Normalized;
                if (n > 0.6f) return HealthStatus.Fine;
                if (n > 0.3f) return HealthStatus.Caution;
                return HealthStatus.Danger;
            }
        }

        private ZombieAI _grabber;
        private float _grabTime;
        private float _grabTick;
        private float _heartTimer;
        private FirstPersonController _fps;

        private void Awake()
        {
            Instance = this;
            currentHealth = maxHealth;
            _fps = GetComponent<FirstPersonController>();
        }

        public void TakeDamage(float amount) => TakeDamage(amount, transform.position + transform.forward);

        public void TakeDamage(float amount, Vector3 fromPosition)
        {
            if (IsDead || amount <= 0f) return;
            if (Time.time - LastHitTime < invulnerableAfterHit && !IsGrabbed) return;

            LastHitTime = Time.time;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            // Como en RE4: un golpe fuerte hace tambalear a Leon un instante
            if (amount >= 12f && !IsGrabbed && currentHealth > 0f && !_staggered) StartCoroutine(HitStagger(Mathf.Clamp(amount / 60f, 0.25f, 0.55f)));

            if (_fps != null)
            {
                _fps.AddShake(Mathf.Clamp(amount / 30f, 0.25f, 0.8f));
                Vector3 push = (transform.position - fromPosition);
                push.y = 0f;
                _fps.Push(push.normalized * 2f);
            }
            ProceduralAudio.PlayAt(ProceduralAudio.Impact(1.2f), transform.position + Vector3.up, 0.8f);

            if (currentHealth <= 0f) Die();
        }

        private bool _staggered;

        private System.Collections.IEnumerator HitStagger(float seconds)
        {
            _staggered = true;
            if (_fps != null) _fps.PushInputLock();
            yield return new WaitForSeconds(seconds);
            if (_fps != null) _fps.PopInputLock();
            _staggered = false;
        }

        /// <summary>Cura un porcentaje de la vida maxima.</summary>
        public bool HealPercent(float percent)
        {
            if (IsDead || currentHealth >= maxHealth) return false;
            currentHealth = Mathf.Min(maxHealth, currentHealth + maxHealth * percent);
            ProceduralAudio.PlayAt(ProceduralAudio.Pickup(), transform.position, 0.5f, 0f);
            return true;
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);
        }

        // ------------------------------------------------------------------ Agarre

        public bool TryBeginGrab(ZombieAI grabber)
        {
            if (IsDead || IsGrabbed || grabber == null) return false;
            _grabber = grabber;
            _grabTime = 0f;
            _grabTick = 0f;
            GrabProgress = 0f;
            if (_fps != null)
            {
                _fps.PushInputLock();
                _fps.ForceLookAt(grabber.transform);
                _fps.AddShake(0.5f);
            }
            return true;
        }

        /// <summary>El enemigo suelta al jugador (por ejemplo, le dispararon). No avisa de vuelta al enemigo.</summary>
        public void ReleaseGrab()
        {
            if (_grabber == null) return;
            _grabber = null;
            if (_fps != null)
            {
                _fps.PopInputLock();
                _fps.ForceLookAt(null);
            }
        }

        private void EndGrab(bool brokeFree)
        {
            ZombieAI g = _grabber;
            _grabber = null;
            if (_fps != null)
            {
                _fps.PopInputLock();
                _fps.ForceLookAt(null);
            }
            if (g != null) g.OnGrabReleased(brokeFree);
            if (brokeFree && _fps != null) _fps.AddShake(0.35f);
        }

        private void Update()
        {
            if (IsDead)
            {
                bool retry = (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.rKey.wasPressedThisFrame))
                             || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
                if (retry && Time.unscaledTime - DeathTime > 1.5f)
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0 ? SceneManager.GetActiveScene().buildIndex : 0);
                }
                return;
            }

            if (IsGrabbed) UpdateGrab();

            if (Status == HealthStatus.Danger)
            {
                _heartTimer -= Time.deltaTime;
                if (_heartTimer <= 0f)
                {
                    _heartTimer = 0.9f;
                    ProceduralAudio.PlayAt(ProceduralAudio.Heartbeat(), transform.position, 0.45f, 0f);
                }
            }
        }

        private void UpdateGrab()
        {
            if (_grabber == null || _grabber.IsDead)
            {
                EndGrab(true);
                return;
            }

            _grabTime += Time.deltaTime;
            GrabProgress = Mathf.Max(0f, GrabProgress - mashDecay * Time.deltaTime);

            bool mash = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame))
                        || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                        || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
            if (mash)
            {
                GrabProgress += mashPerPress;
                if (_fps != null) _fps.AddShake(0.12f);
            }

            if (GrabProgress >= 1f)
            {
                EndGrab(true);
                return;
            }

            _grabTick += Time.deltaTime;
            if (_grabTick >= 1f)
            {
                _grabTick = 0f;
                currentHealth = Mathf.Max(0f, currentHealth - grabDamagePerSecond);
                LastHitTime = Time.time;
                if (currentHealth <= 0f) { Die(); return; }
            }

            if (_grabTime >= grabTimeout)
            {
                // Mordisco final
                ZombieAI g = _grabber;
                EndGrab(false);
                TakeDamageIgnoringInvul(grabBiteDamage, g != null ? g.transform.position : transform.position);
            }
        }

        private void TakeDamageIgnoringInvul(float amount, Vector3 from)
        {
            LastHitTime = -10f;
            TakeDamage(amount, from);
        }

        private void Die()
        {
            if (IsGrabbed)
            {
                ZombieAI g = _grabber;
                _grabber = null;
                if (g != null) g.OnGrabReleased(false);
            }
            currentHealth = 0f;
            DeathTime = Time.unscaledTime;
            Debug.Log("<color=red>[PlayerHealth] Leon ha muerto.</color>");
            if (_fps != null)
            {
                _fps.PushInputLock();
                _fps.SetRoll(70f);
                _fps.LockCursor(false);
                if (_fps.cameraHolder != null) StartCoroutine(FallCamera(_fps.cameraHolder));
            }
            ProceduralAudio.PlayAt(ProceduralAudio.Scream(), transform.position, 0.6f, 0f);
        }

        private System.Collections.IEnumerator FallCamera(Transform holder)
        {
            Vector3 start = holder.localPosition;
            Vector3 end = new Vector3(start.x, 0.35f, start.z - 0.3f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 1.4f;
                holder.localPosition = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }

        /// <summary>Restaura la salud de Leon al máximo permitido si sigue con vida.</summary>
        public bool IsCritical => !IsDead && Status == HealthStatus.Danger;

        public void FullHeal()
        {
            if (IsDead) return;
            currentHealth = maxHealth;
        }
    }
}
