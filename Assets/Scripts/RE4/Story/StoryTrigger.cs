using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>Zona de la historia: avisa una vez cuando Leon entra en el radio (sin fisicas).</summary>
    public class StoryTrigger : MonoBehaviour
    {
        public static event Action<StoryTrigger> OnEntered;

        public string zoneId = "zona";
        public float radius = 12f;
        public bool once = true;

        private bool _inside;
        private bool _fired;
        private float _next;

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 0.25f;
            PlayerHealth ph = PlayerHealth.Instance;
            if (ph == null) return;
            Vector3 d = ph.transform.position - transform.position;
            d.y = 0f;
            bool inside = d.magnitude <= radius;
            if (inside && !_inside && (!once || !_fired))
            {
                _fired = true;
                OnEntered?.Invoke(this);
            }
            _inside = inside;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
