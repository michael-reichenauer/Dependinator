using Bicep.Core.TypeSystem;
using Dependinator.Bicep.Parsing;
using Dependinator.UI.Diagrams.Icons;

namespace Dependinator.Bicep.Tests.Parsing;

public class AzureIconsTests
{
    [Theory]
    [InlineData("Microsoft.Web/sites", "App-Service")]
    [InlineData("Microsoft.Storage/storageAccounts", "Storage-Account")]
    [InlineData("Microsoft.KeyVault/vaults", "Key-Vault")]
    [InlineData("Microsoft.Resources/resourceGroups", "Resource-Group")]
    public void ForResourceType_ShouldMapKnownTypes(string type, string expected)
    {
        Assert.Equal(expected, AzureIcons.ForResourceType(type));
    }

    [Theory]
    [InlineData("Microsoft.Storage/storageAccounts/blobServices/containers", "Blob-Storage")]
    [InlineData("Microsoft.Web/sites/slots", "App-Service")]
    [InlineData("Microsoft.KeyVault/vaults/secrets", "Key-Vault")]
    [InlineData("Microsoft.Network/virtualNetworks/subnets", "Virtual-Network")]
    public void ForResourceType_ShouldFallBackToTheParentType_ForChildTypes(string type, string expected)
    {
        Assert.Equal(expected, AzureIcons.ForResourceType(type));
    }

    [Theory]
    [InlineData("Microsoft.Network/ddosProtectionPlans", "Network")]
    [InlineData("Microsoft.Insights/metricAlerts", "Monitor")]
    [InlineData("Microsoft.Authorization/locks", "Security")]
    public void ForResourceType_ShouldFallBackToTheProviderDefault(string type, string expected)
    {
        Assert.Equal(expected, AzureIcons.ForResourceType(type));
    }

    [Theory]
    [InlineData("Microsoft.Unknown/things")]
    [InlineData("Custom.Provider/widgets/parts")]
    [InlineData("")]
    public void ForResourceType_ShouldFallBackToCloud_ForUnknownTypes(string type)
    {
        Assert.Equal(AzureIcons.Fallback, AzureIcons.ForResourceType(type));
    }

    [Fact]
    public void ForResourceType_ShouldIgnoreCase()
    {
        Assert.Equal("App-Service", AzureIcons.ForResourceType("microsoft.web/SITES"));
    }

    [Fact]
    public void ForScope_ShouldMapDeploymentScopes()
    {
        Assert.Equal("Resource-Group", AzureIcons.ForScope(ResourceScope.ResourceGroup));
        Assert.Equal("Subscription", AzureIcons.ForScope(ResourceScope.Subscription));
        Assert.Equal("Management-Group", AzureIcons.ForScope(ResourceScope.ManagementGroup));
        Assert.Null(AzureIcons.ForScope(ResourceScope.Tenant));
    }

    [Fact]
    public void AllIconNames_ShouldExistInTheIconLibrary()
    {
        Assert.All(AzureIcons.AllIconNames, name => Assert.True(IconLibrary.Contains(name), $"Missing icon '{name}'"));
    }
}
