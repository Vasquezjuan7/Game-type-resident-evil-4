using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Cuerpo de Leon en primera persona: no se ve (solo proyecta sombra), pero camina, corre, se desplaza
    /// de lado, recarga, dispara y reacciona a los golpes con las animaciones de Mixamo, asi su sombra en
    /// el suelo se mueve como el. Se crea solo desde Resources/RE4Body/LeonCuerpo (menu RE4 > Integrar animaciones nuevas).
    /// </summary>
    public class LeonBody : MonoBehaviour
    {
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");
        private static readonly int FireHash = Animator.StringToHash("Fire");
        private static readonly int HitHash = Animator.StringToHash("Hit");

        private FirstPersonController _fps;
        private WeaponSystem _weapons;
        private PlayerHealth _health;
        private Animator _anim;

        private bool _wasReloading;
        private int _lastLoaded = -1;
        private CaseEntry _lastEntry;
        private float _lastHealth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (PlayerHealth.Instance == null || PlayerHealth.Instance.GetComponent<LeonBody>() != null) return;
            if (Resources.Load<GameObject>("RE4Body/LeonCuerpo") == null) return;
            PlayerHealth.Instance.gameObject.AddComponent<LeonBody>();
        }

        private void Start()
        {
            _fps = GetComponent<FirstPersonController>();
            _weapons = GetComponent<WeaponSystem>();
            _health = GetComponent<PlayerHealth>();
            GameObject prefab = Resources.Load<GameObject>("RE4Body/LeonCuerpo");
            if (prefab == null) { enabled = false; return; }

            GameObject body = Instantiate(prefab, transform);
            body.name = "CuerpoSombra";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            // solo sombra: la camara en primera persona no ve el cuerpo por dentro
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                r.receiveShadows = false;
            }
            foreach (var c in body.GetComponentsInChildren<Collider>(true)) Destroy(c);
            _anim = body.GetComponentInChildren<Animator>();
            if (_anim != null)
            {
                _anim.applyRootMotion = false;
                _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            if (_health != null) _lastHealth = _health.currentHealth;
        }

        private void Update()
        {
            if (_anim == null || _fps == null) return;

            // velocidad en ejes locales de Leon: Y adelante/atras, X a los lados (1 = caminar, 2 = correr)
            Vector3 local = transform.InverseTransformDirection(_fps.Velocity);
            float unit = Mathf.Max(0.1f, _fps.walkSpeed);
            _anim.SetFloat(MoveXHash, Mathf.Clamp(local.x / unit, -1f, 1f), 0.1f, Time.deltaTime);
            float y = local.z / unit;
            if (y > 1f) y = 1f + Mathf.Clamp01((local.z - unit) / Mathf.Max(0.1f, _fps.runSpeed - unit)) * 1.2f;
            _anim.SetFloat(MoveYHash, Mathf.Clamp(y, -2f, 2.2f), 0.1f, Time.deltaTime);

            if (_weapons != null)
            {
                bool reloading = _weapons.IsReloading;
                if (reloading && !_wasReloading) _anim.SetTrigger(ReloadHash);
                _wasReloading = reloading;

                // un disparo = la municion cargada del arma equipada baja
                int loaded = _weapons.Loaded;
                if (_weapons.CurrentEntry == _lastEntry && !reloading && loaded < _lastLoaded) _anim.SetTrigger(FireHash);
                _lastLoaded = loaded;
                _lastEntry = _weapons.CurrentEntry;
            }

            if (_health != null)
            {
                if (_health.currentHealth < _lastHealth - 0.5f && !_health.IsDead) _anim.SetTrigger(HitHash);
                _lastHealth = _health.currentHealth;
            }
        }
    }
}
