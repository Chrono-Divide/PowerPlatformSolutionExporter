using PowerPlatformSolutionExporter.Models;
using PowerPlatformSolutionExporter.Services;

namespace PowerPlatformSolutionExporter;

public partial class frmTableViewer : Form
{
    private readonly DataverseService _dataverseService;
    private readonly DataverseTableInfo _table;
    private readonly List<string?> _pageStartCookies = [null];
    private CancellationTokenSource? _loadCancellation;
    private int _currentPage = 1;
    private bool _busy;

    public frmTableViewer(DataverseService dataverseService, DataverseTableInfo table)
    {
        _dataverseService = dataverseService;
        _table = table;
        InitializeComponent();
        Text = $"{table.DisplayName} - Table Inspector";
        lblTableTitle.Text = table.DisplayName;
        lblTableIdentity.Text = $"{table.LogicalName}  |  {table.SchemaName}  |  Primary ID: {table.PrimaryIdAttribute}";
        PopulateColumns();
    }

    private void PopulateColumns()
    {
        dgvColumns.Rows.Clear();
        foreach (var column in _dataverseService.GetTableColumns(_table))
        {
            dgvColumns.Rows.Add(
                column.DisplayName,
                column.LogicalName,
                column.SchemaName,
                column.DataType,
                column.Details,
                column.RequiredLevel,
                column.IsPrimaryId ? "Yes" : string.Empty,
                column.IsPrimaryName ? "Yes" : string.Empty,
                column.IsValidForRead ? "Yes" : "No",
                column.IsValidForCreate ? "Yes" : "No",
                column.IsValidForUpdate ? "Yes" : "No");
        }

        lblColumnSummary.Text = $"{dgvColumns.Rows.Count:N0} columns";
    }

    private async void frmTableViewer_Shown(object? sender, EventArgs e)
    {
        await LoadPageAsync(1, resetPaging: true);
    }

    private async void btnRefreshData_Click(object? sender, EventArgs e)
    {
        await LoadPageAsync(1, resetPaging: true);
    }

    private async void btnPreviousPage_Click(object? sender, EventArgs e)
    {
        if (_currentPage > 1)
        {
            await LoadPageAsync(_currentPage - 1, resetPaging: false);
        }
    }

    private async void btnNextPage_Click(object? sender, EventArgs e)
    {
        if (_pageStartCookies.Count > _currentPage)
        {
            await LoadPageAsync(_currentPage + 1, resetPaging: false);
        }
    }

    private void numPageSize_ValueChanged(object? sender, EventArgs e)
    {
        if (_busy)
        {
            return;
        }

        _pageStartCookies.Clear();
        _pageStartCookies.Add(null);
        _currentPage = 1;
        dgvData.Rows.Clear();
        lblDataStatus.Text = "Page size changed. Click Refresh to load page 1.";
        UpdateDataControls();
    }

    private async Task LoadPageAsync(int pageNumber, bool resetPaging)
    {
        if (_busy)
        {
            return;
        }

        if (resetPaging)
        {
            _pageStartCookies.Clear();
            _pageStartCookies.Add(null);
            pageNumber = 1;
        }

        if (pageNumber < 1 || pageNumber > _pageStartCookies.Count)
        {
            return;
        }

        _busy = true;
        _loadCancellation = new CancellationTokenSource();
        UpdateDataControls();
        lblDataStatus.Text = $"Loading page {pageNumber:N0}...";
        try
        {
            var page = await _dataverseService.RetrieveTableDataPageAsync(
                _table,
                pageNumber,
                Decimal.ToInt32(numPageSize.Value),
                _pageStartCookies[pageNumber - 1],
                _loadCancellation.Token);
            _currentPage = page.PageNumber;
            if (page.HasMoreRecords && !string.IsNullOrWhiteSpace(page.NextPagingCookie))
            {
                if (_pageStartCookies.Count == _currentPage)
                {
                    _pageStartCookies.Add(page.NextPagingCookie);
                }
                else
                {
                    _pageStartCookies[_currentPage] = page.NextPagingCookie;
                }
            }
            else if (_pageStartCookies.Count > _currentPage)
            {
                _pageStartCookies.RemoveRange(_currentPage, _pageStartCookies.Count - _currentPage);
            }

            PopulateData(page);
            lblDataStatus.Text = $"Page {_currentPage:N0}  |  {page.Rows.Count:N0} records" +
                                 (page.HasMoreRecords ? "  |  More records available" : "  |  Last page");
        }
        catch (OperationCanceledException)
        {
            lblDataStatus.Text = "Data loading cancelled.";
        }
        catch (Exception exception)
        {
            lblDataStatus.Text = "Unable to load data.";
            MessageBox.Show(this, exception.Message, "Table Data Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _loadCancellation.Dispose();
            _loadCancellation = null;
            _busy = false;
            UpdateDataControls();
        }
    }

    private void PopulateData(TableDataPage page)
    {
        dgvData.SuspendLayout();
        try
        {
            dgvData.Rows.Clear();
            dgvData.Columns.Clear();
            foreach (var column in page.Columns)
            {
                dgvData.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = column.LogicalName,
                    HeaderText = $"{column.DisplayName}\n{column.LogicalName} · {column.DataType}",
                    MinimumWidth = 120,
                    Width = column.IsPrimaryId ? 250 : 190,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                });
            }

            foreach (var row in page.Rows)
            {
                dgvData.Rows.Add(page.Columns
                    .Select(column => row.TryGetValue(column.LogicalName, out var value) ? value ?? string.Empty : string.Empty)
                    .Cast<object>()
                    .ToArray());
            }
        }
        finally
        {
            dgvData.ResumeLayout();
        }
    }

    private void UpdateDataControls()
    {
        btnRefreshData.Enabled = !_busy;
        btnPreviousPage.Enabled = !_busy && _currentPage > 1;
        btnNextPage.Enabled = !_busy && _pageStartCookies.Count > _currentPage;
        numPageSize.Enabled = !_busy;
        progressData.Visible = _busy;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _loadCancellation?.Cancel();
        base.OnFormClosing(e);
    }
}
