namespace AgentForge.Llm;

/// <summary>
/// The only thing agents are allowed to ask a model for: a typed result.
///
/// Free-form text never leaves this layer. If the model cannot produce the
/// requested shape, that is an error here rather than a parsing surprise
/// three layers up.
/// </summary>
public interface IStructuredModel
{
    string ModelId { get; }

    Task<LlmResult<T>> CompleteAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
        where T : class;
}
