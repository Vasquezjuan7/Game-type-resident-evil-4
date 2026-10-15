using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.RE4
{
    [Serializable]
    public class CaseEntry
    {
        public ItemDefinition item;
        public int count = 1;
        public Vector2Int position;
        public bool rotated;
        [Tooltip("Balas dentro del cargador (solo armas).")]
        public int loadedAmmo;
        [Tooltip("Mejoras compradas al mercader (solo armas).")]
        public WeaponUpgrades upgrades = new WeaponUpgrades();

        public Vector2Int Size => rotated ? new Vector2Int(item.size.y, item.size.x) : item.size;
        public int MagazineSize => item != null && item.weapon != null ? WeaponStats.MagazineSize(item.weapon, this) : 0;
        public RectInt Rect => new RectInt(position, Size);
    }

    [Serializable]
    public class StartingItem
    {
        public ItemDefinition item;
        public int count = 1;
    }

    /// <summary>
    /// Maletin de Leon: inventario en cuadricula al estilo Resident Evil 4.
    /// </summary>
    public class AttacheCase : MonoBehaviour
    {
        public const int QuickSlotCount = 5;

        public static AttacheCase Instance { get; private set; }

        [Header("Cuadricula")]
        public int width = 12;
        public int height = 8;

        [Header("Inventario inicial")]
        public List<StartingItem> startingItems = new List<StartingItem>();

        [Header("Fabricacion (tecla C en el maletin)")]
        public List<CraftRecipe> recipes = new List<CraftRecipe>();

        [Header("Pesetas")]
        public int startingPesetas = 5000;
        public int pesetas;

        [Header("Contenido")]
        public List<CaseEntry> entries = new List<CaseEntry>();
        public CaseEntry[] quickSlots = new CaseEntry[QuickSlotCount];

        public event Action OnChanged;

        private void Awake()
        {
            Instance = this;
            if (quickSlots == null || quickSlots.Length != QuickSlotCount) quickSlots = new CaseEntry[QuickSlotCount];
            entries.Clear();
            pesetas = startingPesetas;
            foreach (var s in startingItems)
            {
                if (s != null && s.item != null) TryAdd(s.item, s.count, out _);
            }
        }

        public void NotifyChanged() => OnChanged?.Invoke();

        public event Action<int> OnPesetasChanged;

        public void AddPesetas(int amount)
        {
            if (amount == 0) return;
            pesetas = Mathf.Max(0, pesetas + amount);
            OnPesetasChanged?.Invoke(amount);
        }

        public bool SpendPesetas(int amount)
        {
            if (amount > pesetas) return false;
            AddPesetas(-amount);
            return true;
        }

        // ------------------------------------------------------------ Consultas

        public bool IsInside(RectInt r) => r.xMin >= 0 && r.yMin >= 0 && r.xMax <= width && r.yMax <= height;

        public bool CanPlace(RectInt r, CaseEntry ignore = null)
        {
            if (!IsInside(r)) return false;
            foreach (var e in entries)
            {
                if (e == ignore) continue;
                if (e.Rect.Overlaps(r)) return false;
            }
            return true;
        }

        public CaseEntry EntryAt(Vector2Int cell)
        {
            foreach (var e in entries)
            {
                if (e.Rect.Contains(cell)) return e;
            }
            return null;
        }

        public int CountItem(ItemDefinition item)
        {
            if (item == null) return 0;
            int c = 0;
            foreach (var e in entries) if (e.item == item) c += e.count;
            return c;
        }

        public CaseEntry FindWeapon(WeaponDefinition weapon)
        {
            foreach (var e in entries) if (e.item != null && e.item.weapon == weapon) return e;
            return null;
        }

        public List<CaseEntry> Weapons()
        {
            var list = new List<CaseEntry>();
            foreach (var e in entries) if (e.item != null && e.item.type == ItemType.Weapon) list.Add(e);
            return list;
        }

        // ------------------------------------------------------------ Modificacion

        /// <summary>Agrega un objeto: primero apila, luego busca hueco (probando rotado).</summary>
        public bool TryAdd(ItemDefinition item, int count, out int leftover)
        {
            leftover = count;
            if (item == null || count <= 0) return false;
            if (item.type == ItemType.Document)
            {
                // los documentos van a Archivos (tecla J), no ocupan sitio en el maletin
                if (StoryDirector.Instance != null) StoryDirector.Instance.AddDocument(item);
                leftover = 0;
                return true;
            }
            if (item.type == ItemType.Money)
            {
                AddPesetas(count);
                leftover = 0;
                return true;
            }

            if (item.IsStackable)
            {
                foreach (var e in entries)
                {
                    if (e.item != item || e.count >= item.maxStack) continue;
                    int add = Mathf.Min(item.maxStack - e.count, leftover);
                    e.count += add;
                    leftover -= add;
                    if (leftover <= 0) break;
                }
            }

            while (leftover > 0)
            {
                if (!FindFreeSpot(item, out Vector2Int pos, out bool rotated)) break;
                int amount = item.IsStackable ? Mathf.Min(item.maxStack, leftover) : 1;
                var entry = new CaseEntry { item = item, count = amount, position = pos, rotated = rotated };
                if (item.weapon != null) entry.loadedAmmo = item.weapon.magazineSize;
                entries.Add(entry);
                leftover -= amount;
                if (item.type == ItemType.Weapon) AutoAssignQuickSlot(entry);
            }

            NotifyChanged();
            return leftover < count;
        }

        public bool FindFreeSpot(ItemDefinition item, out Vector2Int pos, out bool rotated)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                bool rot = pass == 1;
                Vector2Int size = rot ? new Vector2Int(item.size.y, item.size.x) : item.size;
                if (rot && size == item.size) break;
                for (int y = 0; y <= height - size.y; y++)
                {
                    for (int x = 0; x <= width - size.x; x++)
                    {
                        var r = new RectInt(x, y, size.x, size.y);
                        if (CanPlace(r))
                        {
                            pos = new Vector2Int(x, y);
                            rotated = rot;
                            return true;
                        }
                    }
                }
            }
            pos = Vector2Int.zero;
            rotated = false;
            return false;
        }

        public bool TryMove(CaseEntry e, Vector2Int newPos, bool rotated)
        {
            Vector2Int size = rotated ? new Vector2Int(e.item.size.y, e.item.size.x) : e.item.size;
            var r = new RectInt(newPos, size);
            if (!CanPlace(r, e)) return false;
            e.position = newPos;
            e.rotated = rotated;
            NotifyChanged();
            return true;
        }

        public void Remove(CaseEntry e)
        {
            entries.Remove(e);
            for (int i = 0; i < quickSlots.Length; i++) if (quickSlots[i] == e) quickSlots[i] = null;
            NotifyChanged();
        }

        /// <summary>Consume municion repartida en varias pilas. Devuelve cuanto se pudo sacar.</summary>
        public int Consume(ItemDefinition item, int amount)
        {
            int taken = 0;
            for (int i = entries.Count - 1; i >= 0 && taken < amount; i--)
            {
                var e = entries[i];
                if (e.item != item) continue;
                int t = Mathf.Min(e.count, amount - taken);
                e.count -= t;
                taken += t;
                if (e.count <= 0) entries.RemoveAt(i);
            }
            if (taken > 0) NotifyChanged();
            return taken;
        }

        public void AutoAssignQuickSlot(CaseEntry weaponEntry)
        {
            for (int i = 0; i < quickSlots.Length; i++) if (quickSlots[i] == weaponEntry) return;
            for (int i = 0; i < quickSlots.Length; i++)
            {
                if (quickSlots[i] == null || !entries.Contains(quickSlots[i]))
                {
                    quickSlots[i] = weaponEntry;
                    return;
                }
            }
        }

        public void AssignQuickSlot(int slot, CaseEntry e)
        {
            if (slot < 0 || slot >= quickSlots.Length) return;
            for (int i = 0; i < quickSlots.Length; i++) if (quickSlots[i] == e) quickSlots[i] = null;
            quickSlots[slot] = e;
            NotifyChanged();
        }

        public int QuickSlotOf(CaseEntry e)
        {
            for (int i = 0; i < quickSlots.Length; i++) if (quickSlots[i] == e) return i;
            return -1;
        }

        /// <summary>Ordena automaticamente: los objetos mas grandes primero.</summary>
        public void AutoSort()
        {
            var sorted = new List<CaseEntry>(entries);
            sorted.Sort((a, b) =>
            {
                int area = (b.item.size.x * b.item.size.y).CompareTo(a.item.size.x * a.item.size.y);
                return area != 0 ? area : string.Compare(a.item.id, b.item.id, StringComparison.Ordinal);
            });
            var placed = new List<CaseEntry>();
            entries.Clear();
            foreach (var e in sorted)
            {
                if (FindFreeSpot(e.item, out Vector2Int p, out bool rot))
                {
                    e.position = p;
                    e.rotated = rot;
                    entries.Add(e);
                }
                else
                {
                    placed.Add(e); // no deberia pasar; se conserva en su sitio
                }
            }
            entries.AddRange(placed);
            NotifyChanged();
        }

        // ------------------------------------------------------------ Hierbas

        [Header("Mezclas de hierbas")]
        public ItemDefinition mixGreenGreen;
        public ItemDefinition mixGreenRed;

        public static HerbColor CombineHerbs(HerbColor a, HerbColor b)
        {
            if (a == HerbColor.Green && b == HerbColor.Green) return HerbColor.GreenGreen;
            if ((a == HerbColor.Green && b == HerbColor.Red) || (a == HerbColor.Red && b == HerbColor.Green)) return HerbColor.GreenRed;
            return HerbColor.None;
        }

        /// <summary>Combina dos hierbas (arrastrar una sobre otra). La mezcla ocupa el lugar de la segunda.</summary>
        public bool TryCombine(CaseEntry dragged, CaseEntry target)
        {
            if (dragged == null || target == null || dragged == target) return false;
            HerbColor result = CombineHerbs(dragged.item.herb, target.item.herb);
            ItemDefinition mix = result == HerbColor.GreenGreen ? mixGreenGreen : result == HerbColor.GreenRed ? mixGreenRed : null;
            if (mix == null) return false;

            Vector2Int pos = target.position;
            entries.Remove(dragged);
            entries.Remove(target);
            var entry = new CaseEntry { item = mix, count = 1, position = pos, rotated = false };
            if (!CanPlace(entry.Rect))
            {
                if (!FindFreeSpot(mix, out pos, out bool rot))
                {
                    entries.Add(dragged);
                    entries.Add(target);
                    return false;
                }
                entry.position = pos;
                entry.rotated = rot;
            }
            entries.Add(entry);
            NotifyChanged();
            return true;
        }

        /// <summary>Calcula cuántas celdas de la cuadrícula están ocupadas y el espacio libre disponible.</summary>
        public int TotalCells => Mathf.Max(1, width * height);

        public int OccupiedCells
        {
            get
            {
                int used = 0;
                foreach (var e in entries)
                {
                    if (e != null && e.item != null) used += e.Size.x * e.Size.y;
                }
                return Mathf.Min(TotalCells, used);
            }
        }

        public int FreeCells => Mathf.Max(0, TotalCells - OccupiedCells);
    }
}
