using System.Collections.Generic;
using Pulumi;
using Pulumi.AzureNative.Network;
using NetworkInputs = Pulumi.AzureNative.Network.Inputs;
using AzureResources = Pulumi.AzureNative.Resources;
using AzureDatabricks = Pulumi.AzureNative.Databricks;

namespace DatabricksWorkspace;

/// <summary>
/// Input arguments for the DatabricksWorkspaceComponent.
/// This component creates a compliant, network-isolated Databricks workspace
/// following enterprise best practices for the Data & Analytics platform.
/// </summary>
public sealed class DatabricksWorkspaceComponentArgs : Pulumi.ResourceArgs
{
    /// <summary>
    /// The name of the team or project that owns this workspace.
    /// Used for naming resources and tagging.
    /// </summary>
    [Input("teamName", required: true)]
    public Input<string> TeamName { get; set; } = null!;

    /// <summary>
    /// The Azure region where resources will be deployed.
    /// </summary>
    [Input("location", required: true)]
    public Input<string> Location { get; set; } = null!;

    /// <summary>
    /// The Azure subscription ID where resources will be deployed.
    /// Enables hub/spoke multi-subscription architecture.
    /// </summary>
    [Input("subscriptionId", required: true)]
    public Input<string> SubscriptionId { get; set; } = null!;

    /// <summary>
    /// The resource ID of the hub VNet for peering.
    /// This enables connectivity to shared services (DNS, firewall, etc.)
    /// </summary>
    [Input("hubVnetId")]
    public Input<string>? HubVnetId { get; set; }

    /// <summary>
    /// The CIDR block for the spoke VNet (e.g., "10.1.0.0/16").
    /// Must not overlap with hub or other spoke networks.
    /// </summary>
    [Input("spokeCidr", required: true)]
    public Input<string> SpokeCidr { get; set; } = null!;

    /// <summary>
    /// The Databricks SKU tier. Defaults to "premium" for Unity Catalog support.
    /// Options: "standard", "premium", "trial"
    /// </summary>
    [Input("skuTier")]
    public Input<string>? SkuTier { get; set; }

    /// <summary>
    /// Enable public network access. Defaults to false for security.
    /// Set to true only for development/testing scenarios.
    /// </summary>
    [Input("enablePublicAccess")]
    public Input<bool>? EnablePublicAccess { get; set; }

    /// <summary>
    /// Additional tags to apply to all resources.
    /// Standard compliance tags (environment, team, cost-center) are added automatically.
    /// </summary>
    [Input("tags")]
    public InputMap<string>? Tags { get; set; }

    /// <summary>
    /// The environment name (dev, staging, prod).
    /// Used for compliance tagging and naming conventions.
    /// </summary>
    [Input("environment")]
    public Input<string>? Environment { get; set; }

    /// <summary>
    /// Cost center code for chargeback.
    /// Required for compliance in enterprise deployments.
    /// </summary>
    [Input("costCenter")]
    public Input<string>? CostCenter { get; set; }
}

/// <summary>
/// Output type for network configuration details.
/// </summary>
[OutputType]
public sealed class NetworkConfig
{
    [Output("vnetId")]
    public string VnetId { get; }

    [Output("privateSubnetId")]
    public string PrivateSubnetId { get; }

    [Output("publicSubnetId")]
    public string PublicSubnetId { get; }

    [OutputConstructor]
    public NetworkConfig(string vnetId, string privateSubnetId, string publicSubnetId)
    {
        VnetId = vnetId;
        PrivateSubnetId = privateSubnetId;
        PublicSubnetId = publicSubnetId;
    }
}

/// <summary>
/// A compliant, network-isolated Databricks workspace component.
/// 
/// This component encapsulates enterprise best practices for Databricks deployments:
/// - VNet injection for network isolation
/// - Hub/spoke peering for shared services connectivity
/// - NSG rules for secure communication
/// - Compliance tagging for governance
/// - Premium SKU for Unity Catalog support
/// 
/// Example usage from Python:
/// <code>
/// workspace = DatabricksWorkspaceComponent("analytics",
///     team_name="data-science",
///     location="westeurope",
///     subscription_id=config.require("subscription_id"),
///     spoke_cidr="10.1.0.0/16",
///     hub_vnet_id=hub_outputs["vnet_id"],
///     environment="dev",
///     cost_center="CC-12345",
/// )
/// </code>
/// </summary>
public class DatabricksWorkspaceComponent : ComponentResource
{
    /// <summary>
    /// The URL of the Databricks workspace.
    /// </summary>
    [Output("workspaceUrl")]
    public Output<string> WorkspaceUrl { get; private set; } = null!;

    /// <summary>
    /// The unique ID of the Databricks workspace.
    /// </summary>
    [Output("workspaceId")]
    public Output<string> WorkspaceId { get; private set; } = null!;

    /// <summary>
    /// The name of the resource group containing all workspace resources.
    /// </summary>
    [Output("resourceGroupName")]
    public Output<string> ResourceGroupName { get; private set; } = null!;

    /// <summary>
    /// The name of the managed resource group created by Databricks.
    /// </summary>
    [Output("managedResourceGroupName")]
    public Output<string> ManagedResourceGroupName { get; private set; } = null!;

    /// <summary>
    /// Network configuration details for the workspace.
    /// </summary>
    [Output("networkConfig")]
    public Output<NetworkConfig> NetworkConfiguration { get; private set; } = null!;

    public DatabricksWorkspaceComponent(
        string name,
        DatabricksWorkspaceComponentArgs args,
        ComponentResourceOptions? opts = null)
        : base("databricks-workspace:index:DatabricksWorkspaceComponent", name, args, opts)
    {
        var teamName = args.TeamName;
        var location = args.Location;
        var subscriptionId = args.SubscriptionId;
        var spokeCidr = args.SpokeCidr;
        var skuTier = args.SkuTier ?? "premium";
        var enablePublicAccess = args.EnablePublicAccess ?? false;
        var environment = args.Environment ?? "dev";
        var costCenter = args.CostCenter ?? "unassigned";

        // Build compliance tags - these are mandatory for all resources
        // User-provided tags are included, but compliance tags always take precedence
        var baseTags = args.Tags ?? new InputMap<string>();
        baseTags["team"] = teamName;
        baseTags["environment"] = environment;
        baseTags["cost-center"] = costCenter;
        baseTags["managed-by"] = "pulumi";
        baseTags["component"] = "databricks-workspace";

        // Create the resource group for the workspace
        var resourceGroup = new AzureResources.ResourceGroup($"{name}-rg", new AzureResources.ResourceGroupArgs
        {
            ResourceGroupName = Output.Format($"rg-dbw-{teamName}-{environment}"),
            Location = location,
            Tags = baseTags,
        }, new CustomResourceOptions { Parent = this });

        // Create the spoke VNet with Databricks-required subnets
        var vnet = new VirtualNetwork($"{name}-vnet", new VirtualNetworkArgs
        {
            VirtualNetworkName = Output.Format($"vnet-dbw-{teamName}-{environment}"),
            ResourceGroupName = resourceGroup.Name,
            Location = location,
            AddressSpace = new NetworkInputs.AddressSpaceArgs
            {
                AddressPrefixes = new[] { spokeCidr },
            },
            Tags = baseTags,
        }, new CustomResourceOptions { Parent = this });

        // Calculate subnet CIDRs from the spoke CIDR
        // Private subnet: first /24 block
        // Public subnet: second /24 block
        var privateSubnetCidr = spokeCidr.Apply(cidr =>
        {
            var parts = cidr.Split('/');
            var octets = parts[0].Split('.');
            return $"{octets[0]}.{octets[1]}.0.0/24";
        });

        var publicSubnetCidr = spokeCidr.Apply(cidr =>
        {
            var parts = cidr.Split('/');
            var octets = parts[0].Split('.');
            return $"{octets[0]}.{octets[1]}.1.0/24";
        });

        // Create NSG for Databricks private subnet
        var privateNsg = new NetworkSecurityGroup($"{name}-private-nsg", new NetworkSecurityGroupArgs
        {
            NetworkSecurityGroupName = Output.Format($"nsg-dbw-private-{teamName}-{environment}"),
            ResourceGroupName = resourceGroup.Name,
            Location = location,
            Tags = baseTags,
        }, new CustomResourceOptions { Parent = this });

        // Create NSG for Databricks public subnet
        var publicNsg = new NetworkSecurityGroup($"{name}-public-nsg", new NetworkSecurityGroupArgs
        {
            NetworkSecurityGroupName = Output.Format($"nsg-dbw-public-{teamName}-{environment}"),
            ResourceGroupName = resourceGroup.Name,
            Location = location,
            Tags = baseTags,
        }, new CustomResourceOptions { Parent = this });

        // Create private subnet for Databricks (worker nodes)
        var privateSubnet = new Subnet($"{name}-private-subnet", new SubnetArgs
        {
            SubnetName = "databricks-private",
            ResourceGroupName = resourceGroup.Name,
            VirtualNetworkName = vnet.Name,
            AddressPrefix = privateSubnetCidr,
            NetworkSecurityGroup = new NetworkInputs.NetworkSecurityGroupArgs { Id = privateNsg.Id },
            Delegations = new[]
            {
                new NetworkInputs.DelegationArgs
                {
                    Name = "databricks-delegation",
                    ServiceName = "Microsoft.Databricks/workspaces",
                },
            },
        }, new CustomResourceOptions { Parent = this, DependsOn = { vnet, privateNsg } });

        // Create public subnet for Databricks (NAT gateway connectivity)
        var publicSubnet = new Subnet($"{name}-public-subnet", new SubnetArgs
        {
            SubnetName = "databricks-public",
            ResourceGroupName = resourceGroup.Name,
            VirtualNetworkName = vnet.Name,
            AddressPrefix = publicSubnetCidr,
            NetworkSecurityGroup = new NetworkInputs.NetworkSecurityGroupArgs { Id = publicNsg.Id },
            Delegations = new[]
            {
                new NetworkInputs.DelegationArgs
                {
                    Name = "databricks-delegation",
                    ServiceName = "Microsoft.Databricks/workspaces",
                },
            },
        }, new CustomResourceOptions { Parent = this, DependsOn = { vnet, publicNsg, privateSubnet } });

        // Create VNet peering to hub if hub VNet ID is provided
        if (args.HubVnetId != null)
        {
            var spokeToHubPeering = new VirtualNetworkPeering($"{name}-spoke-to-hub", new VirtualNetworkPeeringArgs
            {
                VirtualNetworkPeeringName = "spoke-to-hub",
                ResourceGroupName = resourceGroup.Name,
                VirtualNetworkName = vnet.Name,
                RemoteVirtualNetwork = new NetworkInputs.SubResourceArgs
                {
                    Id = args.HubVnetId,
                },
                AllowVirtualNetworkAccess = true,
                AllowForwardedTraffic = true,
                AllowGatewayTransit = false,
                UseRemoteGateways = false,
            }, new CustomResourceOptions { Parent = this, DependsOn = { vnet } });
        }

        // Build the managed resource group ID
        var managedRgName = Output.Format($"rg-dbw-managed-{teamName}-{environment}");
        var managedRgId = Output.Tuple<string, string>((Input<string>)subscriptionId, (Input<string>)managedRgName).Apply(t =>
            $"/subscriptions/{t.Item1}/resourceGroups/{t.Item2}");

        // Create the Databricks workspace with VNet injection
        var workspace = new AzureDatabricks.Workspace($"{name}-workspace", new AzureDatabricks.WorkspaceArgs
        {
            WorkspaceName = Output.Format($"dbw-{teamName}-{environment}"),
            ResourceGroupName = resourceGroup.Name,
            Location = location,
            ManagedResourceGroupId = managedRgId,
            Sku = new AzureDatabricks.Inputs.SkuArgs
            {
                Name = skuTier,
            },
            PublicNetworkAccess = enablePublicAccess.Apply(v => v
                ? AzureDatabricks.PublicNetworkAccess.Enabled
                : AzureDatabricks.PublicNetworkAccess.Disabled),
            RequiredNsgRules = enablePublicAccess.Apply(v => v
                ? AzureDatabricks.RequiredNsgRules.AllRules
                : AzureDatabricks.RequiredNsgRules.NoAzureDatabricksRules),
            Parameters = new AzureDatabricks.Inputs.WorkspaceCustomParametersArgs
            {
                CustomVirtualNetworkId = new AzureDatabricks.Inputs.WorkspaceCustomStringParameterArgs
                {
                    Value = vnet.Id,
                },
                CustomPrivateSubnetName = new AzureDatabricks.Inputs.WorkspaceCustomStringParameterArgs
                {
                    Value = "databricks-private",
                },
                CustomPublicSubnetName = new AzureDatabricks.Inputs.WorkspaceCustomStringParameterArgs
                {
                    Value = "databricks-public",
                },
                EnableNoPublicIp = new AzureDatabricks.Inputs.WorkspaceCustomBooleanParameterArgs
                {
                    Value = enablePublicAccess.Apply(v => !v),
                },
            },
            Tags = baseTags,
        }, new CustomResourceOptions { Parent = this, DependsOn = { privateSubnet, publicSubnet } });

        // Set outputs
        WorkspaceUrl = workspace.WorkspaceUrl;
        WorkspaceId = workspace.WorkspaceId;
        ResourceGroupName = resourceGroup.Name;
        ManagedResourceGroupName = managedRgName;
        NetworkConfiguration = Output.Tuple(vnet.Id, privateSubnet.Id, publicSubnet.Id).Apply(t =>
            new NetworkConfig(t.Item1, t.Item2, t.Item3));

        // Register outputs for stack exports
        this.RegisterOutputs(new Dictionary<string, object?>
        {
            { "workspaceUrl", WorkspaceUrl },
            { "workspaceId", WorkspaceId },
            { "resourceGroupName", ResourceGroupName },
            { "managedResourceGroupName", ManagedResourceGroupName },
            { "networkConfig", NetworkConfiguration },
        });
    }
}
