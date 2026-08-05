namespace dataverse_table_backup_restore;

partial class frmMain
{
    private System.ComponentModel.IContainer components = null;
    private Panel pnlHeader;
    private Label lblTitle;
    private Label lblSubtitle;
    private GroupBox grpConnection;
    private Label lblEnvironmentUrl;
    private TextBox txtEnvironmentUrl;
    private Label lblUserName;
    private TextBox txtUserName;
    private Button btnConnect;
    private Label lblConnectionStatus;
    private SplitContainer splitMain;
    private GroupBox grpTables;
    private FlowLayoutPanel pnlTableToolbar;
    private Label lblTableScope;
    private ComboBox cboTableScope;
    private Button btnRefreshTables;
    private Button btnSelectAll;
    private Button btnClearSelection;
    private DataGridView dgvTables;
    private DataGridViewCheckBoxColumn colSelected;
    private DataGridViewTextBoxColumn colDisplayName;
    private DataGridViewTextBoxColumn colLogicalName;
    private DataGridViewTextBoxColumn colTableType;
    private DataGridViewTextBoxColumn colDesignReference;
    private DataGridViewTextBoxColumn colPrimaryId;
    private Label lblTableSummary;
    private TableLayoutPanel pnlActions;
    private GroupBox grpCreateSnapshot;
    private Label lblSolutionUniqueName;
    private TextBox txtSolutionUniqueName;
    private Label lblSnapshotContent;
    private ComboBox cboSnapshotContent;
    private Label lblSnapshotFolder;
    private TextBox txtSnapshotFolder;
    private Button btnBrowseSnapshotFolder;
    private Button btnCreateSnapshot;
    private Label lblSnapshotNote;
    private GroupBox grpRestore;
    private Label lblSnapshotFile;
    private TextBox txtSnapshotFile;
    private Button btnBrowseSnapshotFile;
    private CheckBox chkRestoreStructure;
    private CheckBox chkRestoreData;
    private CheckBox chkSafetySnapshot;
    private CheckBox chkDeleteExtraRecords;
    private Button btnRestore;
    private GroupBox grpDangerZone;
    private Label lblClearWarning;
    private Button btnClearTableData;
    private GroupBox grpLog;
    private TextBox txtLog;
    private Panel pnlStatus;
    private ProgressBar progressOperation;
    private Button btnCancelOperation;
    private Label lblSafetyNote;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components is not null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        pnlHeader = new Panel();
        lblTitle = new Label();
        lblSubtitle = new Label();
        grpConnection = new GroupBox();
        lblEnvironmentUrl = new Label();
        txtEnvironmentUrl = new TextBox();
        lblUserName = new Label();
        txtUserName = new TextBox();
        btnConnect = new Button();
        lblConnectionStatus = new Label();
        splitMain = new SplitContainer();
        grpTables = new GroupBox();
        pnlTableToolbar = new FlowLayoutPanel();
        lblTableScope = new Label();
        cboTableScope = new ComboBox();
        btnRefreshTables = new Button();
        btnSelectAll = new Button();
        btnClearSelection = new Button();
        dgvTables = new DataGridView();
        colSelected = new DataGridViewCheckBoxColumn();
        colDisplayName = new DataGridViewTextBoxColumn();
        colLogicalName = new DataGridViewTextBoxColumn();
        colTableType = new DataGridViewTextBoxColumn();
        colDesignReference = new DataGridViewTextBoxColumn();
        colPrimaryId = new DataGridViewTextBoxColumn();
        lblTableSummary = new Label();
        pnlActions = new TableLayoutPanel();
        grpCreateSnapshot = new GroupBox();
        lblSolutionUniqueName = new Label();
        txtSolutionUniqueName = new TextBox();
        lblSnapshotContent = new Label();
        cboSnapshotContent = new ComboBox();
        lblSnapshotFolder = new Label();
        txtSnapshotFolder = new TextBox();
        btnBrowseSnapshotFolder = new Button();
        btnCreateSnapshot = new Button();
        lblSnapshotNote = new Label();
        grpRestore = new GroupBox();
        lblSnapshotFile = new Label();
        txtSnapshotFile = new TextBox();
        btnBrowseSnapshotFile = new Button();
        chkRestoreStructure = new CheckBox();
        chkRestoreData = new CheckBox();
        chkSafetySnapshot = new CheckBox();
        chkDeleteExtraRecords = new CheckBox();
        btnRestore = new Button();
        grpDangerZone = new GroupBox();
        lblClearWarning = new Label();
        btnClearTableData = new Button();
        grpLog = new GroupBox();
        txtLog = new TextBox();
        pnlStatus = new Panel();
        progressOperation = new ProgressBar();
        btnCancelOperation = new Button();
        lblSafetyNote = new Label();
        pnlHeader.SuspendLayout();
        grpConnection.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
        splitMain.Panel1.SuspendLayout();
        splitMain.Panel2.SuspendLayout();
        splitMain.SuspendLayout();
        grpTables.SuspendLayout();
        pnlTableToolbar.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvTables).BeginInit();
        pnlActions.SuspendLayout();
        grpCreateSnapshot.SuspendLayout();
        grpRestore.SuspendLayout();
        grpDangerZone.SuspendLayout();
        grpLog.SuspendLayout();
        pnlStatus.SuspendLayout();
        SuspendLayout();
        //
        // pnlHeader
        //
        pnlHeader.BackColor = Color.FromArgb(28, 42, 58);
        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(lblSubtitle);
        pnlHeader.Dock = DockStyle.Top;
        pnlHeader.Location = new Point(0, 0);
        pnlHeader.Name = "pnlHeader";
        pnlHeader.Padding = new Padding(18, 10, 18, 8);
        pnlHeader.Size = new Size(1400, 70);
        pnlHeader.TabIndex = 0;
        //
        // lblTitle
        //
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold);
        lblTitle.ForeColor = Color.White;
        lblTitle.Location = new Point(18, 8);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(330, 31);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "Dataverse Snapshot Manager";
        //
        // lblSubtitle
        //
        lblSubtitle.AutoSize = true;
        lblSubtitle.ForeColor = Color.FromArgb(196, 210, 224);
        lblSubtitle.Location = new Point(20, 43);
        lblSubtitle.Name = "lblSubtitle";
        lblSubtitle.Size = new Size(577, 15);
        lblSubtitle.TabIndex = 1;
        lblSubtitle.Text = "Capture custom table structure and data, restore a snapshot, or safely clear records from one table.";
        //
        // grpConnection
        //
        grpConnection.Controls.Add(lblEnvironmentUrl);
        grpConnection.Controls.Add(txtEnvironmentUrl);
        grpConnection.Controls.Add(lblUserName);
        grpConnection.Controls.Add(txtUserName);
        grpConnection.Controls.Add(btnConnect);
        grpConnection.Controls.Add(lblConnectionStatus);
        grpConnection.Dock = DockStyle.Top;
        grpConnection.Location = new Point(10, 80);
        grpConnection.Name = "grpConnection";
        grpConnection.Padding = new Padding(10);
        grpConnection.Size = new Size(1380, 92);
        grpConnection.TabIndex = 1;
        grpConnection.TabStop = false;
        grpConnection.Text = "Connection";
        //
        // lblEnvironmentUrl
        //
        lblEnvironmentUrl.AutoSize = true;
        lblEnvironmentUrl.Location = new Point(18, 28);
        lblEnvironmentUrl.Name = "lblEnvironmentUrl";
        lblEnvironmentUrl.Size = new Size(98, 15);
        lblEnvironmentUrl.TabIndex = 0;
        lblEnvironmentUrl.Text = "Environment URL";
        //
        // txtEnvironmentUrl
        //
        txtEnvironmentUrl.Location = new Point(122, 24);
        txtEnvironmentUrl.Name = "txtEnvironmentUrl";
        txtEnvironmentUrl.Size = new Size(355, 23);
        txtEnvironmentUrl.TabIndex = 1;
        //
        // lblUserName
        //
        lblUserName.AutoSize = true;
        lblUserName.Location = new Point(498, 28);
        lblUserName.Name = "lblUserName";
        lblUserName.Size = new Size(63, 15);
        lblUserName.TabIndex = 2;
        lblUserName.Text = "User name";
        //
        // txtUserName
        //
        txtUserName.Location = new Point(567, 24);
        txtUserName.Name = "txtUserName";
        txtUserName.Size = new Size(250, 23);
        txtUserName.TabIndex = 3;
        //
        // btnConnect
        //
        btnConnect.BackColor = Color.FromArgb(33, 115, 70);
        btnConnect.FlatAppearance.BorderSize = 0;
        btnConnect.FlatStyle = FlatStyle.Flat;
        btnConnect.ForeColor = Color.White;
        btnConnect.Location = new Point(837, 22);
        btnConnect.Name = "btnConnect";
        btnConnect.Size = new Size(108, 28);
        btnConnect.TabIndex = 4;
        btnConnect.Text = "Connect";
        btnConnect.UseVisualStyleBackColor = false;
        btnConnect.Click += btnConnect_Click;
        //
        // lblConnectionStatus
        //
        lblConnectionStatus.AutoSize = true;
        lblConnectionStatus.ForeColor = Color.DimGray;
        lblConnectionStatus.Location = new Point(18, 59);
        lblConnectionStatus.Name = "lblConnectionStatus";
        lblConnectionStatus.Size = new Size(90, 15);
        lblConnectionStatus.TabIndex = 5;
        lblConnectionStatus.Text = "Not connected";
        //
        // splitMain
        //
        splitMain.Dock = DockStyle.Fill;
        splitMain.Location = new Point(10, 172);
        splitMain.Name = "splitMain";
        //
        // splitMain.Panel1
        //
        splitMain.Panel1.Controls.Add(grpTables);
        splitMain.Panel1.Padding = new Padding(0, 8, 5, 0);
        //
        // splitMain.Panel2
        //
        splitMain.Panel2.Controls.Add(pnlActions);
        splitMain.Panel2.Padding = new Padding(5, 8, 0, 0);
        splitMain.Size = new Size(1380, 475);
        splitMain.SplitterDistance = 845;
        splitMain.TabIndex = 2;
        //
        // grpTables
        //
        grpTables.Controls.Add(dgvTables);
        grpTables.Controls.Add(lblTableSummary);
        grpTables.Controls.Add(pnlTableToolbar);
        grpTables.Dock = DockStyle.Fill;
        grpTables.Location = new Point(0, 8);
        grpTables.Name = "grpTables";
        grpTables.Padding = new Padding(8);
        grpTables.Size = new Size(840, 467);
        grpTables.TabIndex = 0;
        grpTables.TabStop = false;
        grpTables.Text = "Tables";
        //
        // pnlTableToolbar
        //
        pnlTableToolbar.Controls.Add(lblTableScope);
        pnlTableToolbar.Controls.Add(cboTableScope);
        pnlTableToolbar.Controls.Add(btnRefreshTables);
        pnlTableToolbar.Controls.Add(btnSelectAll);
        pnlTableToolbar.Controls.Add(btnClearSelection);
        pnlTableToolbar.Dock = DockStyle.Top;
        pnlTableToolbar.Location = new Point(8, 24);
        pnlTableToolbar.Name = "pnlTableToolbar";
        pnlTableToolbar.Padding = new Padding(2, 3, 0, 3);
        pnlTableToolbar.Size = new Size(824, 38);
        pnlTableToolbar.TabIndex = 0;
        pnlTableToolbar.WrapContents = false;
        //
        // lblTableScope
        //
        lblTableScope.Anchor = AnchorStyles.Left;
        lblTableScope.AutoSize = true;
        lblTableScope.Margin = new Padding(3, 7, 5, 0);
        lblTableScope.Name = "lblTableScope";
        lblTableScope.Size = new Size(39, 15);
        lblTableScope.TabIndex = 0;
        lblTableScope.Text = "Scope";
        //
        // cboTableScope
        //
        cboTableScope.DropDownStyle = ComboBoxStyle.DropDownList;
        cboTableScope.FormattingEnabled = true;
        cboTableScope.Items.AddRange(new object[] { "Custom tables", "All tables" });
        cboTableScope.Margin = new Padding(0, 2, 10, 3);
        cboTableScope.Name = "cboTableScope";
        cboTableScope.Size = new Size(135, 23);
        cboTableScope.TabIndex = 1;
        cboTableScope.SelectedIndexChanged += cboTableScope_SelectedIndexChanged;
        //
        // btnRefreshTables
        //
        btnRefreshTables.Margin = new Padding(0, 1, 8, 2);
        btnRefreshTables.Name = "btnRefreshTables";
        btnRefreshTables.Size = new Size(88, 27);
        btnRefreshTables.TabIndex = 2;
        btnRefreshTables.Text = "Refresh";
        btnRefreshTables.UseVisualStyleBackColor = true;
        btnRefreshTables.Click += btnRefreshTables_Click;
        //
        // btnSelectAll
        //
        btnSelectAll.Margin = new Padding(0, 1, 8, 2);
        btnSelectAll.Name = "btnSelectAll";
        btnSelectAll.Size = new Size(88, 27);
        btnSelectAll.TabIndex = 3;
        btnSelectAll.Text = "Select custom";
        btnSelectAll.UseVisualStyleBackColor = true;
        btnSelectAll.Click += btnSelectAll_Click;
        //
        // btnClearSelection
        //
        btnClearSelection.Margin = new Padding(0, 1, 3, 2);
        btnClearSelection.Name = "btnClearSelection";
        btnClearSelection.Size = new Size(105, 27);
        btnClearSelection.TabIndex = 4;
        btnClearSelection.Text = "Clear selection";
        btnClearSelection.UseVisualStyleBackColor = true;
        btnClearSelection.Click += btnClearSelection_Click;
        //
        // dgvTables
        //
        dgvTables.AllowUserToAddRows = false;
        dgvTables.AllowUserToDeleteRows = false;
        dgvTables.AllowUserToResizeRows = false;
        dgvTables.BackgroundColor = Color.White;
        dgvTables.BorderStyle = BorderStyle.Fixed3D;
        dgvTables.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvTables.Columns.AddRange(new DataGridViewColumn[] { colSelected, colDisplayName, colLogicalName, colTableType, colDesignReference, colPrimaryId });
        dgvTables.Dock = DockStyle.Fill;
        dgvTables.Location = new Point(8, 62);
        dgvTables.MultiSelect = false;
        dgvTables.Name = "dgvTables";
        dgvTables.RowHeadersVisible = false;
        dgvTables.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvTables.Size = new Size(824, 371);
        dgvTables.TabIndex = 1;
        dgvTables.CellValueChanged += dgvTables_CellValueChanged;
        dgvTables.CurrentCellDirtyStateChanged += dgvTables_CurrentCellDirtyStateChanged;
        //
        // colSelected
        //
        colSelected.DataPropertyName = "Selected";
        colSelected.HeaderText = "Use";
        colSelected.Name = "colSelected";
        colSelected.Width = 45;
        //
        // colDisplayName
        //
        colDisplayName.DataPropertyName = "DisplayName";
        colDisplayName.HeaderText = "Display name";
        colDisplayName.Name = "colDisplayName";
        colDisplayName.ReadOnly = true;
        colDisplayName.Width = 205;
        //
        // colLogicalName
        //
        colLogicalName.DataPropertyName = "LogicalName";
        colLogicalName.HeaderText = "Logical name";
        colLogicalName.Name = "colLogicalName";
        colLogicalName.ReadOnly = true;
        colLogicalName.Width = 245;
        //
        // colTableType
        //
        colTableType.DataPropertyName = "TableType";
        colTableType.HeaderText = "Type";
        colTableType.Name = "colTableType";
        colTableType.ReadOnly = true;
        colTableType.Width = 72;
        //
        // colDesignReference
        //
        colDesignReference.DataPropertyName = "DesignReference";
        colDesignReference.HeaderText = "Design";
        colDesignReference.Name = "colDesignReference";
        colDesignReference.ReadOnly = true;
        colDesignReference.Width = 70;
        //
        // colPrimaryId
        //
        colPrimaryId.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        colPrimaryId.DataPropertyName = "PrimaryId";
        colPrimaryId.HeaderText = "Primary ID";
        colPrimaryId.MinimumWidth = 120;
        colPrimaryId.Name = "colPrimaryId";
        colPrimaryId.ReadOnly = true;
        //
        // lblTableSummary
        //
        lblTableSummary.Dock = DockStyle.Bottom;
        lblTableSummary.ForeColor = Color.DimGray;
        lblTableSummary.Location = new Point(8, 433);
        lblTableSummary.Name = "lblTableSummary";
        lblTableSummary.Padding = new Padding(2, 5, 0, 0);
        lblTableSummary.Size = new Size(824, 26);
        lblTableSummary.TabIndex = 2;
        lblTableSummary.Text = "0 selected";
        //
        // pnlActions
        //
        pnlActions.ColumnCount = 1;
        pnlActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        pnlActions.Controls.Add(grpCreateSnapshot, 0, 0);
        pnlActions.Controls.Add(grpRestore, 0, 1);
        pnlActions.Controls.Add(grpDangerZone, 0, 2);
        pnlActions.Dock = DockStyle.Fill;
        pnlActions.Location = new Point(5, 8);
        pnlActions.Name = "pnlActions";
        pnlActions.RowCount = 3;
        pnlActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 198F));
        pnlActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 194F));
        pnlActions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        pnlActions.Size = new Size(526, 467);
        pnlActions.TabIndex = 0;
        //
        // grpCreateSnapshot
        //
        grpCreateSnapshot.Controls.Add(lblSolutionUniqueName);
        grpCreateSnapshot.Controls.Add(txtSolutionUniqueName);
        grpCreateSnapshot.Controls.Add(lblSnapshotContent);
        grpCreateSnapshot.Controls.Add(cboSnapshotContent);
        grpCreateSnapshot.Controls.Add(lblSnapshotFolder);
        grpCreateSnapshot.Controls.Add(txtSnapshotFolder);
        grpCreateSnapshot.Controls.Add(btnBrowseSnapshotFolder);
        grpCreateSnapshot.Controls.Add(btnCreateSnapshot);
        grpCreateSnapshot.Controls.Add(lblSnapshotNote);
        grpCreateSnapshot.Dock = DockStyle.Fill;
        grpCreateSnapshot.Location = new Point(0, 0);
        grpCreateSnapshot.Margin = new Padding(0, 0, 0, 6);
        grpCreateSnapshot.Name = "grpCreateSnapshot";
        grpCreateSnapshot.Padding = new Padding(10);
        grpCreateSnapshot.Size = new Size(526, 192);
        grpCreateSnapshot.TabIndex = 0;
        grpCreateSnapshot.TabStop = false;
        grpCreateSnapshot.Text = "Create Snapshot";
        //
        // lblSolutionUniqueName
        //
        lblSolutionUniqueName.AutoSize = true;
        lblSolutionUniqueName.Location = new Point(14, 28);
        lblSolutionUniqueName.Name = "lblSolutionUniqueName";
        lblSolutionUniqueName.Size = new Size(83, 15);
        lblSolutionUniqueName.TabIndex = 0;
        lblSolutionUniqueName.Text = "Solution name";
        //
        // txtSolutionUniqueName
        //
        txtSolutionUniqueName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtSolutionUniqueName.Location = new Point(111, 24);
        txtSolutionUniqueName.Name = "txtSolutionUniqueName";
        txtSolutionUniqueName.Size = new Size(400, 23);
        txtSolutionUniqueName.TabIndex = 1;
        //
        // lblSnapshotContent
        //
        lblSnapshotContent.AutoSize = true;
        lblSnapshotContent.Location = new Point(14, 60);
        lblSnapshotContent.Name = "lblSnapshotContent";
        lblSnapshotContent.Size = new Size(50, 15);
        lblSnapshotContent.TabIndex = 2;
        lblSnapshotContent.Text = "Content";
        //
        // cboSnapshotContent
        //
        cboSnapshotContent.DropDownStyle = ComboBoxStyle.DropDownList;
        cboSnapshotContent.FormattingEnabled = true;
        cboSnapshotContent.Items.AddRange(new object[] { "Structure only", "Structure + data" });
        cboSnapshotContent.Location = new Point(111, 56);
        cboSnapshotContent.Name = "cboSnapshotContent";
        cboSnapshotContent.Size = new Size(170, 23);
        cboSnapshotContent.TabIndex = 3;
        //
        // lblSnapshotFolder
        //
        lblSnapshotFolder.AutoSize = true;
        lblSnapshotFolder.Location = new Point(14, 92);
        lblSnapshotFolder.Name = "lblSnapshotFolder";
        lblSnapshotFolder.Size = new Size(40, 15);
        lblSnapshotFolder.TabIndex = 4;
        lblSnapshotFolder.Text = "Folder";
        //
        // txtSnapshotFolder
        //
        txtSnapshotFolder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtSnapshotFolder.Location = new Point(111, 88);
        txtSnapshotFolder.Name = "txtSnapshotFolder";
        txtSnapshotFolder.Size = new Size(358, 23);
        txtSnapshotFolder.TabIndex = 5;
        //
        // btnBrowseSnapshotFolder
        //
        btnBrowseSnapshotFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBrowseSnapshotFolder.Location = new Point(475, 87);
        btnBrowseSnapshotFolder.Name = "btnBrowseSnapshotFolder";
        btnBrowseSnapshotFolder.Size = new Size(36, 25);
        btnBrowseSnapshotFolder.TabIndex = 6;
        btnBrowseSnapshotFolder.Text = "...";
        btnBrowseSnapshotFolder.UseVisualStyleBackColor = true;
        btnBrowseSnapshotFolder.Click += btnBrowseSnapshotFolder_Click;
        //
        // btnCreateSnapshot
        //
        btnCreateSnapshot.BackColor = Color.FromArgb(33, 115, 70);
        btnCreateSnapshot.FlatAppearance.BorderSize = 0;
        btnCreateSnapshot.FlatStyle = FlatStyle.Flat;
        btnCreateSnapshot.ForeColor = Color.White;
        btnCreateSnapshot.Location = new Point(111, 123);
        btnCreateSnapshot.Name = "btnCreateSnapshot";
        btnCreateSnapshot.Size = new Size(170, 32);
        btnCreateSnapshot.TabIndex = 7;
        btnCreateSnapshot.Text = "Create Snapshot";
        btnCreateSnapshot.UseVisualStyleBackColor = false;
        btnCreateSnapshot.Click += btnCreateSnapshot_Click;
        //
        // lblSnapshotNote
        //
        lblSnapshotNote.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblSnapshotNote.ForeColor = Color.DimGray;
        lblSnapshotNote.Location = new Point(14, 163);
        lblSnapshotNote.Name = "lblSnapshotNote";
        lblSnapshotNote.Size = new Size(497, 18);
        lblSnapshotNote.TabIndex = 8;
        lblSnapshotNote.Text = "Structure is stored as an importable unmanaged solution package.";
        //
        // grpRestore
        //
        grpRestore.Controls.Add(lblSnapshotFile);
        grpRestore.Controls.Add(txtSnapshotFile);
        grpRestore.Controls.Add(btnBrowseSnapshotFile);
        grpRestore.Controls.Add(chkRestoreStructure);
        grpRestore.Controls.Add(chkRestoreData);
        grpRestore.Controls.Add(chkSafetySnapshot);
        grpRestore.Controls.Add(chkDeleteExtraRecords);
        grpRestore.Controls.Add(btnRestore);
        grpRestore.Dock = DockStyle.Fill;
        grpRestore.Location = new Point(0, 198);
        grpRestore.Margin = new Padding(0, 0, 0, 6);
        grpRestore.Name = "grpRestore";
        grpRestore.Padding = new Padding(10);
        grpRestore.Size = new Size(526, 188);
        grpRestore.TabIndex = 1;
        grpRestore.TabStop = false;
        grpRestore.Text = "Restore Snapshot";
        //
        // lblSnapshotFile
        //
        lblSnapshotFile.AutoSize = true;
        lblSnapshotFile.Location = new Point(14, 28);
        lblSnapshotFile.Name = "lblSnapshotFile";
        lblSnapshotFile.Size = new Size(27, 15);
        lblSnapshotFile.TabIndex = 0;
        lblSnapshotFile.Text = "File";
        //
        // txtSnapshotFile
        //
        txtSnapshotFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtSnapshotFile.Location = new Point(55, 24);
        txtSnapshotFile.Name = "txtSnapshotFile";
        txtSnapshotFile.Size = new Size(414, 23);
        txtSnapshotFile.TabIndex = 1;
        //
        // btnBrowseSnapshotFile
        //
        btnBrowseSnapshotFile.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBrowseSnapshotFile.Location = new Point(475, 23);
        btnBrowseSnapshotFile.Name = "btnBrowseSnapshotFile";
        btnBrowseSnapshotFile.Size = new Size(36, 25);
        btnBrowseSnapshotFile.TabIndex = 2;
        btnBrowseSnapshotFile.Text = "...";
        btnBrowseSnapshotFile.UseVisualStyleBackColor = true;
        btnBrowseSnapshotFile.Click += btnBrowseSnapshotFile_Click;
        //
        // chkRestoreStructure
        //
        chkRestoreStructure.AutoSize = true;
        chkRestoreStructure.Location = new Point(17, 59);
        chkRestoreStructure.Name = "chkRestoreStructure";
        chkRestoreStructure.Size = new Size(118, 19);
        chkRestoreStructure.TabIndex = 3;
        chkRestoreStructure.Text = "Restore structure";
        chkRestoreStructure.UseVisualStyleBackColor = true;
        //
        // chkRestoreData
        //
        chkRestoreData.AutoSize = true;
        chkRestoreData.Location = new Point(158, 59);
        chkRestoreData.Name = "chkRestoreData";
        chkRestoreData.Size = new Size(91, 19);
        chkRestoreData.TabIndex = 4;
        chkRestoreData.Text = "Restore data";
        chkRestoreData.UseVisualStyleBackColor = true;
        //
        // chkSafetySnapshot
        //
        chkSafetySnapshot.AutoSize = true;
        chkSafetySnapshot.Location = new Point(17, 86);
        chkSafetySnapshot.Name = "chkSafetySnapshot";
        chkSafetySnapshot.Size = new Size(212, 19);
        chkSafetySnapshot.TabIndex = 5;
        chkSafetySnapshot.Text = "Create a safety snapshot before restore";
        chkSafetySnapshot.UseVisualStyleBackColor = true;
        //
        // chkDeleteExtraRecords
        //
        chkDeleteExtraRecords.AutoSize = true;
        chkDeleteExtraRecords.ForeColor = Color.Firebrick;
        chkDeleteExtraRecords.Location = new Point(17, 113);
        chkDeleteExtraRecords.Name = "chkDeleteExtraRecords";
        chkDeleteExtraRecords.Size = new Size(244, 19);
        chkDeleteExtraRecords.TabIndex = 6;
        chkDeleteExtraRecords.Text = "Delete records not present in the snapshot";
        chkDeleteExtraRecords.UseVisualStyleBackColor = true;
        //
        // btnRestore
        //
        btnRestore.BackColor = Color.FromArgb(37, 99, 160);
        btnRestore.FlatAppearance.BorderSize = 0;
        btnRestore.FlatStyle = FlatStyle.Flat;
        btnRestore.ForeColor = Color.White;
        btnRestore.Location = new Point(17, 143);
        btnRestore.Name = "btnRestore";
        btnRestore.Size = new Size(170, 32);
        btnRestore.TabIndex = 7;
        btnRestore.Text = "Restore Snapshot";
        btnRestore.UseVisualStyleBackColor = false;
        btnRestore.Click += btnRestore_Click;
        //
        // grpDangerZone
        //
        grpDangerZone.Controls.Add(lblClearWarning);
        grpDangerZone.Controls.Add(btnClearTableData);
        grpDangerZone.Dock = DockStyle.Fill;
        grpDangerZone.ForeColor = Color.Firebrick;
        grpDangerZone.Location = new Point(0, 392);
        grpDangerZone.Margin = new Padding(0);
        grpDangerZone.Name = "grpDangerZone";
        grpDangerZone.Padding = new Padding(10);
        grpDangerZone.Size = new Size(526, 75);
        grpDangerZone.TabIndex = 2;
        grpDangerZone.TabStop = false;
        grpDangerZone.Text = "Danger Zone";
        //
        // lblClearWarning
        //
        lblClearWarning.AutoSize = true;
        lblClearWarning.ForeColor = Color.DimGray;
        lblClearWarning.Location = new Point(14, 25);
        lblClearWarning.Name = "lblClearWarning";
        lblClearWarning.Size = new Size(260, 15);
        lblClearWarning.TabIndex = 0;
        lblClearWarning.Text = "Select one custom table. Structure is never deleted.";
        //
        // btnClearTableData
        //
        btnClearTableData.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnClearTableData.BackColor = Color.Firebrick;
        btnClearTableData.FlatAppearance.BorderSize = 0;
        btnClearTableData.FlatStyle = FlatStyle.Flat;
        btnClearTableData.ForeColor = Color.White;
        btnClearTableData.Location = new Point(341, 25);
        btnClearTableData.Name = "btnClearTableData";
        btnClearTableData.Size = new Size(170, 32);
        btnClearTableData.TabIndex = 1;
        btnClearTableData.Text = "Clear Selected Table Data";
        btnClearTableData.UseVisualStyleBackColor = false;
        btnClearTableData.Click += btnClearTableData_Click;
        //
        // grpLog
        //
        grpLog.Controls.Add(txtLog);
        grpLog.Dock = DockStyle.Bottom;
        grpLog.Location = new Point(10, 647);
        grpLog.Name = "grpLog";
        grpLog.Padding = new Padding(8);
        grpLog.Size = new Size(1380, 170);
        grpLog.TabIndex = 3;
        grpLog.TabStop = false;
        grpLog.Text = "Operation Log";
        //
        // txtLog
        //
        txtLog.BackColor = Color.FromArgb(248, 249, 250);
        txtLog.Dock = DockStyle.Fill;
        txtLog.Font = new Font("Consolas", 9F);
        txtLog.Location = new Point(8, 24);
        txtLog.Multiline = true;
        txtLog.Name = "txtLog";
        txtLog.ReadOnly = true;
        txtLog.ScrollBars = ScrollBars.Both;
        txtLog.Size = new Size(1364, 138);
        txtLog.TabIndex = 0;
        txtLog.WordWrap = false;
        //
        // pnlStatus
        //
        pnlStatus.Controls.Add(lblSafetyNote);
        pnlStatus.Controls.Add(progressOperation);
        pnlStatus.Controls.Add(btnCancelOperation);
        pnlStatus.Dock = DockStyle.Bottom;
        pnlStatus.Location = new Point(10, 817);
        pnlStatus.Name = "pnlStatus";
        pnlStatus.Size = new Size(1380, 33);
        pnlStatus.TabIndex = 4;
        //
        // progressOperation
        //
        progressOperation.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        progressOperation.Location = new Point(1081, 7);
        progressOperation.Name = "progressOperation";
        progressOperation.Size = new Size(190, 19);
        progressOperation.TabIndex = 1;
        //
        // btnCancelOperation
        //
        btnCancelOperation.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnCancelOperation.Enabled = false;
        btnCancelOperation.Location = new Point(1277, 3);
        btnCancelOperation.Name = "btnCancelOperation";
        btnCancelOperation.Size = new Size(95, 27);
        btnCancelOperation.TabIndex = 2;
        btnCancelOperation.Text = "Cancel";
        btnCancelOperation.UseVisualStyleBackColor = true;
        btnCancelOperation.Click += btnCancelOperation_Click;
        //
        // lblSafetyNote
        //
        lblSafetyNote.AutoSize = true;
        lblSafetyNote.ForeColor = Color.DimGray;
        lblSafetyNote.Location = new Point(2, 9);
        lblSafetyNote.Name = "lblSafetyNote";
        lblSafetyNote.Size = new Size(488, 15);
        lblSafetyNote.TabIndex = 0;
        lblSafetyNote.Text = "Mutating operations are restricted to unmanaged custom tables. Cross-environment restore is blocked.";
        //
        // frmMain
        //
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(244, 246, 248);
        ClientSize = new Size(1400, 860);
        Controls.Add(splitMain);
        Controls.Add(grpLog);
        Controls.Add(pnlStatus);
        Controls.Add(grpConnection);
        Controls.Add(pnlHeader);
        Font = new Font("Segoe UI", 9F);
        MinimumSize = new Size(1120, 760);
        Name = "frmMain";
        Padding = new Padding(10, 10, 10, 10);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Dataverse Snapshot Manager";
        pnlHeader.ResumeLayout(false);
        pnlHeader.PerformLayout();
        grpConnection.ResumeLayout(false);
        grpConnection.PerformLayout();
        splitMain.Panel1.ResumeLayout(false);
        splitMain.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
        splitMain.ResumeLayout(false);
        grpTables.ResumeLayout(false);
        pnlTableToolbar.ResumeLayout(false);
        pnlTableToolbar.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)dgvTables).EndInit();
        pnlActions.ResumeLayout(false);
        grpCreateSnapshot.ResumeLayout(false);
        grpCreateSnapshot.PerformLayout();
        grpRestore.ResumeLayout(false);
        grpRestore.PerformLayout();
        grpDangerZone.ResumeLayout(false);
        grpDangerZone.PerformLayout();
        grpLog.ResumeLayout(false);
        grpLog.PerformLayout();
        pnlStatus.ResumeLayout(false);
        pnlStatus.PerformLayout();
        ResumeLayout(false);
    }
}
