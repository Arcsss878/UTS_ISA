using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UTS_ISA.users
{
    public static class CryptoHelper
    {
        // ─── AES-256 ────────────────────────────────────────────────────────────

        public static string AesEncrypt(string plainText, byte[] key, byte[] iv)
        {
            using (var aes = new AesCryptoServiceProvider { Key = key, IV = iv })
            using (var ms = new MemoryStream())
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public static string AesDecrypt(string cipherBase64, byte[] key, byte[] iv)
        {
            using (var aes = new AesCryptoServiceProvider { Key = key, IV = iv })
            using (var ms = new MemoryStream(Convert.FromBase64String(cipherBase64)))
            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
            using (var sr = new StreamReader(cs))
            {
                return sr.ReadToEnd();
            }
        }

        public static byte[] GenerateAesKey()
        {
            byte[] key = new byte[32];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(key);
            return key;
        }

        public static byte[] GenerateAesIv()
        {
            byte[] iv = new byte[16];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(iv);
            return iv;
        }

        // ─── RSA-2048 ────────────────────────────────────────────────────────────

        public static byte[] RsaEncrypt(byte[] data, string publicKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(publicKeyXml);
                return rsa.Encrypt(data, true);
            }
        }

        public static byte[] RsaDecrypt(byte[] data, string privateKeyXml)
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.FromXmlString(privateKeyXml);
                return rsa.Decrypt(data, true);
            }
        }

        // ─── SHA-256 (password hashing) ──────────────────────────────────────────

        public static string Sha256Hash(string input)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }
    }
}
