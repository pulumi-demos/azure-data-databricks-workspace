# Publishing the Databricks Workspace Component

This document describes how to build and publish the component to the Pulumi Private Registry.

## Prerequisites

1. .NET 8.0 SDK installed
2. Pulumi CLI installed
3. Authenticated to Pulumi Cloud (`pulumi login`)

## Local Build

```bash
cd components/databricks-workspace

# Restore dependencies
dotnet restore

# Build the component
dotnet build --configuration Release
```

## Publish to Private Registry

### Option 1: Manual Publishing

```bash
# From the component directory
cd components/databricks-workspace

# Publish with README
pulumi package publish . --readme ./README.md
```

### Option 2: GitHub Actions (Recommended)

The component includes a GitHub Actions workflow that automatically publishes when:
- A version tag is pushed (e.g., `v1.0.0`)
- Manually triggered via workflow dispatch

To publish a new version:

```bash
# Tag and push
git tag v1.0.0
git push origin v1.0.0
```

## Consuming the Published Component

Once published, teams can consume the component in any language:

### Python

```bash
pulumi package add github.com/pulumi-demos/azure-data/components/databricks-workspace
```

Then in code:

```python
from pulumi_databricks_workspace import DatabricksWorkspaceComponent

workspace = DatabricksWorkspaceComponent("my-workspace",
    team_name="data-science",
    location="westeurope",
    subscription_id="...",
    spoke_cidr="10.1.0.0/16",
)
```

### TypeScript

```bash
pulumi package add github.com/pulumi-demos/azure-data/components/databricks-workspace
```

Then in code:

```typescript
import { DatabricksWorkspaceComponent } from "@pulumi-demos/databricks-workspace";

const workspace = new DatabricksWorkspaceComponent("my-workspace", {
    teamName: "data-science",
    location: "westeurope",
    subscriptionId: "...",
    spokeCidr: "10.1.0.0/16",
});
```

### YAML (No-Code)

Add to `Pulumi.yaml`:

```yaml
packages:
  databricks-workspace: github.com/pulumi-demos/azure-data/components/databricks-workspace
```

Then use in resources:

```yaml
resources:
  workspace:
    type: databricks-workspace:index:DatabricksWorkspaceComponent
    properties:
      teamName: data-science
      location: westeurope
      subscriptionId: ${subscriptionId}
      spokeCidr: "10.1.0.0/16"
```

## Version Management

- Use semantic versioning (MAJOR.MINOR.PATCH)
- Breaking changes require major version bump
- New features require minor version bump
- Bug fixes require patch version bump

## Troubleshooting

### Build Errors

If you encounter build errors, ensure:
1. .NET 8.0 SDK is installed
2. All NuGet packages are restored
3. Pulumi.AzureNative package is compatible

### Publishing Errors

If publishing fails:
1. Verify you're authenticated (`pulumi whoami`)
2. Check you have permission to publish to the organization
3. Ensure the README.md file exists
