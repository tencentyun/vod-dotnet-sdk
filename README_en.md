## Overview

The SDK for upload from server is based on the .NET language platform. By using the SDK and demo together, you can upload videos and cover files directly to Tencent Cloud VOD. You can also specify various optional parameters for upload from server.

## Dependent Environment
1. Dependent environment: .NET Framework 4.6+
2. Activate `VOD` in the Tencent Cloud Console
3. Get the `SecretID` and `SecretKey`

## Directions

### Installing through NuGet (recommended)
1. Install on the command line: 

```
dotnet add package VodSDK --version 1.0.1
```

2. Search for VodSDK in Visual Studio's NuGet package manager and install it.


### Installing through source package

Download the latest code, decompress and install it in your working directory, and open it with Visual Studio 2017 for compiling. As this SDK depends on external packages, the following two SDKs also need to be installed: 
1. [TencentCloud API SDK](https://github.com/TencentCloud/tencentcloud-sdk-dotnet)
2. [COS SDK](https://github.com/tencentyun/qcloud-sdk-dotnet)

## Sample

```
using System;
using VodSDK;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            VodUploadClient client = new VodUploadClient("your secretId", "your secretKey");
            VodUploadRequest request = new VodUploadRequest();
            request.MediaFilePath = "/data/file/Wildlife.mp4";
            try
            {
                VodUploadResponse response = client.Upload("ap-guangzhou", request).Result;
                Console.WriteLine(response.FileId);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            Console.ReadLine();
        }
    }
}
```
