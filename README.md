# Power Platform Solution Exporter

A Windows Forms utility for publishing server-saved Power Platform customizations, incrementing an unmanaged solution revision, exporting the solution as an unmanaged ZIP, and inspecting its included Dataverse tables.

The main window remains named `frmMain`, and all UI text is English. Snapshot restore and destructive table-data operations have been removed.

## Export workflow

Select an unmanaged Solution from the connected environment and choose **Publish & Export Unmanaged ZIP**. After explicit confirmation, the application performs this sequence:

1. Run a read-only Canvas App preflight by comparing each included app's `lastmodifiedtime` with `lastpublishtime`. A known draft or incomplete verification blocks the operation before Publish All or any other mutation.
2. Execute Dataverse `PublishAllXmlRequest`. This publishes all saved customizations in the connected environment, not only the selected Solution.
3. Reload and verify the Canvas App components again to catch a concurrent save or incomplete publish.
4. Increment the final `major.minor.build.revision` segment. For example, `0.1.0.23` becomes `0.1.0.24`.
5. Export through Dataverse `ExportSolutionRequest` with `Managed = false`.
6. Reopen the ZIP, verify its unique name, version, and unmanaged flag, and calculate its SHA-256 hash.

The exported file is named like:

```text
SolutionUniqueName_0_1_0_24_unmanaged_20260806_120000_123.zip
```

Power Platform CLI is not required; connection, publish, version update, and export all use the authenticated Dataverse SDK client.

## Important publishing boundary

No API can save edits that still exist only inside a maker's open browser session. Makers must save their work in Power Apps Studio before starting the export.

The application can publish customizations that have reached Dataverse and can block an export when Canvas App timestamps show a saved unpublished version. It cannot detect keystrokes or edits that have never been saved to the service. Cloud flows and other component types also retain their server-side saved/activation state; the tool does not automatically enable disabled flows.

The Solution version is changed before the export request. If Dataverse rejects or interrupts the subsequent export, the incremented version remains online; skipped version numbers are harmless and are not automatically rolled back.

## Solution and table inspection

The main table grid lists the tables actually included in the selected Solution. It also summarizes Canvas Apps, model-driven apps, workflows, plug-in assemblies, web resources, and security roles.

Double-click a table to open the read-only inspector:

- **Columns and data types** shows display, logical and schema names, Dataverse type, lookup targets, length/range/precision details, required level, primary fields, and read/create/update flags.
- **Record data (read-only)** retrieves records in pages of 10–500 rows. Column headers include display name, logical name, and Dataverse type. File/image/party-list and other unsafe preview values are excluded from the data request but remain visible on the metadata tab.

The inspector never creates, updates, or deletes records.

## Saved connections

Environment URL and user name are editable drop-down lists. Profiles are stored in:

```text
%LocalAppData%\PowerPlatformSolutionExporter\connection-profiles.json
```

Only the environment URL, user-name hint, and last-used timestamp are stored. Passwords and access tokens are not written to the profile. Existing profiles and the MSAL cache are migrated on first use from the legacy `%LocalAppData%\DataverseSolutionExporter` or `%LocalAppData%\DataverseSnapshotManager` directory.

## Requirements

- Windows with the .NET 10 Desktop Runtime, or the .NET 10 SDK for building.
- Dataverse privileges to read Solution/component/table metadata and records.
- Privileges to publish customizations, update the selected Solution version, and export an unmanaged Solution.
- Permission to read Canvas App metadata for publish-state verification.

## Build and test

```powershell
dotnet build PowerPlatformSolutionExporter.slnx
dotnet test PowerPlatformSolutionExporter.slnx
```

## Safety notes

- **Publish All** can make saved customizations visible to users across the connected environment. The confirmation dialog names this scope explicitly.
- The export button never imports a Solution and never modifies table records, but it does publish customizations and update the Solution version.
- Exported unmanaged ZIPs can contain application source, plug-ins, flows, connection references, and environment-variable definitions. Store them as protected source artifacts.

## Microsoft references

- [Export solutions](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/export-solutions)
- [PublishAllXmlRequest](https://learn.microsoft.com/en-us/dotnet/api/microsoft.crm.sdk.messages.publishallxmlrequest?view=dataverse-sdk-latest)
- [Canvas App table and publish timestamps](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/canvasapp)
- [Save and publish a Canvas App](https://learn.microsoft.com/en-us/power-apps/maker/canvas-apps/save-publish-app)
- [Solution versioning](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/update-solutions)
