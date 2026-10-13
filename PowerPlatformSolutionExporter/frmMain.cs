using System.ComponentModel;
using PowerPlatformSolutionExporter.Models;
using PowerPlatformSolutionExporter.Services;

namespace PowerPlatformSolutionExporter;

public partial class frmMain : Form
{
    private readonly DataverseService _dataverseService = new();
    private readonly ConnectionProfileStore _connectionProfileStore = new();
    private readonly AppSettingsStore _appSettingsStore;
    private readonly BindingList<TableGridRow> _tableRows = [];
    private IReadOnlyList<ConnectionProfile> _connectionProfiles = [];
    private SolutionOverview? _solutionOverview;
    private CancellationTokenSource? _operationCancellation;
    private bool _busy;
    private bool _loadingConnectionProfiles;
    private bool _loadingSolutions;

    public frmMain()
        : this(new AppSettingsStore())
    {
    }

    public frmMain(AppSettingsStore appSettingsStore)
    {
        _appSettingsStore = appSettingsStore ?? throw new ArgumentNullException(nameof(appSettingsStore));
        InitializeComponent();
        Text = $"{Text} v{GetApplicationVersion()}";
        dgvTables.AutoGenerateColumns = false;
        dgvTables.DataSource = _tableRows;
        UpdateTableSummary();
        var defaultExportFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PowerPlatformSolutionExports");
        txtExportFolder.Text = _appSettingsStore.LoadExportFolder(defaultExportFolder);
        LoadConnectionProfiles();
        UpdateActionState();
    }

    private async void btnConnect_Click(object? sender, EventArgs e)
    {
        await RunOperationAsync("Connect", async cancellationToken =>
        {
            lblConnectionStatus.Text = "Connecting...";
            lblConnectionStatus.ForeColor = Color.DarkOrange;
            await _dataverseService.ConnectAsync(
                cboEnvironmentUrl.Text.Trim(),
                cboUserName.Text.Trim(),
                cancellationToken);
            SaveCurrentConnectionProfile(showConfirmation: false);
            lblConnectionStatus.Text = $"Connected: {_dataverseService.ConnectedEnvironmentName}";
            lblConnectionStatus.ForeColor = Color.SeaGreen;
            AppendLog("Loading unmanaged solutions...");
            await LoadSolutionsAsync(cancellationToken);
            AppendLog($"Connected to {_dataverseService.ConnectedEnvironmentUrl}.");
        });
    }

    private void btnSaveConnectionProfile_Click(object? sender, EventArgs e)
    {
        try
        {
            SaveCurrentConnectionProfile(showConfirmation: true);
        }
        catch (Exception exception)
        {
            ShowError("Unable to save connection profile", exception);
        }
    }

    private void btnRemoveConnectionProfile_Click(object? sender, EventArgs e)
    {
        try
        {
            if (!_connectionProfileStore.Remove(cboEnvironmentUrl.Text, cboUserName.Text))
            {
                MessageBox.Show(this, "The selected connection profile is not saved.", "Profile Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            LoadConnectionProfiles();
            MessageBox.Show(this, "Connection profile removed.", "Profile Removed", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            ShowError("Unable to remove connection profile", exception);
        }
    }

    private void cboEnvironmentUrl_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loadingConnectionProfiles || cboEnvironmentUrl.SelectedItem is not ConnectionProfile profile)
        {
            return;
        }

        cboUserName.Text = profile.UserName;
    }

    private async void cboSolutions_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loadingSolutions || !_dataverseService.IsConnected || cboSolutions.SelectedItem is not SolutionInfo solution)
        {
            return;
        }

        await RunOperationAsync("Load solution", cancellationToken => LoadSolutionOverviewAsync(solution, cancellationToken));
    }

    private async void btnRefreshSolutions_Click(object? sender, EventArgs e)
    {
        await RunOperationAsync("Refresh solutions", LoadSolutionsAsync);
    }

    private void btnBrowseExportFolder_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose where unmanaged solution ZIP files will be exported.",
            SelectedPath = txtExportFolder.Text,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            txtExportFolder.Text = dialog.SelectedPath;
            SaveExportFolderSetting(showError: true);
        }
    }

    private async void btnExportTableData_Click(object? sender, EventArgs e)
    {
        dgvTables.EndEdit();
        if (_solutionOverview is null || _solutionOverview.Tables.Count == 0)
        {
            MessageBox.Show(
                this,
                "Select an unmanaged solution that contains tables first.",
                "Tables Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var selectedTables = _tableRows
            .Where(row => row.IncludeInDataExport)
            .Select(row => row.Table)
            .ToList();
        if (selectedTables.Count == 0)
        {
            MessageBox.Show(
                this,
                "Select at least one table using the Export data checkboxes.",
                "Tables Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(txtExportFolder.Text))
        {
            MessageBox.Show(this, "Choose an export folder.", "Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SaveExportFolderSetting(showError: true);
        var sourceOverview = _solutionOverview;
        var overview = new SolutionOverview
        {
            Solution = sourceOverview.Solution,
            Tables = selectedTables,
            ComponentCounts = sourceOverview.ComponentCounts,
            CanvasApps = sourceOverview.CanvasApps,
        };
        var confirmation = MessageBox.Show(
            this,
            $"Export table data as Power Apps Web UI-format CSV files?\n\n" +
            $"Solution: {overview.Solution.FriendlyName}\n" +
            $"Selected tables: {overview.Tables.Count:N0}\n" +
            $"Destination: {txtExportFolder.Text.Trim()}\n\n" +
            "This is a data-only export. Each table is written to a separate UTF-8 CSV file. " +
            "CSV files use logical column names and raw values, including the stored read-only columns exported by Web UI. " +
            "Unsupported export fields are skipped and documented in manifest.json.\n\n" +
            "CSV import inserts or updates records; it does not delete extra records from Dataverse.\n\n" +
            "The exported CSV files are unencrypted and can contain sensitive table data. Store them securely.",
            "Confirm Table Data Export",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        await RunOperationAsync("Export table data", async cancellationToken =>
        {
            var result = await new TableDataExporter(_dataverseService).ExportAsync(
                overview,
                txtExportFolder.Text.Trim(),
                CreateProgress(),
                cancellationToken);
            var dependencyWarning = result.DependencyCycleDetected
                ? "\n\nA lookup dependency cycle was detected. Follow import-guide.txt and retry unresolved lookup rows after the first pass."
                : string.Empty;
            MessageBox.Show(
                this,
                $"Table data exported in Power Apps Web UI CSV format.\n\n" +
                $"Tables: {result.TableCount:N0}\n" +
                $"Records: {result.RecordCount:N0}\n" +
                $"Skipped columns: {result.SkippedColumnCount:N0}\n\n" +
                $"Review import-guide.txt and manifest.json before importing.\n\n" +
                result.OutputFolder + dependencyWarning,
                "Table Data Export Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });
    }

    private async void btnExport_Click(object? sender, EventArgs e)
    {
        if (cboSolutions.SelectedItem is not SolutionInfo solution)
        {
            MessageBox.Show(this, "Select an unmanaged solution first.", "Solution Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(txtExportFolder.Text))
        {
            MessageBox.Show(this, "Choose an export folder.", "Folder Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SaveExportFolderSetting(showError: true);

        var nextVersion = SolutionVersion.IncrementRevision(solution.Version);
        var confirmation = MessageBox.Show(
            this,
            $"Publish and export this solution as unmanaged?\n\n" +
            $"Solution: {solution.FriendlyName}\n" +
            $"Unique name: {solution.UniqueName}\n" +
            $"Version: {solution.Version} -> {nextVersion}\n\n" +
            "Table checkboxes affect only Export Table Data; they do not limit this Solution ZIP. " +
            "The ZIP exports the full selected Solution as it is currently configured. A segmented table still contains only the table assets included in the Solution.\n\n" +
            "This publishes ALL saved customizations in the connected environment, not only this solution. " +
            "Saved Canvas App drafts in this solution must already be published or export will be blocked.\n\n" +
            "Unsaved edits still open in Power Apps Studio cannot be detected or saved by this tool.",
            "Confirm Publish and Export",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        await RunOperationAsync("Publish and export", async cancellationToken =>
        {
            var result = await _dataverseService.PublishAndExportUnmanagedAsync(
                solution,
                txtExportFolder.Text.Trim(),
                CreateProgress(),
                cancellationToken);
            await LoadSolutionsAsync(cancellationToken, result.ExportedVersion, solution.UniqueName);
            MessageBox.Show(
                this,
                $"Unmanaged solution exported successfully.\n\n" +
                $"Version: {result.PreviousVersion} -> {result.ExportedVersion}\n" +
                $"Tables: {result.TableCount:N0}\n" +
                $"Root components: {result.ComponentCount:N0}\n" +
                $"SHA-256: {result.Sha256}\n\n" +
                result.FilePath,
                "Export Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });
    }

    private void dgvTables_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (_busy ||
            e.RowIndex < 0 ||
            e.ColumnIndex == colExportData.Index ||
            dgvTables.Rows[e.RowIndex].DataBoundItem is not TableGridRow row)
        {
            return;
        }

        using var viewer = new frmTableViewer(_dataverseService, row.Table);
        viewer.ShowDialog(this);
    }

    private void dgvTables_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (dgvTables.IsCurrentCellDirty && dgvTables.CurrentCell?.ColumnIndex == colExportData.Index)
        {
            dgvTables.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void dgvTables_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != colExportData.Index)
        {
            return;
        }

        UpdateTableSummary();
        UpdateActionState();
    }

    private void chkSelectAllTableData_Click(object? sender, EventArgs e)
    {
        if (_busy || _tableRows.Count == 0)
        {
            return;
        }

        var selectAll = _tableRows.Any(row => !row.IncludeInDataExport);
        foreach (var row in _tableRows)
        {
            row.IncludeInDataExport = selectAll;
        }

        _tableRows.ResetBindings();
        UpdateTableSummary();
        UpdateActionState();
    }

    private void btnCancelOperation_Click(object? sender, EventArgs e)
    {
        _operationCancellation?.Cancel();
    }

    private async Task LoadSolutionsAsync(CancellationToken cancellationToken)
    {
        await LoadSolutionsAsync(cancellationToken, preferredVersion: null, preferredUniqueName: null);
    }

    private async Task LoadSolutionsAsync(
        CancellationToken cancellationToken,
        string? preferredVersion,
        string? preferredUniqueName)
    {
        var previousUniqueName = preferredUniqueName
                                 ?? (cboSolutions.SelectedItem as SolutionInfo)?.UniqueName;
        var solutions = await _dataverseService.LoadUnmanagedSolutionsAsync(cancellationToken);
        _loadingSolutions = true;
        try
        {
            cboSolutions.Items.Clear();
            foreach (var solution in solutions)
            {
                cboSolutions.Items.Add(solution);
            }

            var selected = solutions.FirstOrDefault(solution => string.Equals(
                               solution.UniqueName,
                               previousUniqueName,
                               StringComparison.OrdinalIgnoreCase))
                           ?? solutions.FirstOrDefault();
            if (selected is not null)
            {
                cboSolutions.SelectedItem = selected;
            }
        }
        finally
        {
            _loadingSolutions = false;
        }

        if (cboSolutions.SelectedItem is SolutionInfo selectedSolution)
        {
            await LoadSolutionOverviewAsync(selectedSolution, cancellationToken);
            if (!string.IsNullOrWhiteSpace(preferredVersion) &&
                !string.Equals(selectedSolution.Version, preferredVersion, StringComparison.Ordinal))
            {
                AppendLog($"Warning: expected solution version {preferredVersion}, but Dataverse reports {selectedSolution.Version}.");
            }
        }
        else
        {
            _solutionOverview = null;
            _tableRows.Clear();
            ClearSolutionDetails();
        }

        AppendLog($"Loaded {solutions.Count:N0} unmanaged solutions.");
    }

    private async Task LoadSolutionOverviewAsync(SolutionInfo solution, CancellationToken cancellationToken)
    {
        _solutionOverview = await _dataverseService.LoadSolutionOverviewAsync(
            solution,
            CreateProgress(),
            cancellationToken);
        _tableRows.RaiseListChangedEvents = false;
        _tableRows.Clear();
        foreach (var table in _solutionOverview.Tables)
        {
            _tableRows.Add(new TableGridRow(table));
        }

        _tableRows.RaiseListChangedEvents = true;
        _tableRows.ResetBindings();
        txtCurrentVersion.Text = solution.Version;
        txtNextVersion.Text = SolutionVersion.IncrementRevision(solution.Version);
        txtPublisher.Text = solution.PublisherName;
        lblComponentSummary.Text = BuildComponentSummary(_solutionOverview);
        UpdateTableSummary();
        UpdateActionState();
    }

    private static string BuildComponentSummary(SolutionOverview overview)
    {
        var parts = new List<string>
        {
            $"{overview.GetComponentCount(DataverseService.EntityComponentType):N0} tables",
            $"{overview.GetComponentCount(DataverseService.CanvasAppComponentType):N0} Canvas apps",
            $"{overview.GetComponentCount(DataverseService.AppModuleComponentType):N0} model-driven apps",
            $"{overview.GetComponentCount(DataverseService.WorkflowComponentType):N0} workflows",
            $"{overview.GetComponentCount(DataverseService.PluginAssemblyComponentType):N0} plug-in assemblies",
            $"{overview.GetComponentCount(DataverseService.WebResourceComponentType):N0} web resources",
            $"{overview.GetComponentCount(DataverseService.SecurityRoleComponentType):N0} security roles",
        };
        var unpublishedCount = overview.CanvasApps.Count(app => !app.IsPublished);
        var canvasComponentCount = overview.GetComponentCount(DataverseService.CanvasAppComponentType);
        var componentRecordCount = overview.ComponentCounts.Values.Sum();
        var canvasStatus = canvasComponentCount != overview.CanvasApps.Count
            ? $"Canvas App verification incomplete: {overview.CanvasApps.Count:N0} of {canvasComponentCount:N0} app(s) resolved. Export is disabled."
            : unpublishedCount == 0
                ? $"Canvas App verification: {overview.CanvasApps.Count:N0} published, no saved unpublished version detected."
                : $"Canvas App verification: {unpublishedCount:N0} saved unpublished app(s). Publish in Power Apps, then Reload.";
        return string.Join("  |  ", parts) + Environment.NewLine +
               $"{componentRecordCount:N0} explicit solution-component records detected; Dataverse resolves required dependencies during export." + Environment.NewLine +
               canvasStatus;
    }

    private void ClearSolutionDetails()
    {
        txtCurrentVersion.Clear();
        txtNextVersion.Clear();
        txtPublisher.Clear();
        lblComponentSummary.Text = "Select an unmanaged solution to inspect its components.";
        UpdateTableSummary();
    }

    private void LoadConnectionProfiles(ConnectionProfile? selectProfile = null)
    {
        _loadingConnectionProfiles = true;
        try
        {
            _connectionProfiles = _connectionProfileStore.Load();
            var selectedUrl = selectProfile?.EnvironmentUrl ?? cboEnvironmentUrl.Text;
            var selectedUser = selectProfile?.UserName ?? cboUserName.Text;
            cboEnvironmentUrl.Items.Clear();
            cboUserName.Items.Clear();
            foreach (var profile in _connectionProfiles)
            {
                cboEnvironmentUrl.Items.Add(profile);
            }

            foreach (var userName in _connectionProfiles
                         .Select(profile => profile.UserName)
                         .Where(userName => !string.IsNullOrWhiteSpace(userName))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cboUserName.Items.Add(userName);
            }

            var profileToSelect = selectProfile
                                  ?? _connectionProfiles.FirstOrDefault(profile =>
                                      string.Equals(profile.EnvironmentUrl, selectedUrl, StringComparison.OrdinalIgnoreCase) &&
                                      string.Equals(profile.UserName, selectedUser, StringComparison.OrdinalIgnoreCase))
                                  ?? _connectionProfiles.FirstOrDefault();
            if (profileToSelect is not null)
            {
                cboEnvironmentUrl.SelectedItem = _connectionProfiles.First(profile =>
                    string.Equals(profile.EnvironmentUrl, profileToSelect.EnvironmentUrl, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(profile.UserName, profileToSelect.UserName, StringComparison.OrdinalIgnoreCase));
                cboEnvironmentUrl.Text = profileToSelect.EnvironmentUrl;
                cboUserName.Text = profileToSelect.UserName;
            }
        }
        finally
        {
            _loadingConnectionProfiles = false;
        }
    }

    private void SaveCurrentConnectionProfile(bool showConfirmation)
    {
        var profile = _connectionProfileStore.Save(cboEnvironmentUrl.Text, cboUserName.Text);
        LoadConnectionProfiles(profile);
        if (showConfirmation)
        {
            MessageBox.Show(
                this,
                "Connection profile saved locally. Passwords and access tokens are not stored in the profile file.",
                "Profile Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private async Task RunOperationAsync(string operationName, Func<CancellationToken, Task> operation)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        _operationCancellation = new CancellationTokenSource();
        UpdateActionState();
        AppendLog($"{operationName} started.");
        try
        {
            await operation(_operationCancellation.Token);
            AppendLog($"{operationName} completed.");
        }
        catch (OperationCanceledException)
        {
            AppendLog($"{operationName} cancelled.");
        }
        catch (Exception exception)
        {
            AppendLog($"ERROR: {exception.Message}");
            ShowError($"{operationName} failed", exception);
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            _busy = false;
            UpdateActionState();
        }
    }

    private IProgress<string> CreateProgress()
    {
        return new Progress<string>(AppendLog);
    }

    private void AppendLog(string message)
    {
        if (txtOperationLog.InvokeRequired)
        {
            txtOperationLog.BeginInvoke(() => AppendLog(message));
            return;
        }

        txtOperationLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        txtOperationLog.SelectionStart = txtOperationLog.TextLength;
        txtOperationLog.ScrollToCaret();
    }

    private void UpdateActionState()
    {
        var connected = _dataverseService.IsConnected;
        cboEnvironmentUrl.Enabled = !_busy;
        cboUserName.Enabled = !_busy;
        btnConnect.Enabled = !_busy;
        btnSaveConnectionProfile.Enabled = !_busy;
        btnRemoveConnectionProfile.Enabled = !_busy;
        cboSolutions.Enabled = connected && !_busy;
        btnRefreshSolutions.Enabled = connected && !_busy;
        txtExportFolder.Enabled = !_busy;
        btnBrowseExportFolder.Enabled = !_busy;
        btnExportTableData.Enabled = connected && !_busy && _tableRows.Any(row => row.IncludeInDataExport);
        chkSelectAllTableData.Enabled = connected && !_busy && _tableRows.Count > 0;
        btnExport.Enabled = connected && !_busy && IsCanvasPublishPreflightReady();
        dgvTables.Enabled = connected && !_busy;
        btnCancelOperation.Enabled = _busy;
        progressOperation.Style = _busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        progressOperation.MarqueeAnimationSpeed = _busy ? 25 : 0;
        if (!_busy)
        {
            progressOperation.Value = 0;
        }

        if (_busy)
        {
            btnCancelOperation.Select();
        }
        else if (splitMain.Focused)
        {
            btnConnect.Select();
        }
    }

    private static string GetApplicationVersion()
    {
        var version = typeof(frmMain).Assembly.GetName().Version;
        return version is null
            ? "0.0.0"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private void UpdateTableSummary()
    {
        var selectedCount = _tableRows.Count(row => row.IncludeInDataExport);
        lblTableSummary.Text = $"{_tableRows.Count:N0} tables | {selectedCount:N0} selected for data export. Double-click a table to inspect.";
        chkSelectAllTableData.CheckState = selectedCount switch
        {
            0 => CheckState.Unchecked,
            _ when selectedCount == _tableRows.Count => CheckState.Checked,
            _ => CheckState.Indeterminate,
        };
    }

    private bool IsCanvasPublishPreflightReady()
    {
        if (_solutionOverview is null)
        {
            return false;
        }

        var componentCount = _solutionOverview.GetComponentCount(DataverseService.CanvasAppComponentType);
        return _solutionOverview.CanvasApps.Count == componentCount &&
               _solutionOverview.CanvasApps.All(app => app.IsPublished);
    }

    private void ShowError(string title, Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            var text = $"{current.GetType().Name}: {current.Message}";
            if (!messages.Contains(text, StringComparer.Ordinal))
            {
                messages.Add(text);
            }
        }

        MessageBox.Show(this, string.Join(Environment.NewLine, messages), title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void SaveExportFolderSetting(bool showError)
    {
        if (string.IsNullOrWhiteSpace(txtExportFolder.Text))
        {
            return;
        }

        try
        {
            _appSettingsStore.SaveExportFolder(txtExportFolder.Text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (showError)
            {
                ShowError("Unable to save the export folder", exception);
            }
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SaveExportFolderSetting(showError: false);
        _operationCancellation?.Cancel();
        _dataverseService.Dispose();
        base.OnFormClosed(e);
    }

    private sealed class TableGridRow
    {
        public TableGridRow(DataverseTableInfo table)
        {
            Table = table;
        }

        public DataverseTableInfo Table { get; }
        public bool IncludeInDataExport { get; set; } = true;
        public string DisplayName => Table.DisplayName;
        public string LogicalName => Table.LogicalName;
        public string Type => Table.IsCustom ? "Custom" : "Standard";
        public string PrimaryId => Table.PrimaryIdAttribute;
        public int ColumnCount => Table.Metadata.Attributes?.Length ?? 0;
    }
}
