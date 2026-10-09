using UnityEngine;

namespace Gameplay.RE4
{
    public enum WeaponClass
    {
        Handgun,
        Shotgun,
        SMG,
        Rifle,
        RocketLauncher,
        AssaultRifle
    }

    /// <summary>
    /// Estadisticas y presentacion de un arma. Todos los valores se pueden ajustar en el Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "RE4/Weapon Definition", fileName = "Weapon_")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Identidad")]
        public string id = "weapon";
        public string displayName = "Arma";
        public WeaponClass weaponClass = WeaponClass.Handgun;

        [Header("Modelo en primera persona")]
        public GameObject viewModelPrefab;
        [Tooltip("Largo deseado del arma en metros (se escala automaticamente).")]
        public float viewModelLength = 0.3f;
        [Tooltip("Rotacion extra aplicada al modelo para que el canon apunte hacia +Z.")]
        public Vector3 modelEulerOffset = Vector3.zero;
        public Vector3 hipPosition = new Vector3(0.18f, -0.17f, 0.38f);
        public Vector3 aimPosition = new Vector3(0f, -0.105f, 0.3f);
        public Vector3 sprintEuler = new Vector3(18f, -38f, 12f);
        [Tooltip("El prefab ya viene orientado (canon +Z, arriba +Y): no se calcula la orientacion automatica.")]
        public bool canonicalModel = false;

        [Header("Piezas animadas (nombres de hijos del prefab)")]
        [Tooltip("Corredera de la pistola o palanca de carga del rifle. Retrocede en cada disparo.")]
        public string slidePart = "";
        [Tooltip("Cargador: cae al recargar y entra uno nuevo.")]
        public string magazinePart = "";
        [Tooltip("Recorrido de la corredera como fraccion del largo del arma.")]
        public float slideTravel = 0.16f;
        [Tooltip("La corredera se queda atras al vaciar el cargador (como una pistola real).")]
        public bool slideLocksOnEmpty = true;
        [Tooltip("Tambor del revolver: gira en cada disparo y se abre al recargar.")]
        public string cylinderPart = "";
        public int cylinderChambers = 6;
        [Tooltip("Al apuntar se usa la mira telescopica a pantalla completa.")]
        public bool useScopeOverlay = false;

        [Header("Dano")]
        public float damage = 34f;
        public int pellets = 1;
        public float headMultiplier = 3f;
        public float limbMultiplier = 0.7f;
        [Range(0f, 1f)] public float staggerPower = 0.5f;
        public float knockback = 0.4f;
        [Tooltip("Cuantos enemigos puede atravesar la bala.")]
        public int penetration = 0;
        public float range = 80f;

        [Header("Explosion (lanzacohetes)")]
        public float explosionRadius = 0f;
        public float projectileSpeed = 45f;

        [Header("Cadencia y cargador")]
        public float fireInterval = 0.25f;
        public bool automatic = false;
        public int magazineSize = 10;
        public float reloadTime = 1.6f;
        [Tooltip("La escopeta recarga cartucho a cartucho.")]
        public bool reloadOneByOne = false;
        public ItemDefinition ammoItem;

        [Header("Precision")]
        public float hipSpread = 3.5f;
        public float aimSpread = 0.35f;
        public float aimFov = 52f;
        public float aimTime = 0.18f;

        [Header("Retroceso")]
        public float recoilKickBack = 0.06f;
        public float recoilPitch = 2.2f;
        public float recoilYaw = 0.6f;
        public float cameraPitchKick = 1.2f;
        [Tooltip("Accion de bombeo / cerrojo tras cada disparo.")]
        public bool cycleAfterShot = false;

        [Header("Mejoras (mercader)")]
        public int maxUpgradeLevel = 5;
        [Tooltip("Coste base en pesetas del primer nivel; cada nivel cuesta mas.")]
        public int upgradeBaseCost = 4000;
        [Tooltip("Dano extra por nivel (0.15 = +15%).")]
        public float powerPerLevel = 0.15f;
        [Tooltip("Cadencia extra por nivel (0.1 = +10% disparos/s).")]
        public float ratePerLevel = 0.1f;
        [Tooltip("Reduccion del tiempo de recarga por nivel.")]
        public float reloadPerLevel = 0.1f;
        [Tooltip("Balas extra en el cargador por nivel.")]
        public int capacityPerLevel = 2;

        [Header("Sonidos grabados (si faltan se usan los procedimentales)")]
        public AudioClip[] shotClips = new AudioClip[0];
        public AudioClip[] tailClips = new AudioClip[0];
        public AudioClip equipClip;
        public AudioClip aimClip;
        public AudioClip magOutClip;
        public AudioClip magDrawClip;
        public AudioClip magInClip;
        [Tooltip("Corredera / palanca de carga / bombeo hacia delante.")]
        public AudioClip rackClip;
        [Tooltip("Bombeo hacia atras (escopeta) o apertura (lanzacohetes).")]
        public AudioClip rackBackClip;
        public float clipPitch = 1f;

        [Header("Sonido procedimental")]
        public float shotPitch = 1f;
        public float shotBody = 1f;
        public float shotLength = 0.5f;
        public float noiseRadius = 45f;
    }
}
