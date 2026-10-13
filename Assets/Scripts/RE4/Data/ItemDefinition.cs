using UnityEngine;

namespace Gameplay.RE4
{
    public enum ItemType
    {
        Weapon,
        Ammo,
        Heal,
        Herb,
        Key,
        Treasure,
        Money,
        Resource,
        Document
    }

    public enum HerbColor
    {
        None,
        Green,
        Red,
        GreenGreen,
        GreenRed
    }

    /// <summary>
    /// Definicion de un objeto que puede ir dentro del maletin (estilo Resident Evil 4).
    /// El tamano esta en celdas de la cuadricula.
    /// </summary>
    [CreateAssetMenu(menuName = "RE4/Item Definition", fileName = "Item_")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identidad")]
        public string id = "item";
        public string displayName = "Objeto";
        [TextArea] public string description = "";
        public ItemType type = ItemType.Treasure;

        [Header("Maletin")]
        public Vector2Int size = Vector2Int.one;
        public int maxStack = 1;
        public Color tileColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        public string shortLabel = "";

        [Header("Curacion")]
        [Range(0f, 1f)] public float healPercent = 0f;
        public HerbColor herb = HerbColor.None;

        [Header("Mercader")]
        [Tooltip("Precio de compra en la tienda (0 = no se vende).")]
        public int buyPrice = 0;
        [Tooltip("Lo que paga el mercader al venderselo (0 = no lo compra).")]
        public int sellPrice = 0;
        [Tooltip("Cantidad que se entrega al comprar una unidad en la tienda.")]
        public int buyAmount = 1;

        [Header("Documento (lore)")]
        [TextArea(4, 20)] public string documentText = "";

        [Header("Arma")]
        public WeaponDefinition weapon;

        [Header("Modelo en el mundo (opcional)")]
        public GameObject worldModel;
        [Tooltip("Tamano maximo del modelo en el mundo (metros). 0 = tamano original.")]
        public float worldModelSize = 0f;

        public bool IsStackable => maxStack > 1;

        /// <summary>Consultas rápidas sobre el uso y comercio del objeto en el inventario.</summary>
        public bool CanBeSold => sellPrice > 0 && type != ItemType.Key && type != ItemType.Money;
        public bool IsHealingItem => type == ItemType.Heal || type == ItemType.Herb;
        public int CellArea => Mathf.Max(1, size.x * size.y);
    }
}
