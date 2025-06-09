using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Forms;
using static System.Runtime.InteropServices.JavaScript.JSType;
namespace RemoteMonitoringApplication.Services
{
    public class CryptoService
    {
        public static CngKey CreateECCKeyFromString(string seedString, bool isPrivateKey)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(seedString));
                byte[] keyMaterial = new byte[32];
                Array.Copy(hash, keyMaterial, 32); // P-256 cần 32 bytes

                CngKeyCreationParameters parameters = new CngKeyCreationParameters
                {
                    ExportPolicy = CngExportPolicies.AllowPlaintextExport,
                    KeyUsage = isPrivateKey ? CngKeyUsages.KeyAgreement : CngKeyUsages.AllUsages,
                    Parameters = {
                    new CngProperty("Length", BitConverter.GetBytes(256), CngPropertyOptions.None)
                }
                };

                CngKey key = CngKey.Create(CngAlgorithm.ECDiffieHellmanP256, null, parameters);
                return key;
            }
        }

        public static byte[] GetPrivateKeyBlobFromString(string seedString)
        {
            using (var ecdh = new ECDiffieHellmanCng(CreateECCKeyFromString(seedString, true)))
            {
                return ecdh.Key.Export(CngKeyBlobFormat.EccPrivateBlob);
            }
        }

        public static byte[] GetPublicKeyBlobFromString(string seedString)
        {
            using (var ecdh = new ECDiffieHellmanCng(CreateECCKeyFromString(seedString, false)))
            {
                return ecdh.PublicKey.ToByteArray();
            }
        }
        //private static byte[] key = Encoding.UTF8.GetBytes("12345678");
        //private static byte[] iv = Encoding.UTF8.GetBytes("87654321");
        public static byte[] ComputeSharedKey(string PriKey, string TheirPK)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                string combined = string.Compare(PriKey, TheirPK) < 0 ? PriKey + TheirPK : TheirPK + PriKey;
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
            }
        }


        public static string Encrypt(string plainText, byte[] strKey, string strIV)
        {
            using (System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create())
            {
                byte[] iv = Encoding.UTF8.GetBytes(strIV);

                aes.Key = strKey;
                aes.IV = iv;

                byte[] input = Encoding.UTF8.GetBytes(plainText);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(input, 0, input.Length);
                        cs.FlushFinalBlock();
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
        }
        public static string Decrypt(string encryptedText, byte[] strKey, string strIV)
        {
            using (System.Security.Cryptography.Aes aes = System.Security.Cryptography.Aes.Create())
            {
                byte[] key = strKey;
                byte[] iv = Encoding.UTF8.GetBytes(strIV);

                byte[] input = Convert.FromBase64String(encryptedText);
                using (MemoryStream ms = new MemoryStream())
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(key, iv), CryptoStreamMode.Write))
                {
                    cs.Write(input, 0, input.Length);
                    cs.FlushFinalBlock();
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
        }
    }
}
