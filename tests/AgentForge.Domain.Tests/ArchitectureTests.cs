using System.Reflection;
using AgentForge.Domain.Delivery;

namespace AgentForge.Domain.Tests;

/// <summary>
/// The boundary this project is built around, asserted rather than documented.
///
/// A comment saying "keep the domain clean" is a wish. These tests fail the
/// build the moment someone adds an SDK reference to the domain, which is the
/// single most common way an AI codebase rots: prompt strings and vendor types
/// leak inwards until the business rules are impossible to test offline.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(DeliveryTask).Assembly;

    [Fact]
    public void The_domain_depends_on_nothing_but_the_base_class_library()
    {
        var foreign = Domain
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => !name.StartsWith("System.", StringComparison.Ordinal)
                           && name is not ("System" or "netstandard" or "mscorlib"))
            .ToArray();

        Assert.True(
            foreign.Length == 0,
            $"The domain must stay free of frameworks and SDKs, but it references: {string.Join(", ", foreign)}");
    }

    [Theory]
    [InlineData("prompt")]
    [InlineData("llm")]
    [InlineData("chatclient")]
    [InlineData("completion")]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("embedding")]
    public void No_domain_type_knows_that_language_models_exist(string forbidden)
    {
        var offenders = Domain
            .GetTypes()
            .Where(t => t.IsPublic)
            .SelectMany(t => t
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => $"{t.Name}.{m.Name}")
                .Prepend(t.Name))
            .Where(name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"The domain records that work happened, never how it was produced. Found '{forbidden}' in: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Delivery_rules_live_on_the_aggregate_rather_than_in_a_service()
    {
        // If these move to an orchestrator or a "manager", the invariants stop
        // being enforceable and the tests above stop meaning anything.
        var behaviour = typeof(DeliveryTask)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToArray();

        Assert.Contains(nameof(DeliveryTask.OpenStep), behaviour);
        Assert.Contains(nameof(DeliveryTask.SettleStep), behaviour);
        Assert.Contains(nameof(DeliveryTask.RunGate), behaviour);
        Assert.Contains(nameof(DeliveryTask.BeginRework), behaviour);

        // No public setters: state changes only through the methods above.
        var setters = typeof(DeliveryTask)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToArray();

        Assert.True(setters.Length == 0, $"Public setters bypass the invariants: {string.Join(", ", setters)}");
    }
}
