using System;
using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>Receta de fabricacion del maletin (polvora + recursos = municion), como en RE4 Remake.</summary>
    [Serializable]
    public class CraftRecipe
    {
        public ItemDefinition result;
        public int resultCount = 10;
        public ItemDefinition ingredientA;
        public int countA = 1;
        public ItemDefinition ingredientB;
        public int countB = 0;

        public bool CanCraft(AttacheCase c)
        {
            if (result == null || ingredientA == null || c == null) return false;
            if (c.CountItem(ingredientA) < countA) return false;
            if (ingredientB != null && countB > 0 && c.CountItem(ingredientB) < countB) return false;
            return true;
        }

        public string Describe()
        {
            string b = ingredientB != null && countB > 0 ? $" + {ingredientB.displayName} x{countB}" : "";
            return $"{result.displayName} x{resultCount}   ←   {ingredientA.displayName} x{countA}{b}";
        }

        /// <summary>Fabrica si hay ingredientes y sitio. Devuelve un mensaje para la interfaz.</summary>
        public string Craft(AttacheCase c)
        {
            if (!CanCraft(c)) return "<color=#ff7060>Te faltan materiales.</color>";
            c.Consume(ingredientA, countA);
            if (ingredientB != null && countB > 0) c.Consume(ingredientB, countB);
            c.TryAdd(result, resultCount, out int leftover);
            if (leftover > 0)
            {
                // sin sitio: devolver materiales
                c.TryAdd(ingredientA, countA, out _);
                if (ingredientB != null && countB > 0) c.TryAdd(ingredientB, countB, out _);
                return "<color=#ff7060>No hay sitio en el maletín.</color>";
            }
            return $"<color=#9fe8a0>Fabricado: {result.displayName} x{resultCount}</color>";
        }

        /// <summary>Calcula cuántas veces seguidas se puede fabricar esta receta con los materiales actuales.</summary>
        public int MaxCraftableBatches(AttacheCase c)
        {
            if (result == null || ingredientA == null || c == null || countA <= 0) return 0;
            int batchesA = c.CountItem(ingredientA) / countA;
            if (ingredientB != null && countB > 0)
            {
                int batchesB = c.CountItem(ingredientB) / countB;
                return Mathf.Min(batchesA, batchesB);
            }
            return batchesA;
        }
    }
}
