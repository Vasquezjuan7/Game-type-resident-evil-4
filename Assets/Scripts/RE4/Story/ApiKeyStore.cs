using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Gameplay.RE4
{
    /// <summary>
    /// Claves de API de la IA guardadas CIFRADAS (AES-256) en la carpeta de datos del usuario,
    /// nunca dentro del proyecto, la escena ni la build. La clave de cifrado se deriva de este PC
    /// y de este usuario de Windows: el archivo copiado a otro equipo no sirve.
    /// (Es una proteccion "en reposo": quien controle este mismo PC y usuario podria descifrarla.)
    /// </summary>
    public static class ApiKeyStore
    {
        private const string Salt = "RE4-Valdeoscuro|";

        public static string PathFor(string provider) => Path.Combine(Application.persistentDataPath, provider + "_api_key.dat");

        private static byte[] DeriveKey()
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + Environment.MachineName + "|" + Environment.UserName));
        }

        public static void Save(string provider, string apiKey)
        {
            string path = PathFor(provider);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            using (var aes = Aes.Create())
            {
                aes.Key = DeriveKey();
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                byte[] plain = Encoding.UTF8.GetBytes(apiKey.Trim());
                byte[] cipher;
                using (var enc = aes.CreateEncryptor()) cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
                byte[] all = new byte[aes.IV.Length + cipher.Length];
                Buffer.BlockCopy(aes.IV, 0, all, 0, aes.IV.Length);
                Buffer.BlockCopy(cipher, 0, all, aes.IV.Length, cipher.Length);
                File.WriteAllText(path, Convert.ToBase64String(all));
            }
        }

        /// <summary>Devuelve la clave descifrada o null si no hay (o no se puede descifrar en este PC).</summary>
        public static string Load(string provider)
        {
            try
            {
                string path = PathFor(provider);
                if (!File.Exists(path)) return null;
                byte[] all = Convert.FromBase64String(File.ReadAllText(path).Trim());
                if (all.Length <= 16) return null;
                using (var aes = Aes.Create())
                {
                    aes.Key = DeriveKey();
                    byte[] iv = new byte[16];
                    Buffer.BlockCopy(all, 0, iv, 0, 16);
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using (var dec = aes.CreateDecryptor())
                    {
                        byte[] plain = dec.TransformFinalBlock(all, 16, all.Length - 16);
                        return Encoding.UTF8.GetString(plain);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RE4] No se pudo leer la clave de " + provider + ": " + e.Message);
                return null;
            }
        }
    }
}
