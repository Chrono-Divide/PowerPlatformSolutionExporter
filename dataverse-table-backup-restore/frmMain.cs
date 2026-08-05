using System.ComponentModel;
using dataverse_table_backup_restore.Models;
using dataverse_table_backup_restore.Services;

namespace dataverse_table_backup_restore;

public partial class frmMain : Form
{
    private readonly DataverseService _dataverseService = new();
    private readonly PacCliService _pacCliService = new();
    private readonly ConnectionProfileStore _connectionProfileStore = new();
    private readonly SnapshotPackageService _snapshotPackageService;
    private readonly BindingList<TableGridRow> _tableRows = [];
    private IReadOnlyList<ConnectionProfile> _connectionProfiles = [];
    private CancellationTokenSource? _operationCancellation;
    private bool _busy;
    private bool _loadingConnectionProfiles;

    public frmMain()
    {
        InitializeComponent();
        _snapshotPackageService = new SnapshotPackageService(_dataverseService, _pacCliService);
        dgvTables.AutoGenerateColumns = false;
        dgvTables.DataSource = _tableRows;
        cboTableScope.SelectedIndex = 0;
        cboSnapshotContent.SelectedIndex = 1;
        txtSnapshotFolder.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DataverseSnapshots");
        chkRestoreStructure.Checked = true;
        chkRestoreData.Checked = true;
        chkSafetySnapshot.Checked = true;
        chkDeleteExtraRecords.Checked = false;
        LoadConnectionProfiles();
        UpdateActionState();
    }

    private async void btnConnect_Click(object sender, EventArgs e)
    {
        await RunOperationAsync("Connect", async cancellationToken =>
        {
            SetConnectionStatus("Connecting...", Color.DarkOrange);
            var userName = cboUserName.Text.Trim();
            await _dataverseService.ConnectAsync(cboEnvironmentUrl.Text.Trim(), userName, cancellationToken);
            cboEnvironmentUrl.Text = _dataverseService.ConnectedEnvironmentUrl;
            SetConnectionStatus($"Connected: {_dataverseService.ConnectedEnvironmentName}", Color.SeaGreen);
            TrySaveConnectionProfile(cboEnvironmentUrl.Text, userName);
            await LoadTablesAsync(forceRefresh: true, cancellationToken);
        });
        if (!_dataverseService.IsConnected)
        {
            SetConnectionStatus("Connection failed", Color.Firebrick);
        }
    }

    private void btnSaveConnectionProfile_Click(object sender, EventArgs e)
    {
        try
        {
            var profile = _connectionProfileStore.Save(cboEnvironmentUrl.Text, cboUserName.Text);
            LoadConnectionProfiles(profile.EnvironmentUrl, profile.UserName);
            MessageBox.Show(
                this,
                "Connection profile saved. Only the environment URL and user name are stored; no password or access token is written to the profile.",
                "Profile Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            ShowError("Unable to save connection profile", exception);
        }
    }

    private void btnRemoveConnectionProfile_Click(object sender, EventArgs e)
    {
        ConnectionProfile? profile;
        try
        {
            var normalizedUrl = ConnectionProfileStore.NormalizeEnvironmentUrl(cboEnvironmentUrl.Text);
            profile = _connectionProfiles.FirstOrDefault(item =>
                string.Equals(item.EnvironmentUrl, normalizedUrl, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.UserName, cboUserName.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception)
        {
            ShowError("Unable to remove connection profile", exception);
            return;
        }

        if (profile is null)
        {
            MessageBox.Show(this, "The current URL and user name are not a saved profile.", "Profile Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"Remove this saved connection profile?\n\n{profile.EnvironmentUrl}\n{profile.UserName}",
            "Remove Profile",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        try
        {
            _connectionProfileStore.Remove(profile.EnvironmentUrl, profile.UserName);
            LoadConnectionProfiles();
        }
        catch (Exception exception)
        {
            ShowError("Unable to remove connection profile", exception);
        }
    }

    private void cboEnvironmentUrl_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (_loadingConnectionProfiles)
        {
            return;
        }

        var preferredUserName = _connectionProfiles
            .FirstOrDefault(profile => string.Equals(
                profile.EnvironmentUrl,
                cboEnvironmentUrl.Text,
                StringComparison.OrdinalIgnoreCase))
            ?.UserName;
        PopulateUserNames(cboEnvironmentUrl.Text, preferredUserName);
    }

    private void LoadConnectionProfiles(string? preferredEnvironmentUrl = null, string? preferredUserName = null)
    {
        _loadingConnectionProfiles = true;
        try
        {
            _connectionProfiles = _connectionProfileStore.Load();
            var preferredProfile = _connectionProfiles.FirstOrDefault(profile =>
                                       string.Equals(profile.EnvironmentUrl, preferredEnvironmentUrl, StringComparison.OrdinalIgnoreCase) &&
                                       string.Equals(profile.UserName, preferredUserName, StringComparison.OrdinalIgnoreCase))
                                   ?? _connectionProfiles.FirstOrDefault();

            cboEnvironmentUrl.BeginUpdate();
            cboEnvironmentUrl.Items.Clear();
            cboEnvironmentUrl.Items.AddRange(_connectionProfiles
                .Select(profile => profile.EnvironmentUrl)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<object>()
                .ToArray());
            cboEnvironmentUrl.EndUpdate();
            cboEnvironmentUrl.Text = preferredProfile?.EnvironmentUrl ?? preferredEnvironmentUrl ?? string.Empty;
            PopulateUserNames(cboEnvironmentUrl.Text, preferredProfile?.UserName ?? preferredUserName);
        }
        finally
        {
            _loadingConnectionProfiles = false;
            btnRemoveConnectionProfile.Enabled = !_busy && _connectionProfiles.Count > 0;
        }
    }

    private void PopulateUserNames(string environmentUrl, string? preferredUserName = null)
    {
        var existingText = preferredUserName ?? cboUserName.Text;
        var userNames = _connectionProfiles
            .Where(profile => string.Equals(profile.EnvironmentUrl, environmentUrl, StringComparison.OrdinalIgnoreCase))
            .Select(profile => profile.UserName)
            .Where(userName => !string.IsNullOrWhiteSpace(userName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .ToArray();

        cboUserName.BeginUpdate();
        cboUserName.Items.Clear();
        cboUserName.Items.AddRange(userNames);
        cboUserName.EndUpdate();
        cboUserName.Text = existingText;
    }

    private void TrySaveConnectionProfile(string environmentUrl, string userName)
    {
        try
        {
            var profile = _connectionProfileStore.Save(environmentUrl, userName);
            LoadConnectionProfiles(profile.EnvironmentUrl, profile.UserName);
        }
        catch (Exception exception)
        {
            AppendLog($"[{DateTime.Now:HH:mm:ss}] WARNING: Connected, but the profile could not be saved: {exception.Message}");
        }
    }

    private async void btnRefreshTables_Click(object sender, EventArgs e)
    {
        await RunOperationAsync("Refresh tables", cancellationToken => LoadTablesAsync(forceRefresh: true, cancellationToken));
    }

    private async void cboTableScope_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (!_dataverseService.IsConnected || _busy)
        {
            return;
        }

        await RunOperationAsync("Change table scope", cancellationToken => LoadTablesAsync(forceRefresh: false, cancellationToken));
    }

    private void btnSelectAll_Click(object sender, EventArgs e)
    {
        foreach (var row in _tableRows)
        {
            row.Selected = row.Table.IsCustom;
        }

        dgvTables.Refresh();
        UpdateSelectionSummary();
    }

    private void btnClearSelection_Click(object sender, EventArgs e)
    {
        foreach (var row in _tableRows)
        {
            row.Selected = false;
        }

        dgvTables.Refresh();
        UpdateSelectionSummary();
    }

    private void dgvTables_CurrentCellDirtyStateChanged(object sender, EventArgs e)
    {
        if (dgvTables.IsCurrentCellDirty)
        {
            dgvTables.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void dgvTables_CellValueChanged(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex >= 0 && e.ColumnIndex == colSelected.Index)
        {
            UpdateSelectionSummary();
        }
    }

    private void btnBrowseSnapshotFolder_Click(object sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose where snapshot packages will be stored.",
            SelectedPath = txtSnapshotFolder.Text,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtSnapshotFolder.Text = dialog.SelectedPath;
        }
    }

    private void btnBrowseSnapshotFile_Click(object sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select a Dataverse snapshot",
            Filter = "Dataverse snapshot (*.dvsnapshot)|*.dvsnapshot|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtSnapshotFile.Text = dialog.FileName;
        }
    }

    private async void btnCreateSnapshot_Click(object sender, EventArgs e)
    {
        var tables = GetSelectedTables();
        if (!ValidateCustomSelection(tables))
        {
            return;
        }

        var includeData = cboSnapshotContent.SelectedIndex == 1;
        await RunOperationAsync("Create snapshot", async cancellationToken =>
        {
            var output = await _snapshotPackageService.CreateSnapshotAsync(
                CreateSnapshotOptions(tables, includeStructure: true, includeData),
                CreateProgress(),
                cancellationToken);
            txtSnapshotFile.Text = output;
            MessageBox.Show(this, $"Snapshot created successfully.\n\n{output}", "Snapshot Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private async void btnRestore_Click(object sender, EventArgs e)
    {
        var snapshotFile = txtSnapshotFile.Text.Trim();
        if (!File.Exists(snapshotFile))
        {
            MessageBox.Show(this, "Select an existing .dvsnapshot file first.", "Snapshot Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SnapshotManifest manifest;
        try
        {
            manifest = await _snapshotPackageService.ReadManifestAsync(snapshotFile, CancellationToken.None);
        }
        catch (Exception exception)
        {
            ShowError("Unable to read the snapshot", exception);
            return;
        }

        var restoreStructure = chkRestoreStructure.Checked;
        var restoreData = chkRestoreData.Checked;
        if (!restoreStructure && !restoreData)
        {
            MessageBox.Show(this, "Select structure restore, data restore, or both.", "Restore Options", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var strictWarning = chkDeleteExtraRecords.Checked
            ? "\n\nWARNING: Records not present in the snapshot will be deleted."
            : string.Empty;
        var confirmation = MessageBox.Show(
            this,
            $"Restore snapshot from {manifest.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}?\n" +
            $"Tables: {manifest.Tables.Count:N0}\n" +
            $"Target: {cboEnvironmentUrl.Text.Trim()}" + strictWarning,
            "Confirm Restore",
            MessageBoxButtons.YesNo,
            chkDeleteExtraRecords.Checked ? MessageBoxIcon.Warning : MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        await RunOperationAsync("Restore snapshot", async cancellationToken =>
        {
            var progress = CreateProgress();
            if (chkSafetySnapshot.Checked)
            {
                progress.Report("Creating a safety snapshot of the current target state before restore...");
                var allTables = await _dataverseService.LoadTablesAsync(TableScope.All, progress, cancellationToken);
                var manifestNames = manifest.Tables.Select(table => table.LogicalName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var safetyTables = allTables.Where(table => table.IsCustom && manifestNames.Contains(table.LogicalName)).ToList();
                if (safetyTables.Count > 0)
                {
                    await _snapshotPackageService.CreateSnapshotAsync(
                        CreateSnapshotOptions(
                            safetyTables,
                            includeStructure: true,
                            includeData: true,
                            content: "Safety snapshot before restore"),
                        progress,
                        cancellationToken);
                }
                else
                {
                    progress.Report("No existing custom tables from this snapshot were found for the safety data snapshot.");
                }
            }

            var (_, summary) = await _snapshotPackageService.RestoreSnapshotAsync(
                new RestoreOptions
                {
                    EnvironmentUrl = cboEnvironmentUrl.Text.Trim(),
                    SnapshotFile = snapshotFile,
                    RestoreStructure = restoreStructure,
                    RestoreData = restoreData,
                    DeleteRecordsNotInSnapshot = chkDeleteExtraRecords.Checked,
                },
                progress,
                cancellationToken);
            await LoadTablesAsync(forceRefresh: true, cancellationToken);
            MessageBox.Show(
                this,
                $"Restore completed.\n\nCreated: {summary.Created:N0}\nUpdate operations: {summary.Updated:N0}\nDeleted: {summary.Deleted:N0}\nSkipped: {summary.Skipped:N0}\nFailed: {summary.Failed:N0}",
                "Restore Complete",
                MessageBoxButtons.OK,
                summary.Failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        });
    }

    private async void btnClearTableData_Click(object sender, EventArgs e)
    {
        var tables = GetSelectedTables();
        if (tables.Count != 1)
        {
            MessageBox.Show(this, "Select exactly one custom table to clear.", "One Table Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var table = tables[0];
        if (!table.IsCustom)
        {
            MessageBox.Show(this, "Clear data is limited to unmanaged custom tables.", "Operation Blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!ConfirmExactText(
                "Clear Table Data",
                $"This deletes every record from:\n\n{table.DisplayName}\n{table.LogicalName}\n\nThe table structure is preserved. Related cascading deletes may also occur. A data snapshot will be created first.\n\nType CLEAR to continue:",
                "CLEAR"))
        {
            return;
        }

        await RunOperationAsync("Clear table data", async cancellationToken =>
        {
            var progress = CreateProgress();
            progress.Report("Creating an automatic data snapshot before deletion...");
            var safetySnapshot = await _snapshotPackageService.CreateSnapshotAsync(
                CreateSnapshotOptions(
                    [table],
                    includeStructure: false,
                    includeData: true,
                    content: $"Automatic safety snapshot before clearing {table.LogicalName}"),
                progress,
                cancellationToken);
            var deleted = await _dataverseService.ClearTableDataAsync(table, progress, cancellationToken);
            MessageBox.Show(
                this,
                $"Table data cleared.\n\nDeleted: {deleted:N0}\nSafety snapshot:\n{safetySnapshot}",
                "Clear Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });
    }

    private void btnCancelOperation_Click(object sender, EventArgs e)
    {
        _operationCancellation?.Cancel();
        AppendLog("Cancellation requested. The current server request may need time to finish.");
    }

    private async Task LoadTablesAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var scope = cboTableScope.SelectedIndex == 0 ? TableScope.Custom : TableScope.All;
        var tables = await _dataverseService.LoadTablesAsync(scope, CreateProgress(), cancellationToken, forceRefresh);
        _tableRows.RaiseListChangedEvents = false;
        _tableRows.Clear();
        foreach (var table in tables)
        {
            _tableRows.Add(new TableGridRow
            {
                Selected = scope == TableScope.Custom && table.IsCustom,
                Table = table,
            });
        }

        _tableRows.RaiseListChangedEvents = true;
        _tableRows.ResetBindings();
        UpdateSelectionSummary();
    }

    private SnapshotCreateOptions CreateSnapshotOptions(
        IReadOnlyList<DataverseTableInfo> tables,
        bool includeStructure,
        bool includeData,
        string? content = null)
    {
        return new SnapshotCreateOptions
        {
            EnvironmentUrl = cboEnvironmentUrl.Text.Trim(),
            OutputFolder = txtSnapshotFolder.Text.Trim(),
            Scope = cboTableScope.SelectedIndex == 0 ? "Custom tables" : "All tables",
            Content = content ?? (includeData ? "Structure and data" : "Structure only"),
            SolutionUniqueName = txtSolutionUniqueName.Text.Trim(),
            Tables = tables,
            IncludeStructure = includeStructure,
            IncludeData = includeData,
        };
    }

    private List<DataverseTableInfo> GetSelectedTables()
    {
        dgvTables.EndEdit();
        return _tableRows.Where(row => row.Selected).Select(row => row.Table).ToList();
    }

    private bool ValidateCustomSelection(IReadOnlyCollection<DataverseTableInfo> tables)
    {
        if (tables.Count == 0)
        {
            MessageBox.Show(this, "Select at least one table.", "Table Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        var standard = tables.FirstOrDefault(table => !table.IsCustom);
        if (standard is not null)
        {
            MessageBox.Show(
                this,
                $"Snapshot and restore are currently limited to unmanaged custom tables. '{standard.LogicalName}' is not supported.",
                "Custom Tables Only",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(txtSolutionUniqueName.Text))
        {
            MessageBox.Show(this, "Enter the Dataverse solution unique name.", "Solution Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(txtSnapshotFolder.Text))
        {
            MessageBox.Show(this, "Choose a snapshot folder.", "Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        return true;
    }

    private async Task RunOperationAsync(string operationName, Func<CancellationToken, Task> operation)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        _operationCancellation = new CancellationTokenSource();
        SetBusyState(true);
        AppendLog($"[{DateTime.Now:HH:mm:ss}] {operationName} started.");
        try
        {
            await operation(_operationCancellation.Token);
            AppendLog($"[{DateTime.Now:HH:mm:ss}] {operationName} completed.");
        }
        catch (OperationCanceledException)
        {
            AppendLog($"[{DateTime.Now:HH:mm:ss}] {operationName} cancelled.");
        }
        catch (Exception exception)
        {
            AppendLog($"[{DateTime.Now:HH:mm:ss}] ERROR: {FormatException(exception)}");
            ShowError($"{operationName} failed", exception);
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            _busy = false;
            SetBusyState(false);
        }
    }

    private IProgress<string> CreateProgress()
    {
        return new Progress<string>(AppendLog);
    }

    private void AppendLog(string message)
    {
        if (txtLog.InvokeRequired)
        {
            txtLog.BeginInvoke(() => AppendLog(message));
            return;
        }

        txtLog.AppendText(message + Environment.NewLine);
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.ScrollToCaret();
    }

    private void SetBusyState(bool busy)
    {
        progressOperation.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        progressOperation.MarqueeAnimationSpeed = busy ? 30 : 0;
        btnCancelOperation.Enabled = busy;
        cboEnvironmentUrl.Enabled = !busy;
        cboUserName.Enabled = !busy;
        btnConnect.Enabled = !busy;
        btnSaveConnectionProfile.Enabled = !busy;
        btnRemoveConnectionProfile.Enabled = !busy && _connectionProfiles.Count > 0;
        btnRefreshTables.Enabled = !busy && _dataverseService.IsConnected;
        cboTableScope.Enabled = !busy && _dataverseService.IsConnected;
        dgvTables.Enabled = !busy && _dataverseService.IsConnected;
        btnCreateSnapshot.Enabled = !busy && _dataverseService.IsConnected;
        btnRestore.Enabled = !busy && _dataverseService.IsConnected;
        btnClearTableData.Enabled = !busy && _dataverseService.IsConnected;
        btnSelectAll.Enabled = !busy && _dataverseService.IsConnected;
        btnClearSelection.Enabled = !busy && _dataverseService.IsConnected;
    }

    private void UpdateActionState()
    {
        SetBusyState(_busy);
    }

    private void UpdateSelectionSummary()
    {
        var selected = _tableRows.Count(row => row.Selected);
        var custom = _tableRows.Count(row => row.Table.IsCustom);
        lblTableSummary.Text = $"{selected:N0} selected · {_tableRows.Count:N0} shown · {custom:N0} custom";
    }

    private void SetConnectionStatus(string text, Color color)
    {
        lblConnectionStatus.Text = text;
        lblConnectionStatus.ForeColor = color;
    }

    private void ShowError(string title, Exception exception)
    {
        MessageBox.Show(this, FormatException(exception), title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static string FormatException(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message.Trim();
            if (!string.IsNullOrWhiteSpace(message)
                && !messages.Any(existing => existing.Contains(message, StringComparison.Ordinal)))
            {
                messages.Add(message);
            }
        }

        return string.Join(Environment.NewLine + Environment.NewLine, messages);
    }

    private static bool ConfirmExactText(string title, string prompt, string expectedText)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(520, 260),
            Font = new Font("Segoe UI", 9F),
        };
        var label = new Label
        {
            Text = prompt,
            AutoSize = false,
            Location = new Point(16, 16),
            Size = new Size(488, 160),
        };
        var textBox = new TextBox
        {
            Location = new Point(16, 182),
            Size = new Size(488, 23),
        };
        var okButton = new Button
        {
            Text = "Clear Data",
            DialogResult = DialogResult.OK,
            Location = new Point(318, 219),
            Size = new Size(90, 30),
        };
        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(414, 219),
            Size = new Size(90, 30),
        };
        dialog.Controls.AddRange([label, textBox, okButton, cancelButton]);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;
        return dialog.ShowDialog() == DialogResult.OK && string.Equals(textBox.Text.Trim(), expectedText, StringComparison.Ordinal);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_busy)
        {
            var result = MessageBox.Show(
                this,
                "An operation is still running. Cancel it and close the application?",
                "Operation in Progress",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _operationCancellation?.Cancel();
        }

        _dataverseService.Dispose();
        base.OnFormClosing(e);
    }

    private sealed class TableGridRow : INotifyPropertyChanged
    {
        private bool _selected;

        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                {
                    return;
                }

                _selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            }
        }

        public required DataverseTableInfo Table { get; init; }
        public string DisplayName => Table.DisplayName;
        public string LogicalName => Table.LogicalName;
        public string TableType => Table.IsCustom ? "Custom" : "Standard";
        public string PrimaryId => Table.PrimaryIdAttribute;

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
