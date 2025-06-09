using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
namespace RemoteMonitoringApplication.Services
{
    class CompressService
    {
        // Phương thức nén dữ liệu
        public static byte[] Compress(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var brotli = new BrotliStream(output, CompressionMode.Compress))
                {
                    brotli.Write(data, 0, data.Length);
                }
                return output.ToArray();
            }
        }
        // Phương thức giải nén dữ liệu
        public static byte[] Decompress(byte[] compressedData)
        {
            using (var input = new MemoryStream(compressedData))
            using (var brotli = new BrotliStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                brotli.CopyTo(output);
                return output.ToArray();
            }
        }
    }
}
