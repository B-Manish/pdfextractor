using System;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract;
using Amazon.Textract.Model;
using DotNetEnv;
using System.Text.RegularExpressions;
using System.Net;
using System.Diagnostics;

class Program
{
    static async Task Main(string[] args)
    {
        DotNetEnv.Env.Load();
        string filePath=@"C:\Users\johndoe\Downloads\sample.pdf";
        var textractClient = new AmazonTextractClient(Amazon.RegionEndpoint.USEast1);
        string bucketName = Environment.GetEnvironmentVariable("AWS_BUCKET_NAME");
        string key = $"pdf_{Guid.NewGuid()}"; // to generate a unique key when multiple users upload at the same time to avoid overwriting the file
        string region = Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION");


        var s3Client = new AmazonS3Client(RegionEndpoint.GetBySystemName(region));



        string presignedUrl = GeneratePreSignedURL(s3Client, bucketName, key, TimeSpan.FromMinutes(15));

        // uploads the file to s3 using a presigned url
        await UploadFileToS3(presignedUrl, filePath);

        var document = new Document
        {
            S3Object = new Amazon.Textract.Model.S3Object
            {
                Bucket = bucketName,
                Name = key
            }
        };

        var analyzeRequest = new AnalyzeDocumentRequest
        {
            Document = document,
            FeatureTypes = new List<string> { "FORMS" }
        };

        try
        {
            var analyzeResponse = await textractClient.AnalyzeDocumentAsync(analyzeRequest);

            Dictionary<string, string> keyValuePairs = GetKeyValuePairs(analyzeResponse.Blocks);

            StringBuilder csvContent = new StringBuilder();

            csvContent.AppendLine("Key,Value");

            foreach (var kvp in keyValuePairs)
            {
                csvContent.AppendLine($"{kvp.Key},{kvp.Value.Replace(",", "")}");
                // Console.WriteLine($"{kvp.Key}:{kvp.Value}");
            }

            string csvFilePath = Path.Combine(Directory.GetCurrentDirectory(), "output.csv");

            File.WriteAllText(csvFilePath, csvContent.ToString());

            Console.WriteLine($"CSV file created at: {csvFilePath}");
            await DeleteFileFromS3Async(s3Client, bucketName, key);

        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {e.Message}");
            if (e.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {e.InnerException.Message}");
            }
        }
    }

    private static Dictionary<string, string> GetKeyValuePairs(List<Block> blocks)
    {
        var keyMap = new Dictionary<string, Block>();
        var valueMap = new Dictionary<string, Block>();
        var keyValueMap = new Dictionary<string, string>();

        foreach (var block in blocks)
        {
            if (block.BlockType == BlockType.KEY_VALUE_SET && block.EntityTypes.Contains("KEY"))
            {
                keyMap[block.Id] = block;
            }
            else if (block.BlockType == BlockType.KEY_VALUE_SET && block.EntityTypes.Contains("VALUE"))
            {
                valueMap[block.Id] = block;
            }
        }

        foreach (var keyBlock in keyMap.Values)
        {
            var valueBlockId = keyBlock.Relationships.Find(r => r.Type == RelationshipType.VALUE)?.Ids[0];
            if (valueBlockId != null && valueMap.TryGetValue(valueBlockId, out var valueBlock))
            {
                var keyText = GetTextFromBlock(keyBlock, blocks);
                var valueText = GetTextFromBlock(valueBlock, blocks);
                keyValueMap[keyText] = valueText;
            }
        }

        return keyValueMap;
    }

    private static string GetTextFromBlock(Block block, List<Block> blocks)
    {
        var text = string.Empty;

        if (block.Relationships != null)
        {
            foreach (var relationship in block.Relationships)
            {
                if (relationship.Type == RelationshipType.CHILD)
                {
                    foreach (var childId in relationship.Ids)
                    {
                        var childBlock = blocks.Find(b => b.Id == childId);
                        if (childBlock != null && (childBlock.BlockType == BlockType.WORD || childBlock.BlockType == BlockType.LINE))

                        {
                            text += childBlock.Text + " ";
                        }
                    }
                }
            }
        }

        return text.Trim();
    }

// helper function to generate Presigned url(used to upload the file to s3 bucket.)
    static string GeneratePreSignedURL(IAmazonS3 s3Client, string bucketName, string objectKey, TimeSpan expiryDuration)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = objectKey,
            Expires = DateTime.UtcNow.Add(expiryDuration),
            Verb = HttpVerb.PUT
        };

        Console.WriteLine("s3Client.GetPreSignedURL(request)" + s3Client.GetPreSignedURL(request));
        return s3Client.GetPreSignedURL(request);
    }

// helper function to upload a file to s3 using presined url
    public static async Task UploadFileToS3(string presignedUrl, string filePath)
    {
        try
        {
            string curlCommand = $"curl -X PUT -T \"{filePath}\" \"{presignedUrl}\"";

            // Execute the curl command
            ProcessStartInfo processStartInfo = new ProcessStartInfo("cmd", $"/c {curlCommand}");
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.CreateNoWindow = true;

            using (Process process = Process.Start(processStartInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                Console.WriteLine("Upload result: " + output);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }


// helper function to delete file from s3
    static async Task DeleteFileFromS3Async(IAmazonS3 s3Client, string bucketName, string key)
    {
        try
        {
            var deleteObjectRequest = new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = key
            };

            DeleteObjectResponse response = await s3Client.DeleteObjectAsync(deleteObjectRequest);
            Console.WriteLine($"File with key '{key}' deleted successfully from bucket '{bucketName}'.");
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"Error encountered on server. Message: '{ex.Message}'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unknown error encountered. Message: '{ex.Message}'");
        }
    }
}
