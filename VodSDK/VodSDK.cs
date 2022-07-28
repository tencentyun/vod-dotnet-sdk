using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;
using COSXML.Transfer;
using COSXML.Utils;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        public async Task<VodUploadResponse> Upload(string region, VodUploadRequest req)
        {
            CheckRequest(region, req);

            TencentCloud.Common.Credential cred = new TencentCloud.Common.Credential
            {
                SecretId = SecretId,
                SecretKey = SecretKey
            };
            VodClient vodClient = new VodClient(cred, region);

            ApplyUploadResponse applyResp = await DoApplyRequest(vodClient, req);
            //Console.WriteLine(AbstractModel.ToJsonString(applyResp));

            await DoUploadAction(applyResp, req);

            CommitUploadResponse commitResp = await DoCommitRequest(vodClient, applyResp);
            //Console.WriteLine(AbstractModel.ToJsonString(commitResp));

            VodUploadResponse rsp= AbstractModel.FromJsonString<VodUploadResponse>( AbstractModel.ToJsonString(commitResp));
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

        private async Task<ApplyUploadResponse> DoApplyRequest(VodClient client, VodUploadRequest req)
        {
            req.MediaType = System.IO.Path.GetExtension(req.MediaFilePath).Substring(1);
            req.MediaName = System.IO.Path.GetFileName(req.MediaFilePath);
            if (req.CoverFilePath != null && req.CoverFilePath != "")
            {
                req.CoverType = System.IO.Path.GetExtension(req.CoverFilePath).Substring(1);
            }

            TencentCloudSDKException err = null;
            for (int i = 0; i < retryTime; i++)
            {
                try
                {
                    ApplyUploadResponse rsp = await client.ApplyUpload(req);
                    return rsp;

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

        private async Task<CommitUploadResponse> DoCommitRequest(VodClient client, ApplyUploadResponse applyResp)
        {
            CommitUploadRequest commitReq = new CommitUploadRequest();
            commitReq.VodSessionKey = applyResp.VodSessionKey;
            TencentCloudSDKException err = null;
            for (int i = 0; i < retryTime; i++)
            {
                try
                {
                    return await client.CommitUpload(commitReq);
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

        private async Task<int> DoUploadAction(ApplyUploadResponse applyResp, VodUploadRequest req)
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

            await MultiUpload(cosXml, applyResp.StorageBucket, applyResp.MediaStoragePath, req.MediaFilePath);
            if (!string.IsNullOrEmpty(req.CoverFilePath))
            {
                await MultiUpload(cosXml, applyResp.StorageBucket, applyResp.CoverStoragePath, req.CoverFilePath);
            }
            return 0;
        }
        private async Task<COSXML.Transfer.COSXMLUploadTask.UploadTaskResult> MultiUpload(COSXML.CosXml cosXml, string bucket, string key, string srcPath)
        {
            TransferConfig transferConfig = new TransferConfig();
            TransferManager transferManager = new TransferManager(cosXml, transferConfig);

            COSXMLUploadTask uploadTask = new COSXMLUploadTask(bucket, key.Substring(1));
            uploadTask.SetSrcPath(srcPath);

            try
            {
                return await transferManager.UploadAsync(uploadTask);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }



public class VodClientException : Exception
    {
        public VodClientException(string e) : base(e) { }
    }

    public class VodUploadRequest:ApplyUploadRequest
    {
        public string MediaFilePath { get; set; }
        public string CoverFilePath { get; set; }
    }

    public class VodUploadResponse:CommitUploadResponse
    {
    }
}