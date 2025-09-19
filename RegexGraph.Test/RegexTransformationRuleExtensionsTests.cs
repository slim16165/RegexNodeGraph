using Microsoft.VisualStudio.TestTools.UnitTesting;
using RegexNodeGraph;
using RegexNodeGraph.RegexRules;

namespace UnitTestProject1;

[TestClass]
public class RegexTransformationRuleExtensionsTests
{
    [TestMethod]
    public void GetDisplayName_UsesDescriptionAndTruncates()
    {
        var rule = new RegexTransformationRule("pizza", "FOOD", ConfigOptions.NonUscireInCasoDiMatch)
        {
            Description = "Regola descrittiva lunghissima"
        };

        var displayName = rule.GetDisplayName(10);

        Assert.AreEqual("Regola de…", displayName);
    }

    [TestMethod]
    public void GetDisplayName_FallsBackToPattern()
    {
        var rule = new RegexTransformationRule("pizza", "FOOD", ConfigOptions.NonUscireInCasoDiMatch);

        var displayName = rule.GetDisplayName();

        Assert.AreEqual(rule.From, displayName);
    }
}
