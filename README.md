## 简介

基于 .Net 语言平台的服务端上传的 SDK，通过 SDK 和配合的 Demo，可以将视频和封面文件直接上传到腾讯云点播系统，同时可以指定各项服务端上传的可选参数。

## 依赖环境
1. 依赖平台：.NET Framework 4.6+
2. 从腾讯云控制台开通`云点播`产品
3. 获取 SecretID 、 SecretKey

## 使用方式

### 通过 nuget 安装(推荐)
1. 通过命令行安装: 

```
dotnet add package VodSDK --version 1.0.1
```

2. 通过 Visual Studio 的 nuget 包管理工具，搜索 VodSDK 并安装


### 通过源码包安装

下载最新代码，解压后安装到你的工作目录下，使用 Visual Studio 2017 打开编译。因为此 SDK 还依赖外部的包，所以需要同时安装下面两个 SDK： 
1. [云API SDK](https://github.com/TencentCloud/tencentcloud-sdk-dotnet)
2. [对象存储 SDK](https://github.com/tencentyun/qcloud-sdk-dotnet)

## 示例

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
                VodUploadResponse response = client.Upload("ap-guangzhou", request);
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
