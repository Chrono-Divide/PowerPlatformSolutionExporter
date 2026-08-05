# Dataverse Snapshot Manager

A general-purpose Windows Forms utility for taking point-in-time snapshots of Dataverse custom table structure and data, restoring custom tables from a snapshot, and clearing the records from one custom table without deleting its structure.

The main form is `frmMain` and all application UI text is English. The application has no built-in environment, account, publisher, table-name, or solution-name catalog.

## Table selection

The default scope is **Custom tables**. All unmanaged custom tables returned by the connected environment are selected initially; use the checkboxes or selection buttons to narrow the snapshot. The optional **All tables** scope is provided for inspection, but snapshot, restore, and clear operations remain restricted to unmanaged custom tables.

## Saved connections

Environment URL and user name are editable drop-down lists. Choose **Save profile** to retain a pair, or connect successfully to save it automatically as the most recently used profile. **Remove** deletes the selected pair from the saved list.

Profiles are stored in `%LocalAppData%\DataverseSnapshotManager\connection-profiles.json`. The profile contains only the environment URL, user-name hint, and last-used time. Passwords and access tokens are not written to this file; interactive sign-in and token caching remain managed by MSAL through the Dataverse client.

## Snapshot contents

A `.dvsnapshot` file is a ZIP-compatible package that contains:

- `manifest.json` with environment, table, record-count, warning, and checksum information.
- `solution.zip` when structure is included. This is an unmanaged Dataverse solution exported through PAC CLI.
- One typed JSON file per selected table, containing the table metadata needed by the tool and, when requested, its records.

Snapshot packages can contain passwords, personal information, and other business data. They are excluded from Git by `.gitignore`; store them in an access-controlled location.

The selected-table JSON and the structure package have different scopes. Data is captured only for the selected tables, but `solution.zip` is the **entire named solution**. Before creating a snapshot, the application verifies that the solution contains every selected table. If the solution contains additional tables, the manifest and operation log record a warning because a later structure restore imports those additional components too.

Creation is read-only against Dataverse. It exports the solution, reads metadata, and retrieves records in pages; it does not create, update, or delete Dataverse rows. The package is written only after table files and SHA-256 checksums have been produced.

## Restore behavior

Before asking for confirmation, the application performs a no-write preflight. It verifies the package format, target environment, manifest/file consistency, SHA-256 checksums, table and record counts, unique IDs, supported value encoding, and that all selected tables occur in `solution.zip`. No solution is imported and no record is written if this preflight fails.

Structure restore imports and publishes the **whole saved unmanaged solution**, not only the tables whose data was captured. This can recreate a missing custom table or column and reapply saved customizations. It can also overwrite customizations for components included in that solution. Solution import is additive and does not automatically delete extra schema components that were created after the snapshot.

Data restore runs in two passes:

1. Create missing records with their original primary GUIDs and align writable scalar values.
2. Align lookups, owners, state, and status after referenced records are available.

Before data writes, the application confirms that each target table is an unmanaged custom table, its primary ID matches, and each restorable source column exists with a compatible type and writable target. A snapshot `null` clears the corresponding writable target value. Read-only system values, unsupported special values, and fields excluded with a warning are not recreated.

By default, records created after the snapshot are preserved. The optional **Delete records not present in the snapshot** setting enables strict record alignment and is intentionally off by default. Deletions follow Dataverse relationship cascade behavior and can affect related tables.

With **Create a safety snapshot before restore** selected, current data for source tables that already exist is captured before the restore begins. Current structure is also exported when the source snapshot includes a structure package. A table that does not yet exist has no current data to preserve.

Restore is not a database transaction. Records are processed sequentially, failures are counted and processing continues, and cancellation or a PAC/import error can leave a partially applied restore. The tool does not provide an automatic rollback; use the safety snapshot for a deliberate follow-up recovery.

Cross-environment restore is blocked. Mutating operations are limited to unmanaged custom tables.

## Clear table data

Select exactly one custom table and choose **Clear Selected Table Data**. The application:

1. Creates an automatic data snapshot of the selected table.
2. Requires the exact confirmation text `CLEAR`.
3. Deletes records in batches while keeping the table structure.

Dataverse relationship cascade rules can delete related records in other tables. Review the confirmation and automatic snapshot path before proceeding.

## Requirements

- Windows with .NET 10 Desktop Runtime/SDK.
- Power Platform CLI (`pac`) installed and authenticated.
- Permission to read metadata/data, export/import the configured solution, and create/update/delete records as required.
- Interactive Microsoft sign-in may appear when the application connects through `Microsoft.PowerPlatform.Dataverse.Client`.

Enter the target environment URL and, optionally, a sign-in account hint. To create a structure snapshot, enter the unique name of an existing unmanaged solution that contains the selected custom tables.

## Build

```powershell
dotnet build dataverse-table-backup-restore.slnx
```

## Known limitations

- File, full-size image, activity party-list, and other special Dataverse values require manual verification; the manifest records warnings when detected.
- Audit history and immutable system timestamps/users are not recreated as ordinary writable fields.
- Solution import restores missing or changed components but does not remove extra schema components.
- The configured solution must contain the selected custom tables for a complete structure snapshot.
- Record restore is intentionally sequential to make errors visible and reduce throttling risk; very large tables can take time.
- The `.dvsnapshot` package is not encrypted. Protect it as a copy of the underlying business data.
- Restore does not recreate audit history or guarantee immutable created/modified timestamps and users.
- There is no cross-table ACID transaction or automatic rollback if a restore is interrupted.
