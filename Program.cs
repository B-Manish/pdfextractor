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

            StringBuilder csvContent = new StringBuilder();

            csvContent.AppendLine("Key,Value");

            foreach (var kvp in keyValuePairs)
            {
                csvContent.AppendLine($"{kvp.Key},{kvp.Value.Replace(",", "")}");
                Console.WriteLine($"{kvp.Key}:{kvp.Value}");
            }

            string csvFilePath = Path.Combine(Directory.GetCurrentDirectory(), "output.csv");

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

