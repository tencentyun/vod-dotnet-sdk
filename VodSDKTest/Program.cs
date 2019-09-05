using System;
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

            VodUploadResponse response = client.Upload("ap-guangzhou", req);


            Console.Write("{0}|{1}\n", response.FileId, response.MediaUrl);

            Console.Read();
        }
    }
}
