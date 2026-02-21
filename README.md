# Databricks Workspace Component

A multi-language Pulumi component that creates a compliant, network-isolated Azure Databricks workspace following enterprise best practices.

## What's New in v0.0.4

**v0.0.4** - Added `workspaceName` output for easier cross-stack referencing.

**v0.0.3** - **Data Classification Tags**: New optional `dataClassification` input propagates a `data-classification` tag to all provisioned resources. Supports values like `public`, `internal`, `confidential`, and `restricted` to meet regulatory and compliance requirements out of the box.

```python
workspace = DatabricksWorkspaceComponent("analytics",
    team_name="data-science",
    data_classification="confidential",   # new in v0.0.3
    # ...
)
```

## Features

- **VNet Injection**: Deploys Databricks into your own VNet for network isolation
- **Hub/Spoke Architecture**: Supports peering to a central hub VNet for shared services
- **Compliance Tagging**: Automatically applies mandatory tags (team, environment, cost-center)
- **Security by Default**: Public network access disabled, NSGs configured
- **Premium SKU**: Enables Unity Catalog and advanced security features

## Installation

### From Pulumi Private Registry

```bash
# Python
pulumi package add github.com/pulumi-demos/azure-data/components/databricks-workspace

# TypeScript
pulumi package add github.com/pulumi-demos/azure-data/components/databricks-workspace

# C#
pulumi package add github.com/pulumi-demos/azure-data/components/databricks-workspace
```

## Usage

### Python

```python
from pulumi_databricks_workspace import DatabricksWorkspaceComponent

workspace = DatabricksWorkspaceComponent("analytics",
    team_name="data-science",
    location="westeurope",
    subscription_id=config.require("subscription_id"),
    spoke_cidr="10.1.0.0/16",
    hub_vnet_id=hub_outputs["vnet_id"],  # Optional: for hub/spoke
    environment="dev",
    cost_center="CC-12345",
)

pulumi.export("workspace_url", workspace.workspace_url)
```

### TypeScript

```typescript
import { DatabricksWorkspaceComponent } from "@pulumi-demos/databricks-workspace";

const workspace = new DatabricksWorkspaceComponent("analytics", {
    teamName: "data-science",
    location: "westeurope",
    subscriptionId: config.require("subscriptionId"),
    spokeCidr: "10.1.0.0/16",
    hubVnetId: hubOutputs.vnetId,  // Optional: for hub/spoke
    environment: "dev",
    costCenter: "CC-12345",
});

export const workspaceUrl = workspace.workspaceUrl;
```

### YAML (No-Code)

```yaml
resources:
  workspace:
    type: databricks-workspace:index:DatabricksWorkspaceComponent
    properties:
      teamName: data-science
      location: westeurope
      subscriptionId: ${subscriptionId}
      spokeCidr: "10.1.0.0/16"
      hubVnetId: ${hubVnetId}
      environment: dev
      costCenter: CC-12345

outputs:
  workspaceUrl: ${workspace.workspaceUrl}
```

## Inputs

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `teamName` | string | Yes | Team/project name for naming and tagging |
| `location` | string | Yes | Azure region |
| `subscriptionId` | string | Yes | Target Azure subscription |
| `spokeCidr` | string | Yes | CIDR block for the spoke VNet |
| `hubVnetId` | string | No | Hub VNet ID for peering |
| `skuTier` | string | No | Databricks SKU (default: "premium") |
| `enablePublicAccess` | bool | No | Enable public access (default: false) |
| `environment` | string | No | Environment name (default: "dev") |
| `costCenter` | string | No | Cost center for chargeback |
| `dataClassification` | string | No | Data classification level (e.g., `public`, `internal`, `confidential`, `restricted`) |
| `tags` | map | No | Additional tags |

## Outputs

| Name | Type | Description |
|------|------|-------------|
| `workspaceUrl` | string | Databricks workspace URL |
| `workspaceName` | string | Databricks workspace name |
| `workspaceId` | string | Databricks workspace ID |
| `resourceGroupName` | string | Resource group name |
| `managedResourceGroupName` | string | Managed resource group name |
| `networkConfig` | object | VNet and subnet IDs |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Hub Subscription                          │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ Hub VNet (existing - e.g., from Bicep)              │    │
│  │  - Shared services                                   │    │
│  │  - DNS                                               │    │
│  │  - Firewall (optional)                               │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                         VNet Peering
                              │
┌─────────────────────────────────────────────────────────────┐
│                   Spoke Subscription                         │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ Spoke VNet (created by component)                   │    │
│  │  ┌─────────────────┐  ┌─────────────────┐          │    │
│  │  │ Private Subnet  │  │ Public Subnet   │          │    │
│  │  │ (workers)       │  │ (NAT)           │          │    │
│  │  │ + NSG           │  │ + NSG           │          │    │
│  │  └─────────────────┘  └─────────────────┘          │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                              │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ Databricks Workspace                                │    │
│  │  - Premium SKU                                      │    │
│  │  - VNet injected                                    │    │
│  │  - No public IP                                     │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

## Building the Component

```bash
cd components/databricks-workspace
dotnet build
```

## Publishing to Private Registry

```bash
# Build and publish (requires DOTNET_ROOT set for Homebrew .NET installs)
pulumi package publish components/databricks-workspace/bin/Debug/net10.0/pulumi-resource-databricks-workspace \
  --readme components/databricks-workspace/README.md \
  --publisher demo
```

## Cost Estimate

| Resource | Cost | Notes |
|----------|------|-------|
| Resource Group | Free | Container only |
| VNet + Subnets | Free | No data transfer |
| NSGs | Free | Rules only |
| Databricks Workspace | ~$0.07/DBU | Pay-per-use |
| Databricks Cluster | ~$0.40-2.00/hr | On-demand |

**Tip**: Destroy after demo to avoid ongoing costs.
