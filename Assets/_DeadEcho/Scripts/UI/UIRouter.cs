using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DeadEcho.UI
{
    /// <summary>
    /// Pila de pantallas (push / pop). Abre y cierra paneles con transicion, gestiona Esc / B
    /// y restaura la seleccion al volver. Hay uno por Canvas raiz de interfaz.
    /// </summary>
    public class UIRouter : MonoBehaviour
    {
        [Tooltip("Pantalla que se abre al empezar (opcional).")]
        public UIScreen initialScreen;
        [Tooltip("La pantalla anterior queda visible (pero inactiva) debajo de la nueva, como en los modales.")]
        public bool keepPreviousVisible;

        private readonly Stack<UIScreen> _stack = new Stack<UIScreen>();
        private GameObject _lastValidSelection;

        public UIScreen Top => _stack.Count > 0 ? _stack.Peek() : null;
        public int Count => _stack.Count;

        private void Start()
        {
            foreach (var s in GetComponentsInChildren<UIScreen>(true))
            {
                s.Router = this;
                if (s != initialScreen) s.gameObject.SetActive(false);
            }
            if (initialScreen != null) Push(initialScreen);
        }

        /// <summary>Abre una pantalla encima de la actual.</summary>
        public void Push(UIScreen screen, bool modal = false)
        {
            if (screen == null) return;
            screen.Router = this;
            if (Top != null) Top.Hide(modal || keepPreviousVisible);
            _stack.Push(screen);
            screen.Show(false);
        }

        /// <summary>Cierra la pantalla actual y vuelve a la anterior (restaurando su seleccion).</summary>
        public void Pop()
        {
            if (_stack.Count == 0) return;
            UIScreen top = _stack.Pop();
            top.Hide();
            if (Top != null) Top.Show(true);
        }

        /// <summary>Cierra todo y deja solo esta pantalla.</summary>
        public void Replace(UIScreen screen)
        {
            while (_stack.Count > 0) _stack.Pop().Hide();
            Push(screen);
        }

        private void Update()
        {
            UIScreen top = Top;
            if (top == null) return;

            // Esc / B / Atras del raton
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)
                        || (Mouse.current != null && Mouse.current.backButton.wasPressedThisFrame);
            if (back && top.Group.interactable) top.OnBack();

            // Nunca quedarse sin seleccion (si se hace clic en el vacio, volver a la ultima valida)
            EventSystem es = EventSystem.current;
            if (es == null) return;
            GameObject sel = es.currentSelectedGameObject;
            if (sel != null && sel.activeInHierarchy && sel.transform.IsChildOf(top.transform)) _lastValidSelection = sel;
            else if (top.Group.interactable)
            {
                GameObject fallback = _lastValidSelection != null && _lastValidSelection.activeInHierarchy && _lastValidSelection.transform.IsChildOf(top.transform)
                    ? _lastValidSelection
                    : top.firstSelected != null ? top.firstSelected.gameObject : null;
                if (fallback != null) es.SetSelectedGameObject(fallback);
            }
        }
    }
}
