using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AgentForge.Llm.Fake;

/// <summary>
/// A deterministic <see cref="IChatClient"/> driven by a script.
///
/// This is the piece that makes the whole system testable. Every orchestration
/// test in this repository runs against it: no API key, no network, no cost,
/// and the same input always produces the same delivery. A rule can return a
/// different answer on each call, which is how the rework loop is exercised —
/// the security agent blocks on its first review and passes on its second.
/// </summary>
public sealed class ScriptedChatClient : IChatClient
{
    private readonly List<Rule> _rules = [];

    public int CallCount { get; private set; }

    public static ScriptedChatClient Create() => new();

    /// <summary>Replies are consumed in order; the last one repeats forever.</summary>
    public ScriptedChatClient When(Func<string, bool> matches, params string[] replies)
    {
        ArgumentOutOfRangeException.ThrowIfZero(replies.Length);
        _rules.Add(new Rule(matches, replies));
        return this;
    }

    public ScriptedChatClient WhenPromptContains(string needle, params string[] replies) =>
        When(prompt => prompt.Contains(needle, StringComparison.OrdinalIgnoreCase), replies);

    public ScriptedChatClient Otherwise(string reply) => When(_ => true, reply);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prompt = string.Join('\n', messages.Select(m => m.Text));
        var rule = _rules.Find(r => r.Matches(prompt))
                   ?? throw new InvalidOperationException(
                       $"No scripted reply matched this prompt. Add a rule for it:\n{Excerpt(prompt)}");

        CallCount++;
        var reply = rule.Next();

        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, reply))
        {
            ModelId = "scripted",
            Usage = new UsageDetails
            {
                // Deterministic on purpose: token counts must not drift between runs.
                InputTokenCount = prompt.Length / 4,
                OutputTokenCount = reply.Length / 4,
            },
        };

        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
        // Nothing to release; declared because IChatClient requires it.
    }

    private static string Excerpt(string prompt) =>
        prompt.Length <= 400 ? prompt : prompt[..400] + " …";

    private sealed class Rule(Func<string, bool> matches, string[] replies)
    {
        private int _cursor;

        public bool Matches(string prompt) => matches(prompt);

        public string Next()
        {
            var reply = replies[Math.Min(_cursor, replies.Length - 1)];
            _cursor++;
            return reply;
        }
    }
}
