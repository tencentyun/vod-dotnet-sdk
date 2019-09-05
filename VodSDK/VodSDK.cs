using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;
using COSXML.Utils;
using System;
using System.IO;
using System.Threading;
using TencentCloud.Common;
using TencentCloud.Vod.V20180717;
using TencentCloud.Vod.V20180717.Models;

namespace VodSDK
{
    public class VodUploadClient
    {
        private string SecretId;
        private string SecretKey;

        static private long MinPartSize = 1024 * 1024;
        static private long MaxPartNum = 10000;
        static public int PoolSize = 10;
        static private int retryTime = 3;
        public VodUploadClient(string secretId, string secretKey)
        {
            SecretId = secretId;
            SecretKey = secretKey;
        }

        public VodUploadResponse Upload(string region, VodUploadRequest req)
        {
            CheckRequest(region, req);

            TencentCloud.Common.Credential cred = new TencentCloud.Common.Credential
            {
                SecretId = SecretId,
                SecretKey = SecretKey
            };
            VodClient vodClient = new VodClient(cred, region);

            ApplyUploadResponse applyResp = DoApplyRequest(vodClient, req);
            //Console.WriteLine(AbstractModel.ToJsonString(applyResp));

            DoUploadAction(applyResp, req);

            CommitUploadResponse commitResp = DoCommitRequest(vodClient, applyResp);
            //Console.WriteLine(AbstractModel.ToJsonString(commitResp));

            VodUploadResponse rsp = new VodUploadResponse();
            rsp.FileId = commitResp.FileId;
            rsp.MediaUrl = commitResp.MediaUrl;
            return rsp;
        }

        private void CheckRequest(string region, VodUploadRequest req)
        {
            FileInfo fileInfo = new FileInfo(req.MediaFilePath);
            if (string.IsNullOrEmpty(region))
            {
                throw new VodClientException("lack region");
            }

            if (string.IsNullOrEmpty(req.MediaFilePath))
            {
                throw new VodClientException("lack media path");
            }

            if (!fileInfo.Exists)
            {
                throw new VodClientException("media path is invalid");
            }

            if (fileInfo.Extension == "")
            {
                throw new VodClientException("lack media type");
            }

            if (!string.IsNullOrEmpty(req.CoverFilePath))
            {
                FileInfo coverInfo = new FileInfo(req.CoverFilePath);
                if (!coverInfo.Exists)
                {
                    throw new VodClientException("cover path is invalid");
                }

                if (coverInfo.Extension == "")
                {
                    throw new VodClientException("lack cover type");
                }
            }
        }

        private ApplyUploadResponse DoApplyRequest(VodClient client, VodUploadRequest req)
        {
            ApplyUploadRequest applyReq = new ApplyUploadRequest();
            applyReq.MediaType = System.IO.Path.GetExtension(req.MediaFilePath).Substring(1);
            applyReq.MediaName = System.IO.Path.GetFileName(req.MediaFilePath);
            if (req.CoverFilePath != null && req.CoverFilePath != "")
            {
                applyReq.CoverType = System.IO.Path.GetExtension(req.CoverFilePath).Substring(1);
            }
            applyReq.Procedure = req.Procedure;

            TencentCloudSDKException err = null;
            for (int i = 0; i < retryTime; i++)
            {
                try
                {
                    return client.ApplyUpload(applyReq).
                        ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (TencentCloudSDKException exception)
                {
                    if (exception.RequestId == "")
                    {
                        err = exception;
                        continue;
                    }
                    throw exception;
                }
            }
            throw err;
        }

        private CommitUploadResponse DoCommitRequest(VodClient client, ApplyUploadResponse applyResp)
        {
            CommitUploadRequest commitReq = new CommitUploadRequest();
            commitReq.VodSessionKey = applyResp.VodSessionKey;
            TencentCloudSDKException err = null;
            for (int i = 0; i < retryTime; i++)
            {
                try
                {
                    return client.CommitUpload(commitReq).
                        ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (TencentCloudSDKException exception)
                {
                    if (exception.RequestId == "")
                    {
                        err = exception;
                        continue;
                    }
                    throw exception;
                }
            }
            throw err;
        }

        private void DoUploadAction(ApplyUploadResponse applyResp, VodUploadRequest req)
        {
            string[] fields = applyResp.StorageBucket.Split('-');
            string cosAppId = fields[fields.Length - 1];

            CosXmlConfig config = new CosXmlConfig.Builder()
                    .SetAppid(cosAppId)
                    .SetRegion(applyResp.StorageRegion)
                    .SetDebugLog(false)
                    .SetConnectionLimit(512)
                    .Build();
            DefaultSessionQCloudCredentialProvider qCloudCredentialProvider = new DefaultSessionQCloudCredentialProvider(applyResp.TempCertificate.SecretId, applyResp.TempCertificate.SecretKey,
                (long)applyResp.TempCertificate.ExpiredTime, applyResp.TempCertificate.Token);
            CosXmlServer cosXml = new CosXmlServer(config, qCloudCredentialProvider);

            MultiUpload(cosXml, applyResp.StorageBucket, applyResp.MediaStoragePath, req.MediaFilePath);
            if (!string.IsNullOrEmpty(req.CoverFilePath))
            {
                MultiUpload(cosXml, applyResp.StorageBucket, applyResp.CoverStoragePath, req.CoverFilePath);
            }
        }
        private class UploadOnePart
        {
            CosXml CosXml;
            UploadPartRequest Req;
            public UploadPartResult Result { get; set; }

            public UploadOnePart(CosXml cosXml, UploadPartRequest req)
            {
                CosXml = cosXml;
                Req = req;
            }


            public void UploadPartResult()
            {
                Result = CosXml.UploadPart(Req);
            }
        }

        private void MultiUpload(COSXML.CosXml cosXml, string bucket, string key, string srcPath)
        {
            try
            {
                InitMultipartUploadRequest initMultipartUploadRequest = new InitMultipartUploadRequest(bucket, key);
                //设置签名有效时长
                initMultipartUploadRequest.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);
                InitMultipartUploadResult initMultipartUploadResult = cosXml.InitMultipartUpload(initMultipartUploadRequest);

                string uploadId = initMultipartUploadResult.initMultipartUpload.uploadId;

                CompleteMultipartUploadRequest completeMultiUploadRequest = new CompleteMultipartUploadRequest(bucket, key, uploadId);
                completeMultiUploadRequest.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);

                FileInfo fileInfo = new FileInfo(srcPath);
                long contentLength = fileInfo.Length;
                long partSize = MinPartSize;
                long partNum = (contentLength + partSize - 1) / partSize;
                if (partNum > MaxPartNum)
                {
                    partSize = (partNum + MaxPartNum - 1) / MaxPartNum * 1024 * 1024;
                    partNum = (contentLength + partSize - 1) / partSize;
                }

                UploadOnePart[] uploadList = new UploadOnePart[PoolSize];
                Thread[] workPool = new Thread[PoolSize];

                for (int i = 0; i * partSize <= contentLength; i += PoolSize)
                {
                    for (int j = 0; j < PoolSize; j++)
                    {
                        if ((i + j) * partSize >= contentLength)
                        {
                            break;
                        }

                        UploadPartRequest uploadPartRequest = new UploadPartRequest(bucket, key, (int)(i + j + 1), uploadId, srcPath, (i + j) * partSize, partSize);
                        //设置签名有效时长
                        uploadPartRequest.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);

                        uploadList[j] = new UploadOnePart(cosXml, uploadPartRequest);
                        ThreadStart childref = new ThreadStart(uploadList[j].UploadPartResult);
                        workPool[j] = new Thread(childref);
                        workPool[j].Start();
                    }
                    for (int j = 0; j < PoolSize; j++)
                    {
                        if ((i + j) * partSize >= contentLength)
                        {
                            break;
                        }
                        workPool[j].Join();
                        completeMultiUploadRequest.SetPartNumberAndETag(i + j + 1, uploadList[j].Result.eTag);
                    }
                }

                //执行请求
                CompleteMultipartUploadResult completeMultiUploadResult = cosXml.CompleteMultiUpload(completeMultiUploadRequest);
            }
            catch (COSXML.CosException.CosClientException clientEx)
            {
                throw clientEx;
            }
            catch (COSXML.CosException.CosServerException serverEx)
            {
                throw serverEx;
            }
        }
    }

    public class VodClientException : Exception
    {
        public VodClientException(string e) : base(e) { }
    }

    public class VodUploadRequest
    {
        public string MediaFilePath { get; set; }
        public string CoverFilePath { get; set; }
        public string Procedure { get; set; }
    }

    public class VodUploadResponse
    {
        public string FileId { get; set; }
        public string MediaUrl { get; set; }

    }
}