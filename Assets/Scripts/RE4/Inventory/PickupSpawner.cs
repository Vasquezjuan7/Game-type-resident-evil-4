using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>Punto colocado por el constructor del nivel: crea el objeto al iniciar la partida.</summary>
    public class PickupSpawner : MonoBehaviour
    {
        public ItemDefinition item;
        public int count = 1;

        private void Start()
        {
            if (item != null) ItemPickup.Spawn(item, count, transform.position);
            Destroy(gameObject);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = item != null ? item.tileColor : Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, Vector3.one * 0.25f);
        }
    }
}
