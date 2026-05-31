using System.Text.Json;

namespace DaprWorkflowExploration.IntegrationTests.TestUtils;

internal static class JsonAssert
{
    public static void ContainsJson(string actualJson, string expectedSubsetJson)
    {
        using var actual = JsonDocument.Parse(actualJson);
        using var expected = JsonDocument.Parse(expectedSubsetJson);

        AssertContains(actual.RootElement, expected.RootElement, "$");
    }

    public static bool TryContainsJson(string actualJson, string expectedSubsetJson)
    {
        try
        {
            ContainsJson(actualJson, expectedSubsetJson);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void AssertContains(JsonElement actual, JsonElement expected, string path)
    {
        Assert.AreEqual(expected.ValueKind, actual.ValueKind, $"JSON kind mismatch at {path}.");

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var expectedProperty in expected.EnumerateObject())
                {
                    Assert.IsTrue(
                        actual.TryGetProperty(expectedProperty.Name, out var actualProperty),
                        $"Missing JSON property at {path}.{expectedProperty.Name}.");

                    AssertContains(actualProperty, expectedProperty.Value, $"{path}.{expectedProperty.Name}");
                }
                break;

            case JsonValueKind.Array:
                var actualItems = actual.EnumerateArray().ToArray();
                var expectedItems = expected.EnumerateArray().ToArray();

                Assert.IsTrue(
                    actualItems.Length >= expectedItems.Length,
                    $"JSON array at {path} contains fewer items than expected.");

                for (var i = 0; i < expectedItems.Length; i++)
                {
                    AssertContains(actualItems[i], expectedItems[i], $"{path}[{i}]");
                }
                break;

            case JsonValueKind.String:
                Assert.AreEqual(expected.GetString(), actual.GetString(), $"JSON value mismatch at {path}.");
                break;

            case JsonValueKind.Number:
                Assert.AreEqual(expected.GetDecimal(), actual.GetDecimal(), $"JSON value mismatch at {path}.");
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.AreEqual(expected.GetBoolean(), actual.GetBoolean(), $"JSON value mismatch at {path}.");
                break;

            case JsonValueKind.Null:
                Assert.AreEqual(JsonValueKind.Null, actual.ValueKind, $"JSON value mismatch at {path}.");
                break;

            default:
                Assert.Fail($"Unsupported JSON value kind '{expected.ValueKind}' at {path}.");
                break;
        }
    }
}
