using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using RAGamuffin.Abstractions;
using Tokenizers.DotNet;

namespace RAGamuffin.Embedding;

/// <summary>
/// ONNX-based embedding provider for generating vector embeddings from text.
/// Uses ONNX Runtime for high-performance inference with local embedding models.
/// </summary>
public class OnnxEmbedder : IEmbedder
{
    private readonly InferenceSession _session;
    private readonly Tokenizer _tokenizer;
    private readonly int _maxSequenceLength;
    private readonly int _embeddingDimension;

    /// <summary>
    /// Gets the dimension of the embedding vectors produced by this embedder.
    /// </summary>
    public int Dimension => _embeddingDimension;

    /// <summary>
    /// Gets or sets the provider name for this embedder.
    /// </summary>
    public string ProviderName { get; set; } = "Onnx";

    /// <summary>
    /// Initializes a new instance of the OnnxEmbedder.
    /// </summary>
    /// <param name="modelPath">Path to the ONNX model file</param>
    /// <param name="tokenizerPath">Path to the tokenizer configuration file</param>
    /// <param name="maxSequenceLength">Maximum sequence length for tokenization (default: 128)</param>
    /// <exception cref="ArgumentNullException">Thrown when modelPath or tokenizerPath is null or empty</exception>
    /// <exception cref="FileNotFoundException">Thrown when model or tokenizer files are not found</exception>
    /// <exception cref="InvalidOperationException">Thrown when the model or tokenizer cannot be loaded</exception>
    public OnnxEmbedder(string modelPath, string tokenizerPath, int maxSequenceLength = 128)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            throw new ArgumentException("Model path cannot be null or empty.", nameof(modelPath));
        }

        if (string.IsNullOrWhiteSpace(tokenizerPath))
        {
            throw new ArgumentException("Tokenizer path cannot be null or empty.", nameof(tokenizerPath));
        }

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"ONNX model file not found at: {modelPath}");
        }

        if (!File.Exists(tokenizerPath))
        {
            throw new FileNotFoundException($"Tokenizer file not found at: {tokenizerPath}");
        }

        try
        {
            _session = new InferenceSession(modelPath);
            _tokenizer = new Tokenizer(tokenizerPath);
            _maxSequenceLength = maxSequenceLength;
            
            // Determine embedding dimension from model output
            _embeddingDimension = DetermineEmbeddingDimension();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize ONNX embedder. Please ensure the model and tokenizer files are valid.", ex);
        }
    }

    /// <summary>
    /// Generates a vector embedding for a single text input.
    /// </summary>
    /// <param name="text">The text to embed</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>A vector embedding representing the input text</returns>
    /// <exception cref="ArgumentNullException">Thrown when text is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when embedding generation fails</exception>
    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text), "Text cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[_embeddingDimension]; // Return zero vector for empty text
        }

        try
        {
            return await Task.Run(() => GenerateEmbedding(text), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to generate embedding for text. The ONNX model inference may have failed.", ex);
        }
    }

    /// <summary>
    /// Generates vector embeddings for multiple text inputs.
    /// </summary>
    /// <param name="texts">Collection of texts to embed</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>Array of vector embeddings, one for each input text</returns>
    /// <exception cref="ArgumentNullException">Thrown when texts is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when embedding generation fails</exception>
    public async Task<float[][]> EmbedAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        if (texts is null)
        {
            throw new ArgumentNullException(nameof(texts), "Texts collection cannot be null.");
        }

        var textArray = texts.ToArray();
        if (textArray.Length == 0)
        {
            return Array.Empty<float[]>();
        }

        try
        {
            var embeddings = new float[textArray.Length][];
            
            for (int i = 0; i < textArray.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                embeddings[i] = await EmbedAsync(textArray[i], cancellationToken);
            }

            return embeddings;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to generate embeddings for multiple texts.", ex);
        }
    }

    /// <summary>
    /// Generates a vector embedding for the given text using ONNX inference.
    /// </summary>
    /// <param name="text">The text to embed</param>
    /// <returns>A vector embedding representing the input text</returns>
    private float[] GenerateEmbedding(string text)
    {
        // Tokenize the input text
        var tokenIds = TokenizeText(text);

        // Create input tensors
        var inputTensors = CreateInputTensors(tokenIds);

        // Run inference
        using var results = _session.Run(inputTensors);
        var outputTensor = results.First().AsTensor<float>();

        // Process the output tensor
        return ProcessOutputTensor(outputTensor);
    }

    /// <summary>
    /// Tokenizes the input text and prepares it for the model.
    /// </summary>
    /// <param name="text">The text to tokenize</param>
    /// <returns>Token IDs ready for model input</returns>
    private long[] TokenizeText(string text)
    {
        var rawIds = _tokenizer.Encode(text);
        var ids = rawIds.Select(i => (long)i).ToList();

        // Truncate or pad to max sequence length
        if (ids.Count > _maxSequenceLength)
        {
            ids = ids.Take(_maxSequenceLength).ToList();
        }
        else
        {
            int paddingLength = _maxSequenceLength - ids.Count;
            ids.AddRange(Enumerable.Repeat(0L, paddingLength));
        }

        return ids.ToArray();
    }

    /// <summary>
    /// Creates input tensors for the ONNX model.
    /// </summary>
    /// <param name="tokenIds">Token IDs for the input text</param>
    /// <returns>List of NamedOnnxValue inputs for the model</returns>
    private List<NamedOnnxValue> CreateInputTensors(long[] tokenIds)
    {
        // Create attention mask (1 for real tokens, 0 for padding)
        var attentionMask = new long[_maxSequenceLength];
        for (int i = 0; i < tokenIds.Length; i++)
        {
            attentionMask[i] = tokenIds[i] != 0 ? 1L : 0L;
        }

        // Create tensors
        var idTensor = new DenseTensor<long>(tokenIds, new[] { 1, _maxSequenceLength });
        var maskTensor = new DenseTensor<long>(attentionMask, new[] { 1, _maxSequenceLength });

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", idTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", maskTensor)
        };

        // Add token type IDs if the model expects them
        var inputNames = _session.InputMetadata.Keys.ToList();
        if (inputNames.Contains("token_type_ids"))
        {
            var tokenTypeIds = new long[_maxSequenceLength]; // All zeros for single sequence
            var tokenTypeTensor = new DenseTensor<long>(tokenTypeIds, new[] { 1, _maxSequenceLength });
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeTensor));
        }

        return inputs;
    }

    /// <summary>
    /// Processes the output tensor from the ONNX model to extract embeddings.
    /// </summary>
    /// <param name="outputTensor">The output tensor from model inference</param>
    /// <returns>Processed embedding vector</returns>
    private float[] ProcessOutputTensor(Tensor<float> outputTensor)
    {
        if (outputTensor.Dimensions.Length == 3)
        {
            // [batch_size, sequence_length, embedding_dim] - apply mean pooling
            return ApplyMeanPooling(outputTensor);
        }
        else if (outputTensor.Dimensions.Length == 2)
        {
            // [batch_size, embedding_dim] - direct output
            return outputTensor.ToArray();
        }
        else
        {
            // Fallback: flatten the tensor
            return outputTensor.ToArray();
        }
    }

    /// <summary>
    /// Applies mean pooling over the sequence dimension to get a single embedding vector.
    /// </summary>
    /// <param name="tensor">3D tensor with shape [batch_size, sequence_length, embedding_dim]</param>
    /// <returns>Mean-pooled embedding vector</returns>
    private float[] ApplyMeanPooling(Tensor<float> tensor)
    {
        int sequenceLength = tensor.Dimensions[1];
        int embeddingDim = tensor.Dimensions[2];

        var embeddings = new float[embeddingDim];

        for (int j = 0; j < embeddingDim; j++)
        {
            float sum = 0f;
            for (int i = 0; i < sequenceLength; i++)
            {
                sum += tensor[0, i, j];
            }
            embeddings[j] = sum / sequenceLength;
        }

        return embeddings;
    }

    /// <summary>
    /// Determines the embedding dimension from the model's output metadata.
    /// </summary>
    /// <returns>The embedding dimension</returns>
    private int DetermineEmbeddingDimension()
    {
        try
        {
            // Create a dummy input to determine output shape
            var dummyIds = new long[_maxSequenceLength];
            var dummyMask = Enumerable.Repeat(1L, _maxSequenceLength).ToArray();
            
            var dummyInputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(dummyIds, new[] { 1, _maxSequenceLength })),
                NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(dummyMask, new[] { 1, _maxSequenceLength }))
            };

            using var results = _session.Run(dummyInputs);
            var outputTensor = results.First().AsTensor<float>();
            
            // Return the last dimension as the embedding dimension
            return outputTensor.Dimensions[^1];
        }
        catch
        {
            // Fallback to a common embedding dimension
            return 768;
        }
    }

    /// <summary>
    /// Disposes of the ONNX embedder and releases resources.
    /// </summary>
    public void Dispose()
    {
        _session?.Dispose();
    }
}
