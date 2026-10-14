using Dependinator.Bicep.Parsing;

namespace Dependinator.Bicep.Tests.Parsing;

public class AuxiliaryResourcesTests
{
    [Theory]
    [InlineData("Microsoft.Authorization/roleAssignments")]
    [InlineData("Microsoft.Insights/diagnosticSettings")]
    [InlineData("Microsoft.Authorization/locks")]
    [InlineData("Microsoft.Network/privateEndpoints/privateDnsZoneGroups")]
    [InlineData("Microsoft.Web/sites/config")]
    [InlineData("microsoft.web/SITES/CONFIG")]
    public void IsAuxiliary_ShouldBeTrue_ForSettingsAssignmentsAndLocks(string type)
    {
        Assert.True(AuxiliaryResources.IsAuxiliary(type));
    }

    [Theory]
    [InlineData("Microsoft.Web/sites")]
    [InlineData("Microsoft.Network/privateEndpoints")]
    [InlineData("Microsoft.Insights/metricAlerts")]
    [InlineData("Microsoft.Consumption/budgets")]
    public void IsAuxiliary_ShouldBeFalse_ForServicesWithAnIdentityOfTheirOwn(string type)
    {
        Assert.False(AuxiliaryResources.IsAuxiliary(type));
    }

    [Fact]
    public void AllTypes_ShouldBeFullProviderTypes()
    {
        Assert.All(AuxiliaryResources.AllTypes, t => Assert.Matches(@"^Microsoft\.\w+(/\w+)+$", t));
    }
}
