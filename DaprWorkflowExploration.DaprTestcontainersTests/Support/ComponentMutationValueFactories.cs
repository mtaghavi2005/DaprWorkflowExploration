namespace DaprWorkflowExploration.DaprTestcontainersTests.Support;

internal static class ComponentMutationValueFactories
{
    public static string RedisHost(ComponentMutationContext context)
    {
        return context.RedisHost
               ?? throw new InvalidOperationException("RedisHost was requested for component mutation, but no Redis test container is available.");
    }

    public static Func<ComponentMutationContext, string> Constant(string value)
    {
        return _ => value;
    }
}
