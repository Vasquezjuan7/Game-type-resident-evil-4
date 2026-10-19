using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.RE4
{
    /// <summary>Detecta objetos cercanos y los recoge con [E].</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public float range = 2.4f;
        public float maxAngle = 50f;

        public ItemPickup Focused { get; private set; }
        public Merchant FocusedMerchant { get; private set; }
        public NpcTalker FocusedNpc { get; private set; }
        public string Message { get; private set; }
        private float _messageUntil;

        private AttacheCase _case;
        private FirstPersonController _fps;

        private void Awake()
        {
            _case = GetComponent<AttacheCase>();
            _fps = GetComponent<FirstPersonController>();
        }

        public void ShowMessage(string msg, float seconds = 2f)
        {
            Message = msg;
            _messageUntil = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            if (Time.unscaledTime > _messageUntil) Message = null;

            Focused = null;
            FocusedMerchant = null;
            FocusedNpc = null;
            if (_fps != null && _fps.InputLocked) return;
            if (PlayerHealth.Instance != null && PlayerHealth.Instance.IsDead) return;

            Camera cam = _fps != null && _fps.ViewCamera != null ? _fps.ViewCamera : Camera.main;
            if (cam == null) return;

            float best = float.MaxValue;
            foreach (var p in ItemPickup.All)
            {
                if (p == null) continue;
                Vector3 to = p.transform.position - cam.transform.position;
                float d = to.magnitude;
                if (d > range + 1f) continue;
                Vector3 flat = p.transform.position - transform.position;
                flat.y = 0f;
                if (flat.magnitude > range) continue;
                float angle = Vector3.Angle(cam.transform.forward, to);
                if (angle > maxAngle) continue;
                float score = d + angle * 0.03f;
                if (score < best) { best = score; Focused = p; }
            }

            if (Focused == null)
            {
                foreach (var m in Merchant.All)
                {
                    if (m == null || !m.InRange(transform.position)) continue;
                    if (Vector3.Angle(cam.transform.forward, m.transform.position + Vector3.up - cam.transform.position) > 75f) continue;
                    FocusedMerchant = m;
                    break;
                }
            }

            if (Focused == null && FocusedMerchant == null)
            {
                foreach (var n in NpcTalker.All)
                {
                    if (n == null || !n.InRange(transform.position)) continue;
                    if (Vector3.Angle(cam.transform.forward, n.transform.position + Vector3.up * 1.5f - cam.transform.position) > 70f) continue;
                    FocusedNpc = n;
                    break;
                }
            }

            bool interact = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                            || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
            if (interact && Focused != null)
            {
                string label = Focused.Label;
                if (Focused.TryCollect(_case)) ShowMessage($"Recogiste: {label}");
                else ShowMessage("No hay espacio en el maletín");
            }
            else if (interact && FocusedMerchant != null)
            {
                FocusedMerchant.Talk(gameObject);
            }
            else if (interact && FocusedNpc != null)
            {
                FocusedNpc.Talk(gameObject);
            }
        }

        /// <summary>Devuelve true si el jugador tiene algún objeto, mercader o NPC enfocado para interactuar.</summary>
        public bool HasTarget => Focused != null || FocusedMerchant != null || FocusedNpc != null;
    }
}
