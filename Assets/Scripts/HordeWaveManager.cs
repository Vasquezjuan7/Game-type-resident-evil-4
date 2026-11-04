using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Gameplay.RE4;
using Random = UnityEngine.Random;

namespace Gameplay
{
    /// <summary>
    /// Director del pueblo al estilo de la escena de la aldea de Resident Evil 4:
    /// 1) Los aldeanos trabajan/deambulan sin ver a Leon.
    /// 2) Cuando lo descubren comienza el asedio por oleadas desde los accesos del pueblo.
    /// 3) Tras la ultima oleada suena la campana de la iglesia y los aldeanos se retiran.
    /// Los enemigos sueltan objetos segun lo que necesita el jugador (municion de sus armas o hierbas).
    /// </summary>
    public class HordeWaveManager : MonoBehaviour
    {
        public static HordeWaveManager Instance { get; private set; }
        public static event Action<string> OnBanner;

        [Header("Enemigos")]
        public List<GameObject> zombiePrefabs = new List<GameObject>();
        public List<Transform> spawnPoints = new List<Transform>();
        public List<Transform> villagerPoints = new List<Transform>();

        [Header("Perros zombi")]
        public GameObject dogPrefab;
        public int[] dogsPerWave = { 0, 2, 3, 4, 6 };
        [Tooltip("Perros que merodean por el pueblo antes del asedio.")]
        public int prowlingDogs = 2;
        public float dogHealth = 60f;

        [Header("Jefe")]
        public GameObject bossPrefab;
        [Tooltip("Oleada en la que aparece (0 = la ultima).")]
        public int bossWave = 0;
        public float bossHealth = 2200f;
        public string bossName = "TYRANT";
        public ZombieHealth Boss { get; private set; }

        [Header("Dificultad")]
        [Tooltip("Multiplicador de vida de los aldeanos (1 = normal, 1.3 = dificil al estilo RE4).")]
        public float difficulty = 1.3f;

        [Header("Oleadas")]
        public int[] zombiesPerWave = { 8, 12, 16, 22, 28 };
        public int maxAlive = 16;
        public float timeBetweenWaves = 9f;
        public float minSpawnDistance = 22f;
        public float baseHealth = 110f;
        public float healthPerWave = 15f;

        [Header("Botin dinamico")]
        public List<ItemDefinition> ammoDrops = new List<ItemDefinition>();
        public ItemDefinition greenHerb;
        public ItemDefinition redHerb;
        [Range(0f, 1f)] public float dropChance = 0.5f;
        public ItemDefinition pesetasItem;
        public ItemDefinition gunpowder, resourceS, resourceL;
        public List<ItemDefinition> treasures = new List<ItemDefinition>();
        [Range(0f, 1f)] public float pesetasChance = 0.55f;

        public int CurrentWave { get; private set; }
        public int MaxWaves => zombiesPerWave != null ? zombiesPerWave.Length : 0;
        public int Alive { get; private set; }
        public int RemainingToSpawn { get; private set; }
        public int Remaining => Alive + RemainingToSpawn + (_bossPending ? 1 : 0);
        private bool _bossPending;
        public bool SiegeActive { get; private set; }
        public bool Victory { get; private set; }
        public int Kills { get; private set; }

        private bool _waveRunning;
        private Transform _player;

        private void Awake()
        {
            Instance = this;
            // mas presion y botin algo mas escaso (los valores guardados en la escena eran los antiguos)
            maxAlive = Mathf.Max(maxAlive, 18);
            dropChance = Mathf.Min(dropChance, 0.42f);
        }

        private void Start()
        {
            if (PlayerHealth.Instance != null) _player = PlayerHealth.Instance.transform;
            SpawnVillagers();
            StartCoroutine(WatchForDiscovery());
        }

        public static void Banner(string message) => OnBanner?.Invoke(message);

        // ------------------------------------------------------------ Fase 1: aldea tranquila

        private void SpawnVillagers()
        {
            foreach (var p in villagerPoints)
            {
                if (p == null) continue;
                ZombieAI ai = SpawnZombie(p.position, p.rotation, 0, false);
                if (ai != null) Alive++;
            }
            for (int i = 0; i < prowlingDogs && villagerPoints.Count > 0; i++)
            {
                Transform p = villagerPoints[Random.Range(0, villagerPoints.Count)];
                if (p != null && SpawnDog(p.position + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f)), p.rotation, 0, false)) Alive++;
            }
        }

        private IEnumerator WatchForDiscovery()
        {
            yield return new WaitForSeconds(1f);
            Banner("PUEBLO\n<size=20>Algo no está bien con estos aldeanos...</size>");
            while (!SiegeActive)
            {
                foreach (var z in ZombieAI.All)
                {
                    if (z != null && z.State != GanadoState.Idle && z.State != GanadoState.Dead)
                    {
                        StartSiege();
                        break;
                    }
                }
                foreach (var d in ZombieDogAI.All)
                {
                    if (!SiegeActive && d != null && d.State != DogState.Idle && !d.IsDead) StartSiege();
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        private void StartSiege()
        {
            if (SiegeActive) return;
            SiegeActive = true;
            Banner("¡TE HAN DESCUBIERTO!\n<size=22>¡Los aldeanos vienen por ti!</size>");
            foreach (var z in ZombieAI.All) if (z != null && z.State == GanadoState.Idle) z.BecomeAlerted(false);
            foreach (var d in new List<ZombieDogAI>(ZombieDogAI.All)) if (d != null) d.Alert();
            StartCoroutine(NextWave(4f));
        }

        // ------------------------------------------------------------ Fase 2: asedio

        private IEnumerator NextWave(float delay)
        {
            yield return new WaitForSeconds(delay);
            CurrentWave++;
            int total = zombiesPerWave[Mathf.Clamp(CurrentWave - 1, 0, zombiesPerWave.Length - 1)];
            int dogs = dogPrefab != null && dogsPerWave != null && dogsPerWave.Length > 0 ? dogsPerWave[Mathf.Clamp(CurrentWave - 1, 0, dogsPerWave.Length - 1)] : 0;
            int dogsLeft = dogs;
            RemainingToSpawn = total + dogs;
            _waveRunning = true;
            string dogText = dogs > 0 ? $" y {dogs} perros" : "";
            Banner($"OLEADA {CurrentWave} / {MaxWaves}\n<size=22>{total} aldeanos{dogText} se acercan</size>");
            if (bossPrefab != null && CurrentWave == (bossWave > 0 ? bossWave : MaxWaves)) StartCoroutine(SpawnBoss(6f));
            ProceduralAudio.PlayAt(ProceduralAudio.Scream(), _player != null ? _player.position + _player.forward * 20f : transform.position, 0.6f, 0.1f, 80f);

            while (RemainingToSpawn > 0)
            {
                if (Alive < maxAlive)
                {
                    int pack = Mathf.Min(Random.Range(2, 5), RemainingToSpawn);
                    Transform sp = PickSpawnPoint();
                    for (int i = 0; i < pack; i++)
                    {
                        // repartidos: cada uno puede salir de otra entrada y con mas separacion (no en bloque)
                        if (i > 0 && Random.value < 0.5f) sp = PickSpawnPoint();
                        Vector2 r = Random.insideUnitCircle * 6f;
                        Vector3 pos = sp != null ? sp.position + new Vector3(r.x, 0f, r.y) : transform.position;
                        Quaternion rot = sp != null ? sp.rotation : Quaternion.identity;
                        bool dog = dogsLeft > 0 && Random.value < (float)dogsLeft / Mathf.Max(1, RemainingToSpawn);
                        RemainingToSpawn--;
                        if (dog)
                        {
                            dogsLeft--;
                            if (SpawnDog(pos, rot, CurrentWave, true)) Alive++;
                            continue;
                        }
                        ZombieAI ai = SpawnZombie(pos, rot, CurrentWave, true);
                        if (ai != null) Alive++;
                    }
                }
                yield return new WaitForSeconds(Random.Range(2f, 4f));
            }
        }

        private Transform PickSpawnPoint()
        {
            if (spawnPoints.Count == 0) return null;
            var candidates = new List<Transform>();
            foreach (var sp in spawnPoints)
            {
                if (sp == null) continue;
                if (_player == null || Vector3.Distance(sp.position, _player.position) >= minSpawnDistance) candidates.Add(sp);
            }
            if (candidates.Count == 0) candidates.AddRange(spawnPoints);
            return candidates[Random.Range(0, candidates.Count)];
        }

        private ZombieAI SpawnZombie(Vector3 position, Quaternion rotation, int wave, bool alerted)
        {
            if (zombiePrefabs.Count == 0) return null;
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                Debug.LogWarning("[HordeWaveManager] Punto de aparicion fuera de la NavMesh: " + position);
                return null;
            }

            GameObject prefab = zombiePrefabs[Random.Range(0, zombiePrefabs.Count)];
            GameObject z = Instantiate(prefab, hit.position, Quaternion.Euler(0f, rotation.eulerAngles.y + Random.Range(-40f, 40f), 0f));
            z.name = wave > 0 ? $"Ganado_W{wave}" : "Ganado_Aldeano";

            ZombieAI ai = z.GetComponent<ZombieAI>();
            ZombieHealth hp = z.GetComponent<ZombieHealth>();
            if (ai == null || hp == null)
            {
                Debug.LogWarning("[HordeWaveManager] El prefab no tiene ZombieAI/ZombieHealth. Ejecuta 'RE4 > Construir Pueblo'.");
                Destroy(z);
                return null;
            }

            // Dificultad al estilo RE4: aguantan mas balas, pegan mas fuerte y algunos atacan a distancia
            hp.SetMaxHealth((baseHealth + healthPerWave * Mathf.Max(0, wave - 1)) * difficulty + Random.Range(-15f, 15f));
            ai.startAlerted = alerted;
            ai.isRunner = alerted && Random.value < 0.16f + wave * 0.06f;
            ai.attackDamage = (12f + wave * 2f) * Mathf.Lerp(1f, difficulty, 0.6f);
            ai.grabChance = 0.35f;
            ai.thrower = Random.value < 0.22f + wave * 0.04f;
            ai.dynamite = ai.thrower && wave >= 2 && Random.value < 0.3f;
            ai.throwDamage = 13f + wave * 1.5f;
            ai.meleeWeapon = !ai.thrower && Random.value < 0.4f;
            ai.carriesTorch = Random.value < 0.3f;
            ai.dodgeChance = Mathf.Clamp01(0.25f + wave * 0.05f);
            return ai;
        }

        private IEnumerator SpawnBoss(float delay)
        {
            _bossPending = true;
            yield return new WaitForSeconds(delay);
            _bossPending = false;
            Transform sp = PickSpawnPoint();
            Vector3 pos = sp != null ? sp.position : transform.position;
            if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, 8f, NavMesh.AllAreas)) yield break;
            GameObject b = Instantiate(bossPrefab, hit.position, sp != null ? sp.rotation : Quaternion.identity);
            b.name = bossName;
            ZombieAI ai = b.GetComponent<ZombieAI>();
            ZombieHealth hp = b.GetComponent<ZombieHealth>();
            if (ai == null || hp == null) { Destroy(b); yield break; }
            hp.SetMaxHealth(bossHealth);
            hp.dropChance = 1f;
            ai.startAlerted = true;
            Boss = hp;
            Alive++;
            Banner($"¡{bossName}!\n<size=22>Algo enorme se acerca... apunta a la cabeza</size>");
            ProceduralAudio.PlayAt(ProceduralAudio.Scream(), hit.position + Vector3.up * 2f, 1f, 0f, 120f);
        }

        private bool SpawnDog(Vector3 position, Quaternion rotation, int wave, bool alerted)
        {
            if (dogPrefab == null) return false;
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 8f, NavMesh.AllAreas)) return false;
            GameObject d = Instantiate(dogPrefab, hit.position, Quaternion.Euler(0f, rotation.eulerAngles.y + Random.Range(-60f, 60f), 0f));
            d.name = wave > 0 ? $"PerroZombi_W{wave}" : "PerroZombi";
            ZombieDogAI ai = d.GetComponent<ZombieDogAI>();
            ZombieHealth hp = d.GetComponent<ZombieHealth>();
            if (ai == null || hp == null)
            {
                Destroy(d);
                return false;
            }
            hp.SetMaxHealth(dogHealth + 8f * Mathf.Max(0, wave - 1));
            ai.startAlerted = alerted;
            ai.biteDamage += wave * 0.8f;
            ai.pounceDamage += wave * 1.2f;
            return true;
        }

        public void NotifyZombieKilled(ZombieHealth zombie, Vector3 position)
        {
            Alive = Mathf.Max(0, Alive - 1);
            Kills++;
            TryDrop(position);
            if (zombie != null && zombie == Boss)
            {
                // el jefe suelta un buen botin
                if (pesetasItem != null) ItemPickup.Spawn(pesetasItem, 15000, position + Vector3.up * 0.3f);
                Banner($"{bossName} ABATIDO\n<size=22>+15.000 pesetas</size>");
            }

            if (SiegeActive && _waveRunning && RemainingToSpawn <= 0 && Alive <= 0 && !_bossPending)
            {
                _waveRunning = false;
                if (CurrentWave >= MaxWaves) StartCoroutine(VictoryRoutine());
                else
                {
                    Banner($"OLEADA {CurrentWave} SUPERADA\n<size=22>Recarga y prepárate...</size>");
                    StartCoroutine(NextWave(timeBetweenWaves));
                }
            }
        }

        /// <summary>Compatibilidad con codigo anterior.</summary>
        public void NotifyZombieKilled(Vector3 position) => NotifyZombieKilled(null, position);

        private void TryDrop(Vector3 position) => DropLoot(position, 1f);

        /// <summary>
        /// Botin al estilo RE4 (enemigos y cajas): pesetas aparte, y luego hierba si vas mal de vida,
        /// municion de las armas que llevas, polvora / recursos para fabricar o, a veces, un tesoro.
        /// </summary>
        public void DropLoot(Vector3 position, float chanceMul)
        {
            if (pesetasItem != null && Random.value < pesetasChance * chanceMul)
            {
                int money = (Random.Range(3, 11) + CurrentWave * 2) * 100;
                ItemPickup coins = ItemPickup.Spawn(pesetasItem, money, position + new Vector3(0.4f, 0.3f, 0.2f));
                if (coins != null) coins.despawns = true;
            }

            if (Random.value > dropChance * chanceMul) return;
            Vector3 at = position + Vector3.up * 0.3f;

            PlayerHealth ph = PlayerHealth.Instance;
            bool lowHealth = ph != null && ph.Normalized < 0.5f;
            if ((lowHealth && Random.value < 0.6f) || Random.value < 0.12f)
            {
                ItemDefinition herb = Random.value < 0.75f ? greenHerb : redHerb;
                Spawn(herb, 1, at);
                return;
            }

            float roll = Random.value;
            if (roll < 0.07f && treasures.Count > 0)
            {
                Spawn(treasures[Random.Range(0, treasures.Count)], 1, at);
                return;
            }
            if (roll < 0.35f && gunpowder != null)
            {
                float r = Random.value;
                ItemDefinition res = r < 0.6f ? gunpowder : r < 0.85f ? resourceS : resourceL;
                Spawn(res ?? gunpowder, res == gunpowder ? Random.Range(1, 3) : 1, at);
                return;
            }

            // Municion de un arma que el jugador lleva (como el sistema de botin de RE4)
            var owned = new List<ItemDefinition>();
            AttacheCase attache = AttacheCase.Instance;
            if (attache != null)
            {
                foreach (var e in attache.Weapons())
                {
                    WeaponDefinition w = e.item.weapon;
                    if (w != null && w.ammoItem != null && w.weaponClass != WeaponClass.RocketLauncher && !owned.Contains(w.ammoItem)) owned.Add(w.ammoItem);
                }
            }
            if (owned.Count == 0) owned.AddRange(ammoDrops);
            if (owned.Count == 0) return;

            ItemDefinition ammo = owned[Random.Range(0, owned.Count)];
            int amount = Mathf.Max(1, Mathf.RoundToInt(ammo.maxStack * Random.Range(0.12f, 0.25f)));
            Spawn(ammo, amount, at);
        }

        private static void Spawn(ItemDefinition item, int count, Vector3 at)
        {
            if (item == null) return;
            ItemPickup p = ItemPickup.Spawn(item, count, at);
            if (p != null) p.despawns = true;
        }

        // ------------------------------------------------------------ Fase 3: la campana

        private IEnumerator VictoryRoutine()
        {
            yield return new WaitForSeconds(1.5f);
            Victory = true;
            Vector3 bellPos = _player != null ? _player.position + Vector3.up * 15f + _player.forward * 40f : transform.position;
            GameObject church = GameObject.Find("Iglesia_Campanario");
            if (church != null) bellPos = church.transform.position + Vector3.up * 12f;

            Banner("La campana de la iglesia suena...\n<size=22>Los aldeanos se retiran. Has sobrevivido.</size>");
            for (int i = 0; i < 3; i++)
            {
                ProceduralAudio.PlayAt(ProceduralAudio.Bell(), bellPos, 1f, 0f, 400f);
                yield return new WaitForSeconds(2.2f);
            }
        }

        /// <summary>Progreso normalizado (0..1) de las oleadas completadas durante el asedio del pueblo.</summary>
        public float SiegeProgress => MaxWaves > 0 ? Mathf.Clamp01((float)CurrentWave / MaxWaves) : 0f;
    }
}
