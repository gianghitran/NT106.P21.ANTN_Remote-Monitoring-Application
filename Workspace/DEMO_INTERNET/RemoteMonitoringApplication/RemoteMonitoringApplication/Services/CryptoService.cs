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
