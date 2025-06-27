using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Tokenizers.DotNet;

namespace RAGamuffin.Embedding;
public class OnnxEmbedder : IEmbedder
{
    public int Dimension => throw new NotImplementedException();

    public string ProviderName { get; set; } = "Onnx";

    private InferenceSession _session;
    private Tokenizer _tokenizer;


    public OnnxEmbedder(string model, string tokenizer)
    {
        _session = new InferenceSession(model);
        _tokenizer = new Tokenizer(tokenizer);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        uint[] rawIds = _tokenizer.Encode(text);

        int maxLen = 128;
        var ids = rawIds.Select(i => (long)i).ToList();
        var mask = Enumerable.Repeat(1L, ids.Count).ToList();

        if(ids.Count > maxLen)
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

        var idTensor = new DenseTensor<long>(new[] { 1, maxLen });
        var maskTensor = new DenseTensor<long>(new[] { 1, maxLen });
        var tokenTypeTensor = new DenseTensor<long>(new[] { 1, maxLen });
        for(int i = 0; i < maxLen; i++)
        {
            idTensor[0, i] = ids[i];
            maskTensor[0, i] = mask[i];
            tokenTypeTensor[0, i] = 0L; // all zeros for single sequence
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
        float[] embeddings;
        if (outputTensor.Dimensions.Length == 3)
        {
            // [1, seq_len, 128] - mean pool over seq_len
            int seqLen = outputTensor.Dimensions[1];
            int embDim = outputTensor.Dimensions[2];
            embeddings = new float[embDim];
            for (int j = 0; j < embDim; j++)
            {
                float sum = 0;
                for (int i = 0; i < seqLen; i++)
                    sum += outputTensor[0, i, j];
                embeddings[j] = sum / seqLen;
            }
        }
        else if (outputTensor.Dimensions.Length == 2)
        {
            // [1, 128] - just flatten
            embeddings = outputTensor.ToArray();
        }
        else
        {
            // fallback
            embeddings = outputTensor.ToArray();
        }
        return embeddings;
    }

    public Task<float[][]> EmbedAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
