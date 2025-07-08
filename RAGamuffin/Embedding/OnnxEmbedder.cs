using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using RAGamuffin.Abstractions;
using RAGamuffin.Core;
using Tokenizers.DotNet;

namespace RAGamuffin.Embedding;
public class OnnxEmbedder : IEmbedder
{
    public int Dimension { get; private set; }

    public string ProviderName { get; set; } = "Onnx";

    private InferenceSession _session;
    private Tokenizer _tokenizer;

    public OnnxEmbedder(string model, string tokenizer)
    {
        _session = new InferenceSession(model);
        _tokenizer = new Tokenizer(tokenizer);
        
        // Determine the embedding dimension from the model output
        Dimension = DetermineEmbeddingDimension();
    }

    private int DetermineEmbeddingDimension()
    {
        try
        {
            // Get the output metadata to determine the embedding dimension
            var outputMeta = _session.OutputMetadata;
            if (outputMeta.Count > 0)
            {
                var firstOutput = outputMeta.First();
                var shape = firstOutput.Value.Dimensions;
                
                if (shape.Length == 3)
                {
                    // [batch_size, seq_len, emb_dim] - return the embedding dimension
                    return (int)shape[2];
                }
                else if (shape.Length == 2)
                {
                    // [batch_size, emb_dim] - return the embedding dimension
                    return (int)shape[1];
                }
                else if (shape.Length == 1)
                {
                    // [emb_dim] - return the embedding dimension
                    return (int)shape[0];
                }
            }
            
            // Fallback: try to infer from the model by running a dummy inference
            var dummyText = "test";
            var dummyEmbedding = EmbedBatchSync(new[] { dummyText });
            if (dummyEmbedding.Length > 0 && dummyEmbedding[0].Length > 0)
            {
                return dummyEmbedding[0].Length;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not determine embedding dimension from model: {ex.Message}");
        }
        
        // Default fallback dimension
        Console.WriteLine("Warning: Using default embedding dimension of 768");
        return 768;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var results = await EmbedBatchAsync(new[] { text }, cancellationToken);
        return results[0];
    }

    /// <summary>
    /// Embeds multiple texts in a single batch operation for much better performance
    /// </summary>
    /// <param name="texts">Array of texts to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of embeddings, one for each input text</returns>
    public async Task<float[][]> EmbedBatchAsync(string[] texts, CancellationToken cancellationToken = default)
    {
        if (texts == null || texts.Length == 0)
        {
            return new float[0][];
        }

        return await Task.Run(() => EmbedBatchSync(texts), cancellationToken);
    }

    private static string SanitizeUtf16(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        // 1. Replace invalid UTF-16 sequences
        var encoding = System.Text.Encoding.GetEncoding(
            "UTF-8",
            new System.Text.EncoderReplacementFallback(""),
            new System.Text.DecoderReplacementFallback("")
        );
        var bytes = encoding.GetBytes(input);
        string sanitized = encoding.GetString(bytes);

        // 2. Remove control characters except newline (\n) and tab (\t)
        sanitized = new string(sanitized.Where(c =>
            (c >= 0x20 || c == '\n' || c == '\t') &&
            (c < 0x7F || c > 0x9F || c == '\n' || c == '\t')
        ).ToArray());

        // 3. Normalize Unicode to NFC
        sanitized = sanitized.Normalize(System.Text.NormalizationForm.FormC);

        // 4. Remove zero-width and directional formatting characters
        // (e.g., U+200B..U+200F, U+202A..U+202E, U+2060..U+206F, U+FEFF)
        int[] invisible = new int[] {
            0x200B, 0x200C, 0x200D, 0x200E, 0x200F,
            0x202A, 0x202B, 0x202C, 0x202D, 0x202E,
            0x2060, 0x2061, 0x2062, 0x2063, 0x2064, 0x2066, 0x2067, 0x2068, 0x2069, 0x206A, 0x206B, 0x206C, 0x206D, 0x206E, 0x206F,
            0xFEFF
        };
        sanitized = new string(sanitized.Where(c => !invisible.Contains((int)c)).ToArray());

        return sanitized;
    }

    private float[][] EmbedBatchSync(string[] texts)
    {
        int batchSize = texts.Length;
        int maxLen = 128;
        
        // Prepare batch tensors
        var idTensor = new DenseTensor<long>(new[] { batchSize, maxLen });
        var maskTensor = new DenseTensor<long>(new[] { batchSize, maxLen });
        var tokenTypeTensor = new DenseTensor<long>(new[] { batchSize, maxLen });

        // Process each text in the batch
        for (int batchIndex = 0; batchIndex < batchSize; batchIndex++)
        {
            string text = texts[batchIndex];
            // Sanitize text to ensure valid UTF-16
            text = SanitizeUtf16(text);
            uint[] rawIds = _tokenizer.Encode(text);

            var ids = rawIds.Select(i => (long)i).ToList();
            var mask = Enumerable.Repeat(1L, ids.Count).ToList();

            if (ids.Count > maxLen)
            {
                ids = ids.Take(maxLen).ToList();
                mask = mask.Take(maxLen).ToList();
            }
            else
            {
                int pad = maxLen - ids.Count;
                ids.AddRange(Enumerable.Repeat(0L, pad));
                mask.AddRange(Enumerable.Repeat(0L, pad));
            }

            // Fill tensors for this batch item
            for (int i = 0; i < maxLen; i++)
            {
                idTensor[batchIndex, i] = ids[i];
                maskTensor[batchIndex, i] = mask[i];
                tokenTypeTensor[batchIndex, i] = 0L; // all zeros for single sequence
            }
        }

        var inputMeta = _session.InputMetadata;
        var inputNames = inputMeta.Keys.ToList();

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", idTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor)
        };
        if (inputNames.Contains("token_type_ids"))
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeTensor));
        }

        using var results = _session.Run(inputs);
        var outputTensor = results.First().AsTensor<float>();
        
        // Process batch output
        float[][] embeddings = new float[batchSize][];
        
        if (outputTensor.Dimensions.Length == 3)
        {
            // [batch_size, seq_len, emb_dim] - mean pool over seq_len for each batch item
            int seqLen = outputTensor.Dimensions[1];
            int embDim = outputTensor.Dimensions[2];
            
            for (int batchIndex = 0; batchIndex < batchSize; batchIndex++)
            {
                embeddings[batchIndex] = new float[embDim];
                for (int j = 0; j < embDim; j++)
                {
                    float sum = 0;
                    for (int i = 0; i < seqLen; i++)
                        sum += outputTensor[batchIndex, i, j];
                    embeddings[batchIndex][j] = sum / seqLen;
                }
            }
        }
        else if (outputTensor.Dimensions.Length == 2)
        {
            // [batch_size, emb_dim] - just extract each batch item
            int embDim = outputTensor.Dimensions[1];
            for (int batchIndex = 0; batchIndex < batchSize; batchIndex++)
            {
                embeddings[batchIndex] = new float[embDim];
                for (int j = 0; j < embDim; j++)
                {
                    embeddings[batchIndex][j] = outputTensor[batchIndex, j];
                }
            }
        }
        else
        {
            // fallback - flatten and split
            var flatArray = outputTensor.ToArray();
            int embDim = flatArray.Length / batchSize;
            for (int batchIndex = 0; batchIndex < batchSize; batchIndex++)
            {
                embeddings[batchIndex] = new float[embDim];
                Array.Copy(flatArray, batchIndex * embDim, embeddings[batchIndex], 0, embDim);
            }
        }
        
        return embeddings;
    }
}
