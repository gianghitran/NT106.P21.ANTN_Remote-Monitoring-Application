using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteMonitoringApplication.Services
{
    public static class ClientIdentity
    {
        public static string GenerateRandomId()
        {
            var rnd = new Random();
            return rnd.Next(100000, 1000000).ToString(); 
        }

        public static string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789@#$%";
            var rnd = new Random();
            var sb = new StringBuilder();
            for (int i = 0; i < 16; i++)
                sb.Append(chars[rnd.Next(chars.Length)]);
            return sb.ToString();
        }
    }
}
