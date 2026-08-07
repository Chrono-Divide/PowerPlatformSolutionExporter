# Power Platform Solution Exporter

A Windows Forms utility for publishing server-saved Power Platform customizations, incrementing an unmanaged solution revision, exporting the solution as an unmanaged ZIP, inspecting its included Dataverse tables, and exporting their data as browser-importable CSV files.

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

The application version is defined by the `Version` property in `PowerPlatformSolutionExporter.csproj` and appears in the `frmMain` window title. Increment the patch component by one for every released modification (`0.0.1`, `0.0.2`, `0.0.3`, and so on).

## Important publishing boundary

No API can save edits that still exist only inside a maker's open browser session. Makers must save their work in Power Apps Studio before starting the export.

The application can publish customizations that have reached Dataverse and can block an export when Canvas App timestamps show a saved unpublished version. It cannot detect keystrokes or edits that have never been saved to the service. Cloud flows and other component types also retain their server-side saved/activation state; the tool does not automatically enable disabled flows.

The Solution version is changed before the export request. If Dataverse rejects or interrupts the subsequent export, the incremented version remains online; skipped version numbers are harmless and are not automatically rolled back.

## Solution and table inspection

The main table grid lists the tables actually included in the selected Solution. It also summarizes Canvas Apps, model-driven apps, workflows, plug-in assemblies, web resources, and security roles.

Double-click a table to open the read-only inspector:

- **Columns and data types** shows display, logical and schema names, Dataverse type, lookup targets, length/range/precision details, required level, primary fields, and read/create/update flags.
- **Record data (read-only)** retrieves records in pages of 10–500 rows. Column headers include display name, logical name, and Dataverse type. File/image/party-list and other unsafe preview values are excluded from the data request but remain visible on the metadata tab.

For the columns each tab displays, both keep the primary record GUID first, then list custom columns, Dataverse-generated columns, and Dataverse system columns. Generated columns (the primary GUID and internal columns whose `AttributeOf` metadata extends another column) use a light yellow background; system columns use light blue-gray; custom columns remain white. Long names are measured for a wider initial column, wrap in record headers, and remain available in full through tooltips.

Click a header in **Columns and data types** to sort the column definitions ascending or descending while the primary GUID remains pinned first. Click a header in **Record data (read-only)** to sort through Dataverse across the complete table, reset paging to page 1, and use the primary GUID as a stable secondary order.

Record cells remain selectable and copyable, and clipboard output contains only the selected cell values without column headers. The inspector never creates, updates, or deletes records.

## Browser-importable table data export

Choose **Export Table Data** to create a timestamped folder for every table listed in the selected Solution. This operation is read-only: it does not publish customizations, change the Solution version, or modify Dataverse records. The folder contains:

- One UTF-8 BOM CSV file per table, including its Primary ID GUID and all readable/importable columns.
- `manifest.json`, which maps every CSV header to its display name, logical name, data type, lookup targets, required level, and skipped-column reason.
- `import-guide.txt`, which gives the browser import steps and a lookup-aware recommended table order.

CSV headings use Dataverse display names for browser auto-mapping. If display names are duplicated within one table, the logical name is appended to make the headings unambiguous. Choice, Boolean, status, state, and lookup values use their Dataverse display values; numeric, money, GUID, and date/time values use stable invariant representations.

Power Apps browser import doesn't support File, Image, multi-select Choices, Timezone, and specific system fields. The exporter also omits generated, calculated, unreadable, and otherwise read-only columns, and records every omission in the manifest. CSV import performs inserts/updates only: it doesn't delete target rows that aren't present in the CSV. If a table or column was deleted, import the unmanaged Solution ZIP before importing its CSV data.

The exporter writes to a temporary folder and only exposes the final timestamped folder after every table, the manifest, and the guide finish successfully. Cancellation or failure removes the incomplete temporary export.

The export contains only values readable by the signed-in account. CSV and manifest files are unencrypted and can contain sensitive business data, so the complete output folder must be stored securely.

## Saved connections

Environment URL and user name are editable drop-down lists. Profiles are stored in:

```text
%LocalAppData%\PowerPlatformSolutionExporter\connection-profiles.json
```

Only the environment URL, user-name hint, and last-used timestamp are stored. Passwords and access tokens are not written to the profile. Existing profiles and the MSAL cache are migrated on first use from the legacy `%LocalAppData%\DataverseSolutionExporter` or `%LocalAppData%\DataverseSnapshotManager` directory.

The first launch uses `%UserProfile%\Documents\PowerPlatformSolutionExports` as the export folder. After it is changed, the last non-empty folder is restored from the separate settings file in the same local directory:

```text
%LocalAppData%\PowerPlatformSolutionExporter\app-settings.json
```

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
- **Export Table Data** is read-only, but browser CSV import is not an exact database restore: it doesn't delete extra rows and requires careful Primary ID, Choice, and lookup mapping.
- Exported unmanaged ZIPs can contain application source, plug-ins, flows, connection references, and environment-variable definitions. Store them as protected source artifacts.

## Microsoft references

- [Export solutions](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/export-solutions)
- [PublishAllXmlRequest](https://learn.microsoft.com/en-us/dotnet/api/microsoft.crm.sdk.messages.publishallxmlrequest?view=dataverse-sdk-latest)
- [Canvas App table and publish timestamps](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/reference/entities/canvasapp)
- [Save and publish a Canvas App](https://learn.microsoft.com/en-us/power-apps/maker/canvas-apps/save-publish-app)
- [Solution versioning](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/update-solutions)
- [Dataverse AttributeMetadata](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/webapi/reference/attributemetadata?view=dataverse-latest)
- [AttributeMetadata.AttributeOf](https://learn.microsoft.com/en-us/dotnet/api/microsoft.xrm.sdk.metadata.attributemetadata.attributeof?view=dataverse-sdk-latest)
- [Import data from Excel and export data to CSV](https://learn.microsoft.com/en-us/power-apps/maker/data-platform/data-platform-import-export)
