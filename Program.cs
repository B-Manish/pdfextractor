using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Textract;
using Amazon.Textract.Model;
using DotNetEnv;
using System.Text.RegularExpressions;

class Program
{
    static async Task Main(string[] args)
    {
        DotNetEnv.Env.Load();
        var textractClient = new AmazonTextractClient(Amazon.RegionEndpoint.USEast1);
        string bucketName = "ocr-nlp";
        string key = "invoice1.pdf";

        var document = new Document
        {
            S3Object = new S3Object
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

            // Create a StringBuilder to hold the CSV content
            StringBuilder csvContent = new StringBuilder();

            // Add header row to the CSV
            csvContent.AppendLine("Key,Value");

            // Iterate through the key-value pairs and add each one to the CSV content
            foreach (var kvp in keyValuePairs)
            {
                csvContent.AppendLine($"{kvp.Key},{kvp.Value}");
                Console.WriteLine($"{kvp.Key}:{kvp.Value}");
            }

            // Define the path to save the CSV file in the current directory
            string csvFilePath = Path.Combine(Directory.GetCurrentDirectory(), "output.csv");

            // Write the CSV content to the file
            File.WriteAllText(csvFilePath, csvContent.ToString());

            Console.WriteLine($"CSV file created at: {csvFilePath}");
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

}




// using System;
// using System.IO;
// using System.Net.Http;
// using System.Security.Cryptography;
// using System.Text;
// using System.Threading.Tasks;

// class Program
// {
//     private static readonly string accessKey = "AKIATCKATMPUBNE6OJ6W";
//     private static readonly string secretKey = "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";
//     private static readonly string region = "us-east-1";
//     private static readonly string service = "textract";
//     private static readonly string host = $"textract.{region}.amazonaws.com";
//     private static readonly string endpoint = $"https://{host}/";
//     private static readonly string target = "Textract.DetectDocumentText";

//     static async Task Main(string[] args)
//     {
//         // Replace this with the path to your PDF/image file
//         string filePath = @"C:\Users\manish.batchu\Downloads\invoice1.pdf";
//         byte[] fileData = File.ReadAllBytes(filePath);

//         // Construct the request
//         string amzDate = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
//         string dateStamp = DateTime.UtcNow.ToString("yyyyMMdd");

//         string payloadHash = HashPayload(fileData);
//         string canonicalRequest = CreateCanonicalRequest(payloadHash, amzDate);

//         string stringToSign = CreateStringToSign(canonicalRequest, amzDate, dateStamp);
//         string signature = CalculateSignature(stringToSign, dateStamp);

//         string authorizationHeader = $"AWS4-HMAC-SHA256 Credential={accessKey}/{dateStamp}/{region}/{service}/aws4_request, SignedHeaders=content-type;host;x-amz-date;x-amz-target, Signature={signature}";

//         using (HttpClient httpClient = new HttpClient())
//         {
//             HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint);

//             request.Headers.Add("X-Amz-Date", amzDate);
//             request.Headers.Add("X-Amz-Target", "Textract.DetectDocumentText");
//             request.Headers.Add("Authorization", authorizationHeader);
//             request.Headers.Add("Content-Type", "application/x-amz-json-1.1");

//             // Add file content
//             request.Content = new ByteArrayContent(fileData);

//             // Send request
//             HttpResponseMessage response = await httpClient.SendAsync(request);
//             string responseBody = await response.Content.ReadAsStringAsync();

//             Console.WriteLine("Response: " + responseBody);
//         }

//     }

//     // Create Canonical Request
//     private static string CreateCanonicalRequest(string payloadHash, string amzDate)
//     {
//         string canonicalUri = "/";
//         string canonicalQueryString = "";
//         string canonicalHeaders = $"content-type:application/x-amz-json-1.1\nhost:{host}\nx-amz-date:{amzDate}\nx-amz-target:{target}\n";
//         string signedHeaders = "content-type;host;x-amz-date;x-amz-target";
//         return $"POST\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
//     }

//     // Create String to Sign
//     private static string CreateStringToSign(string canonicalRequest, string amzDate, string dateStamp)
//     {
//         string algorithm = "AWS4-HMAC-SHA256";
//         string credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
//         string canonicalRequestHash = HashString(canonicalRequest);
//         return $"{algorithm}\n{amzDate}\n{credentialScope}\n{canonicalRequestHash}";
//     }

//     // Calculate Signature
//     private static string CalculateSignature(string stringToSign, string dateStamp)
//     {
//         byte[] signingKey = GetSignatureKey(secretKey, dateStamp, region, service);
//         return ToHexString(HmacSHA256(stringToSign, signingKey));
//     }

//     // Hash payload (image or PDF data)
//     private static string HashPayload(byte[] payload)
//     {
//         using (SHA256 sha256 = SHA256.Create())
//         {
//             return ToHexString(sha256.ComputeHash(payload));
//         }
//     }

//     // Utility functions for signing
//     private static string HashString(string text)
//     {
//         using (SHA256 sha256 = SHA256.Create())
//         {
//             return ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(text)));
//         }
//     }

//     private static byte[] HmacSHA256(string data, byte[] key)
//     {
//         using (HMACSHA256 hmac = new HMACSHA256(key))
//         {
//             return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
//         }
//     }

//     private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
//     {
//         byte[] kDate = HmacSHA256(dateStamp, Encoding.UTF8.GetBytes("AWS4" + key));
//         byte[] kRegion = HmacSHA256(regionName, kDate);
//         byte[] kService = HmacSHA256(serviceName, kRegion);
//         return HmacSHA256("aws4_request", kService);
//     }

//     private static string ToHexString(byte[] bytes)
//     {
//         StringBuilder sb = new StringBuilder();
//         foreach (byte b in bytes)
//         {
//             sb.Append(b.ToString("x2"));
//         }
//         return sb.ToString();
//     }



// }




// using System;
// using System.IO;
// using System.Net.Http;
// using System.Security.Cryptography;
// using System.Text;
// using System.Threading.Tasks;

// class Program
// {
//     private static readonly string accessKey = "AKIATCKATMPUBNE6OJ6W";
//     private static readonly string secretKey = "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";
//     private static readonly string region = "us-east-1"; 
//     private static readonly string service = "textract";
//     private static readonly string host = $"textract.{region}.amazonaws.com";
//     private static readonly string endpoint = $"https://{host}/";
//     private static readonly string target = "Textract.DetectDocumentText";

//     static async Task Main(string[] args)
//     {
//         // Replace this with the path to your PDF/image file
//         string filePath = @"C:\Users\manish.batchu\Downloads\invoice1.pdf";
//         byte[] fileData = File.ReadAllBytes(filePath);

//         // Construct the request
//         string amzDate = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
//         string dateStamp = DateTime.UtcNow.ToString("yyyyMMdd");

//         string payloadHash = HashPayload(fileData);
//         string canonicalRequest = CreateCanonicalRequest(payloadHash, amzDate);

//         string stringToSign = CreateStringToSign(canonicalRequest, amzDate, dateStamp);
//         string signature = CalculateSignature(stringToSign, dateStamp);

//         // Ensure proper spacing in the Authorization header
//         string authorizationHeader = $"AWS4-HMAC-SHA256 Credential={accessKey}/{dateStamp}/{region}/{service}/aws4_request, SignedHeaders=content-type;host;x-amz-date;x-amz-target, Signature={signature}";

//         using (HttpClient httpClient = new HttpClient())
//         {
//             HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint);

//             request.Headers.Add("X-Amz-Date", amzDate);
//             request.Headers.Add("X-Amz-Target", target);
//             request.Headers.Add("Authorization", authorizationHeader); // Ensure proper header value
//             request.Headers.Add("Content-Type", "application/x-amz-json-1.1");

//             // Add file content
//             request.Content = new ByteArrayContent(fileData);

//             // Send request
//             HttpResponseMessage response = await httpClient.SendAsync(request);
//             string responseBody = await response.Content.ReadAsStringAsync();

//             Console.WriteLine("Response: " + responseBody);
//         }
//     }

//     // Create Canonical Request
//     private static string CreateCanonicalRequest(string payloadHash, string amzDate)
//     {
//         string canonicalUri = "/";
//         string canonicalQueryString = "";
//         string canonicalHeaders = $"content-type:application/x-amz-json-1.1\nhost:{host}\nx-amz-date:{amzDate}\nx-amz-target:{target}\n";
//         string signedHeaders = "content-type;host;x-amz-date;x-amz-target";
//         return $"POST\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
//     }

//     // Create String to Sign
//     private static string CreateStringToSign(string canonicalRequest, string amzDate, string dateStamp)
//     {
//         string algorithm = "AWS4-HMAC-SHA256";
//         string credentialScope = $"{dateStamp}/{region}/{service}/aws4_request";
//         string canonicalRequestHash = HashString(canonicalRequest);
//         return $"{algorithm}\n{amzDate}\n{credentialScope}\n{canonicalRequestHash}";
//     }

//     // Calculate Signature
//     private static string CalculateSignature(string stringToSign, string dateStamp)
//     {
//         byte[] signingKey = GetSignatureKey(secretKey, dateStamp, region, service);
//         return ToHexString(HmacSHA256(stringToSign, signingKey));
//     }

//     // Hash payload (image or PDF data)
//     private static string HashPayload(byte[] payload)
//     {
//         using (SHA256 sha256 = SHA256.Create())
//         {
//             return ToHexString(sha256.ComputeHash(payload));
//         }
//     }

//     // Utility functions for signing
//     private static string HashString(string text)
//     {
//         using (SHA256 sha256 = SHA256.Create())
//         {
//             return ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(text)));
//         }
//     }

//     private static byte[] HmacSHA256(string data, byte[] key)
//     {
//         using (HMACSHA256 hmac = new HMACSHA256(key))
//         {
//             return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
//         }
//     }

//     private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
//     {
//         byte[] kDate = HmacSHA256(dateStamp, Encoding.UTF8.GetBytes("AWS4" + key));
//         byte[] kRegion = HmacSHA256(regionName, kDate);
//         byte[] kService = HmacSHA256(serviceName, kRegion);
//         return HmacSHA256("aws4_request", kService);
//     }

//     private static string ToHexString(byte[] bytes)
//     {
//         StringBuilder sb = new StringBuilder();
//         foreach (byte b in bytes)
//         {
//             sb.Append(b.ToString("x2"));
//         }
//         return sb.ToString();
//     }
// }

