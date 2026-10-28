using System.IO;
using UnityEditor;
using UnityEngine;
using Gameplay.RE4;

namespace LevelSetup.RE4Tools
{
    /// <summary>
    /// Claves de la IA de los NPC. Se guardan CIFRADAS (AES-256, ligadas a este PC y usuario)
    /// en la carpeta de datos del usuario: nunca en la escena, los assets ni la build.
    /// Gemini tiene prioridad (conversacion + voces); Claude se usa si no hay clave de Gemini.
    /// </summary>
    public class RE4AIKeyWindow : EditorWindow
    {
        private string _gemini = "";
        private string _claude = "";

        public static void Open()
        {
            var w = GetWindow<RE4AIKeyWindow>(true, "RE4 · IA y voces de los NPC", true);
            w.minSize = new Vector2(560f, 330f);
            w._gemini = ApiKeyStore.Load("gemini") ?? "";
            w._claude = ApiKeyStore.Load("claude") ?? "";
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "GEMINI (recomendado): Tomás conversa con IA y TODOS hablan con voz (Tomás, Ingrid por radio, Leon, el mercader y los gritos de los aldeanos).\n" +
                "Cada frase de voz se genera una sola vez y se guarda en caché.\n\n" +
                "CLAUDE: solo conversación (sin voces), si no hay clave de Gemini.\n\n" +
                "Las claves se guardan cifradas en:\n" + Application.persistentDataPath, MessageType.Info);
            EditorGUILayout.Space(6f);
            _gemini = EditorGUILayout.PasswordField("Clave de Gemini", _gemini);
            _claude = EditorGUILayout.PasswordField("Clave de Claude (opcional)", _claude);
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Guardar (cifrado)"))
            {
                ApiKeyStore.Save("gemini", _gemini);
                ApiKeyStore.Save("claude", _claude);
                NpcChat.ReloadKeys();
                ShowNotification(new GUIContent("Claves guardadas"));
            }
            if (GUILayout.Button("Borrar voces en caché"))
            {
                if (Directory.Exists(NpcVoice.CacheFolder)) Directory.Delete(NpcVoice.CacheFolder, true);
                ShowNotification(new GUIContent("Caché de voces borrada"));
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(4f);
            NpcChat.ReloadKeys();
            EditorGUILayout.LabelField("IA de conversación: " + NpcChat.ProviderName);
            EditorGUILayout.LabelField("Voces: " + (NpcVoice.Enabled ? "activadas (Gemini)" : "desactivadas (solo subtítulos)"));
        }
    }
}
