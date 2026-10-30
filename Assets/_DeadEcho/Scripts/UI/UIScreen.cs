using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeadEcho.UI
{
    /// <summary>
    /// Pantalla o panel gestionado por <see cref="UIRouter"/>. Usa un CanvasGroup para el fundido
    /// y recuerda que elemento estaba seleccionado para restaurarlo al volver.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreen : MonoBehaviour
    {
        [Tooltip("Elemento seleccionado al abrir la pantalla (EventSystem.SetSelectedGameObject).")]
        public Selectable firstSelected;
        [Tooltip("Esc / B cierra esta pantalla y vuelve a la anterior.")]
        public bool canGoBack = true;
        [Tooltip("Duracion del fundido de entrada y salida.")]
        public float fadeTime = 0.25f;

        public event Action OnShown;
        public event Action OnHidden;

        public bool IsVisible { get; private set; }
        public CanvasGroup Group { get; private set; }
        public UIRouter Router { get; internal set; }

        /// <summary>Ultima seleccion dentro de esta pantalla (para restaurarla al volver).</summary>
        internal GameObject lastSelected;

        private Coroutine _fade;

        protected virtual void Awake()
        {
            Group = GetComponent<CanvasGroup>();
        }

        /// <summary>Muestra la pantalla (con fundido) y fija la seleccion inicial o la recordada.</summary>
        public virtual void Show(bool restoreSelection)
        {
            gameObject.SetActive(true);
            IsVisible = true;
            Group.interactable = true;
            Group.blocksRaycasts = true;
            Fade(1f);
            GameObject target = restoreSelection && lastSelected != null && lastSelected.activeInHierarchy ? lastSelected
                : firstSelected != null ? firstSelected.gameObject : null;
            Select(target);
            OnShown?.Invoke();
        }

        /// <summary>Oculta la pantalla. Si keepActive, solo deja de responder (queda debajo en la pila).</summary>
        public virtual void Hide(bool keepVisibleBelow = false)
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
                lastSelected = EventSystem.current.currentSelectedGameObject;
            IsVisible = keepVisibleBelow;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            if (!keepVisibleBelow) Fade(0f, true);
            OnHidden?.Invoke();
        }

        /// <summary>Que hacer con Esc / B. Por defecto, volver a la pantalla anterior.</summary>
        public virtual void OnBack()
        {
            if (!canGoBack || Router == null) return;
            UIAudio.Play(UISound.Atras);
            Router.Pop();
        }

        public void Select(GameObject go)
        {
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            if (go != null) EventSystem.current.SetSelectedGameObject(go);
        }

        private void Fade(float to, bool deactivateAtEnd = false)
        {
            if (_fade != null) StopCoroutine(_fade);
            if (!isActiveAndEnabled)
            {
                Group.alpha = to;
                return;
            }
            _fade = StartCoroutine(FadeRoutine(to, deactivateAtEnd));
        }

        private IEnumerator FadeRoutine(float to, bool deactivateAtEnd)
        {
            yield return UIFx.Fade(Group, to, Core.SettingsService.ReduceMotion ? 0f : fadeTime);
            if (deactivateAtEnd && !IsVisible) gameObject.SetActive(false);
            _fade = null;
        }
    }
}
