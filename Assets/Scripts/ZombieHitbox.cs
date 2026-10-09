using UnityEngine;
using Gameplay.RE4;

namespace Gameplay
{
    /// <summary>
    /// Zona de impacto pegada a un hueso del enemigo (cabeza, torso, brazos, piernas).
    /// Vive en la capa "Hitbox" como trigger para que los disparos la detecten sin bloquear al jugador.
    /// </summary>
    public class ZombieHitbox : MonoBehaviour
    {
        public BodyPart part = BodyPart.Body;
        public ZombieHealth health;

        private void Awake()
        {
            if (health == null) health = GetComponentInParent<ZombieHealth>();
        }

        /// <summary>Indica si la zona de impacto corresponde a un punto crítico (cabeza).</summary>
        public bool IsCriticalZone => part == BodyPart.Head;
    }
}
