using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>Marca la superficie de un objeto para los pasos (dirt, stone, gravel, grass, wood).</summary>
    public class SurfaceTag : MonoBehaviour
    {
        public string surface = "stone";

        /// <summary>Devuelve el identificador de superficie limpio y en minúsculas.</summary>
        public string NormalizedSurface => string.IsNullOrWhiteSpace(surface) ? "dirt" : surface.Trim().ToLowerInvariant();
    }
}
