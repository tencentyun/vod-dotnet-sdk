using System;
using System.Threading;
using System.Threading.Tasks;
using VodSDK;

namespace VodSDKTest
{
    class Program
    {
        static void Main(string[] args)
        {
            VodUploadClient client = new VodUploadClient("secretid", "secretkey");
            VodUploadRequest req = new VodUploadRequest();
            req.MediaFilePath = "F:\\sz-rz\\a.mp4";

            Task<VodUploadResponse> r = client.Upload("ap-guangzhou", req);
            VodUploadResponse response = r.Result;


            Console.Write("{0}|{1}\n", response.FileId, response.MediaUrl);

            Console.Read();
        }
    }
}
