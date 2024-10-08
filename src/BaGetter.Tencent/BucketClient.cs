using COSXML;
using COSXML.CosException;
using COSXML.Model.Bucket;
using COSXML.Model.Object;
using COSXML.Model.Tag;

namespace BaGetter.Tencent;

public class BucketClient
{
    private readonly CosXmlServer _cosXml;
    private readonly string _fullBucketName;
    private readonly string _appId;
    private readonly string _region;

    public CosXmlServer CosXml => _cosXml;

    public BucketClient(CosXmlServer cosXml, string fullBucketName, string appId, string region)
    {
        _cosXml = cosXml;
        _fullBucketName = fullBucketName;
        _appId = appId;
        _region = region;
    }

    public bool UploadBytes(string key, byte[] fileData)
    {
        var result = false;
        try
        {
            var request = new PutObjectRequest(_fullBucketName, key, fileData);

            var resultData = CosXml.PutObject(request);

            if (resultData != null && resultData.IsSuccessful())
            {
                result = true;
            }
        }
        catch (CosClientException clientEx)
        {
            throw new Exception(clientEx.Message);
        }
        catch (CosServerException serverEx)
        {
            throw new Exception(serverEx.Message);
        }
        return result;
    }

    public bool UploadStream(string key, Stream stream)
    {
        var result = false;
        try
        {
            var request = new PutObjectRequest(_fullBucketName, key, stream);

            var resultData = CosXml.PutObject(request);

            if (resultData != null && resultData.IsSuccessful())
            {
                result = true;
            }
        }
        catch (CosClientException clientEx)
        {
            throw new Exception(clientEx.Message);
        }
        catch (CosServerException serverEx)
        {
            throw new Exception(serverEx.Message);
        }
        return result;
    }

    public (string, byte[]) DownloadDirDefaultFileBytes(string dir)
    {
        try
        {
            var request = new GetBucketRequest(_fullBucketName);
            request.SetPrefix($"{dir.TrimEnd('/')}/");
            request.SetDelimiter("/");

            var result = CosXml.GetBucket(request);

            var info = result.listBucket;

            var objects = info.contentsList;

            var objectData = objects.FirstOrDefault(o => o.size > 0);

            if (objectData != null)
            {
                var fileName = Path.GetFileName(objectData.key);
                var fileBytes = DownloadFileBytes(objectData.key);
                return (fileName, fileBytes);
            }
        }
        catch (CosClientException clientEx)
        {
            throw new Exception(clientEx.Message);
        }
        catch (CosServerException serverEx)
        {
            throw new Exception(serverEx.Message);
        }
        return (string.Empty, Array.Empty<byte>());
    }

    public byte[] DownloadFileBytes(string key)
    {
        try
        {
            var request = new GetObjectBytesRequest(_fullBucketName, key);
            var result = CosXml.GetObject(request);
            if (result != null)
            {
                return result.content;
            }
        }
        catch (CosClientException clientEx)
        {
            throw new Exception(clientEx.Message);
        }
        catch (CosServerException serverEx)
        {
            throw new Exception(serverEx.Message);
        }
        return Array.Empty<byte>();
    }

    public List<string> GetDirFiles(string dir)
    {
        try
        {
            var request = new GetBucketRequest(_fullBucketName);
            request.SetPrefix($"{dir.TrimEnd('/')}/");
            request.SetDelimiter("/");

            var result = CosXml.GetBucket(request);

            var info = result.listBucket;

            var objects = info.contentsList;

            return objects.Where(o => o.size > 0).Select(o => o.key).ToList();

        }
        catch (CosClientException clientEx)
        {
            throw new Exception(clientEx.Message);
        }
        catch (CosServerException serverEx)
        {
            throw new Exception(serverEx.Message);
        }
    }

    public List<string> GetDirectories(string dir)
    {
        var dirs = new List<string>();
        try
        {
            var request = new GetBucketRequest(_fullBucketName);
            request.SetPrefix($"{dir.TrimEnd('/')}/");
            request.SetDelimiter("/");

            var result = CosXml.GetBucket(request);

            var info = result.listBucket;

            var objects = info.contentsList;

            var list = objects.Where(o => o.size == 0 && o.key != dir).Select(o => o.key).ToList();

            dirs.AddRange(list);

            var commonPrefixes = info.commonPrefixesList;

            dirs.AddRange(commonPrefixes.Select(c => c.prefix));

            return dirs;

        }
        catch (CosClientException clientEx)
        {
            throw new Exception(clientEx.Message);
        }
        catch (CosServerException serverEx)
        {
            throw new Exception(serverEx.Message);
        }
    }

    public bool DirExists(string dir)
    {
        try
        {
            var request = new GetBucketRequest(_fullBucketName);
            request.SetPrefix($"{dir.TrimEnd('/')}/");
            request.SetDelimiter("/");

            var result = CosXml.GetBucket(request);

            var info = result.listBucket;

            var objects = info.contentsList;

            return objects.Count > 0 || info?.commonPrefixesList.Count > 0;

        }
        catch (CosClientException clientEx)
        {
            throw new Exception(clientEx.Message);
        }
        catch (CosServerException serverEx)
        {
            throw new Exception(serverEx.Message);
        }
    }

    public void MoveDir(string sourceDir, string destDir)
    {
        var listRequest = new GetBucketRequest(_fullBucketName);

        listRequest.SetPrefix($"{sourceDir.TrimEnd('/')}/");
        var listResult = CosXml.GetBucket(listRequest);

        var info = listResult.listBucket;

        var objects = info.contentsList;

        foreach (var obj in objects)
        {
            string sourceKey = obj.key;
            string destinationKey = $"{destDir.TrimEnd('/')}/{sourceKey.Substring(sourceDir.Length)}";

            var copySource = new CopySourceStruct(_appId, _fullBucketName, _region, sourceKey);

            var request = new CopyObjectRequest(_fullBucketName, destinationKey);

            request.SetCopySource(copySource);
            try
            {

                var result = CosXml.CopyObject(request);
                var deleteRequest = new DeleteObjectRequest(_fullBucketName, sourceKey);
                var deleteResult = CosXml.DeleteObject(deleteRequest);
            }
            catch (CosClientException clientEx)
            {
                throw new Exception(clientEx.Message);
            }
            catch (CosServerException serverEx)
            {
                throw new Exception(serverEx.Message);
            }
        }

    }

    public void DeleteDir(string dir)
    {
        try
        {
            string nextMarker = null;
            do
            {
                var listRequest = new GetBucketRequest(_fullBucketName);
                listRequest.SetPrefix($"{dir.TrimEnd('/')}/");
                listRequest.SetMarker(nextMarker);
                var listResult = CosXml.GetBucket(listRequest);
                var info = listResult.listBucket;
                List<ListBucket.Contents> objects = info.contentsList;
                nextMarker = info.nextMarker;

                var deleteRequest = new DeleteMultiObjectRequest(_fullBucketName);

                deleteRequest.SetDeleteQuiet(false);
                var deleteObjects = new List<string>();
                foreach (var content in objects)
                {
                    deleteObjects.Add(content.key);
                }
                deleteRequest.SetObjectKeys(deleteObjects);

                var deleteResult = CosXml.DeleteMultiObjects(deleteRequest);

            } while (nextMarker != null);
        }
        catch (CosClientException clientEx)
        {
            throw new Exception(clientEx.Message);
        }
        catch (CosServerException serverEx)
        {
            throw new Exception(serverEx.Message);
        }
    }

    public bool DoesObjectExist(string key)
    {
        var request = new DoesObjectExistRequest(_fullBucketName, key);
        return CosXml.DoesObjectExist(request);
    }
}

