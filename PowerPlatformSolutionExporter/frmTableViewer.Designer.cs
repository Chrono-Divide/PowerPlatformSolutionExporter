#nullable disable

namespace PowerPlatformSolutionExporter;

partial class frmTableViewer
{
    private System.ComponentModel.IContainer components = null;
    private Panel pnlHeader;
    private Label lblTableTitle;
    private Label lblTableIdentity;
    private TabControl tabViewer;
    private TabPage tabColumns;
    private TabPage tabData;
    private DataGridView dgvColumns;
    private Label lblColumnSummary;
    private Panel pnlDataToolbar;
    private Label lblPageSize;
    private NumericUpDown numPageSize;
    private Button btnRefreshData;
    private Button btnPreviousPage;
    private Button btnNextPage;
    private Label lblDataStatus;
    private ProgressBar progressData;
    private DataGridView dgvData;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        pnlHeader = new Panel();
        lblTableTitle = new Label();
        lblTableIdentity = new Label();
        tabViewer = new TabControl();
        tabColumns = new TabPage();
        dgvColumns = new DataGridView();
        lblColumnSummary = new Label();
        tabData = new TabPage();
        dgvData = new DataGridView();
        pnlDataToolbar = new Panel();
        lblPageSize = new Label();
        numPageSize = new NumericUpDown();
        btnRefreshData = new Button();
        btnPreviousPage = new Button();
        btnNextPage = new Button();
        lblDataStatus = new Label();
        progressData = new ProgressBar();
        pnlHeader.SuspendLayout();
        tabViewer.SuspendLayout();
        tabColumns.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvColumns).BeginInit();
        tabData.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)dgvData).BeginInit();
        pnlDataToolbar.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numPageSize).BeginInit();
        SuspendLayout();
        // header
        pnlHeader.BackColor = Color.FromArgb(28, 46, 64);
        pnlHeader.Controls.Add(lblTableTitle);
        pnlHeader.Controls.Add(lblTableIdentity);
        pnlHeader.Dock = DockStyle.Top;
        pnlHeader.Height = 74;
        lblTableTitle.AutoSize = true;
        lblTableTitle.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        lblTableTitle.ForeColor = Color.White;
        lblTableTitle.Location = new Point(18, 9);
        lblTableIdentity.AutoSize = true;
        lblTableIdentity.ForeColor = Color.WhiteSmoke;
        lblTableIdentity.Location = new Point(20, 47);
        // tabs
        tabViewer.Controls.Add(tabColumns);
        tabViewer.Controls.Add(tabData);
        tabViewer.Dock = DockStyle.Fill;
        tabViewer.Name = "tabViewer";
        // columns tab
        tabColumns.Controls.Add(dgvColumns);
        tabColumns.Controls.Add(lblColumnSummary);
        tabColumns.Text = "Columns and data types";
        tabColumns.Padding = new Padding(6);
        dgvColumns.AllowUserToAddRows = false;
        dgvColumns.AllowUserToDeleteRows = false;
        dgvColumns.AllowUserToOrderColumns = true;
        dgvColumns.AllowUserToResizeRows = false;
        dgvColumns.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        dgvColumns.BackgroundColor = Color.White;
        dgvColumns.Dock = DockStyle.Fill;
        dgvColumns.Name = "dgvColumns";
        dgvColumns.ReadOnly = true;
        dgvColumns.RowHeadersVisible = false;
        dgvColumns.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvColumns.Columns.Add("DisplayName", "Display name");
        dgvColumns.Columns.Add("LogicalName", "Logical name");
        dgvColumns.Columns.Add("SchemaName", "Schema name");
        dgvColumns.Columns.Add("Origin", "Origin");
        dgvColumns.Columns.Add("DataType", "Data type");
        dgvColumns.Columns.Add("Details", "Type details");
        dgvColumns.Columns.Add("Required", "Required");
        dgvColumns.Columns.Add("PrimaryId", "Primary ID");
        dgvColumns.Columns.Add("PrimaryName", "Primary name");
        dgvColumns.Columns.Add("Read", "Read");
        dgvColumns.Columns.Add("Create", "Create");
        dgvColumns.Columns.Add("Update", "Update");
        foreach (DataGridViewColumn column in dgvColumns.Columns)
        {
            column.SortMode = DataGridViewColumnSortMode.Programmatic;
        }
        dgvColumns.ColumnHeaderMouseClick += dgvColumns_ColumnHeaderMouseClick;
        lblColumnSummary.Dock = DockStyle.Bottom;
        lblColumnSummary.Height = 28;
        lblColumnSummary.Padding = new Padding(4, 6, 0, 0);
        lblColumnSummary.ForeColor = Color.DimGray;
        lblColumnSummary.Name = "lblColumnSummary";
        // data tab
        tabData.Controls.Add(dgvData);
        tabData.Controls.Add(pnlDataToolbar);
        tabData.Text = "Record data (read-only)";
        tabData.Padding = new Padding(6);
        dgvData.AllowUserToAddRows = false;
        dgvData.AllowUserToDeleteRows = false;
        dgvData.AllowUserToOrderColumns = true;
        dgvData.AllowUserToResizeRows = false;
        dgvData.BackgroundColor = Color.White;
        dgvData.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
        dgvData.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        dgvData.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvData.Dock = DockStyle.Fill;
        dgvData.EnableHeadersVisualStyles = false;
        dgvData.Name = "dgvData";
        dgvData.ReadOnly = true;
        dgvData.RowHeadersVisible = false;
        dgvData.SelectionMode = DataGridViewSelectionMode.CellSelect;
        dgvData.ShowCellToolTips = true;
        dgvData.ColumnHeaderMouseClick += dgvData_ColumnHeaderMouseClick;
        // toolbar
        pnlDataToolbar.Controls.Add(lblPageSize);
        pnlDataToolbar.Controls.Add(numPageSize);
        pnlDataToolbar.Controls.Add(btnRefreshData);
        pnlDataToolbar.Controls.Add(btnPreviousPage);
        pnlDataToolbar.Controls.Add(btnNextPage);
        pnlDataToolbar.Controls.Add(lblDataStatus);
        pnlDataToolbar.Controls.Add(progressData);
        pnlDataToolbar.Dock = DockStyle.Top;
        pnlDataToolbar.Height = 46;
        lblPageSize.AutoSize = true;
        lblPageSize.Location = new Point(4, 15);
        lblPageSize.Text = "Page size";
        numPageSize.Location = new Point(70, 11);
        numPageSize.Minimum = 10;
        numPageSize.Maximum = 500;
        numPageSize.Name = "numPageSize";
        numPageSize.Value = 100;
        numPageSize.Width = 70;
        numPageSize.ValueChanged += numPageSize_ValueChanged;
        btnRefreshData.Location = new Point(151, 9);
        btnRefreshData.Size = new Size(82, 27);
        btnRefreshData.Name = "btnRefreshData";
        btnRefreshData.Text = "Refresh";
        btnRefreshData.Click += btnRefreshData_Click;
        btnPreviousPage.Location = new Point(242, 9);
        btnPreviousPage.Size = new Size(88, 27);
        btnPreviousPage.Name = "btnPreviousPage";
        btnPreviousPage.Text = "Previous";
        btnPreviousPage.Click += btnPreviousPage_Click;
        btnNextPage.Location = new Point(337, 9);
        btnNextPage.Size = new Size(75, 27);
        btnNextPage.Name = "btnNextPage";
        btnNextPage.Text = "Next";
        btnNextPage.Click += btnNextPage_Click;
        lblDataStatus.AutoSize = true;
        lblDataStatus.Location = new Point(425, 15);
        lblDataStatus.Name = "lblDataStatus";
        lblDataStatus.Text = "Not loaded";
        progressData.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        progressData.Location = new Point(1120, 13);
        progressData.MarqueeAnimationSpeed = 25;
        progressData.Size = new Size(140, 20);
        progressData.Style = ProgressBarStyle.Marquee;
        progressData.Visible = false;
        // form
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1280, 780);
        Controls.Add(tabViewer);
        Controls.Add(pnlHeader);
        MinimumSize = new Size(950, 600);
        Name = "frmTableViewer";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Table Inspector";
        Shown += frmTableViewer_Shown;
        pnlHeader.ResumeLayout(false);
        pnlHeader.PerformLayout();
        tabViewer.ResumeLayout(false);
        tabColumns.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dgvColumns).EndInit();
        tabData.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)dgvData).EndInit();
        pnlDataToolbar.ResumeLayout(false);
        pnlDataToolbar.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numPageSize).EndInit();
        ResumeLayout(false);
    }
}
