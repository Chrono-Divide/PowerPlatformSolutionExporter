using PowerPlatformSolutionExporter.Models;
using PowerPlatformSolutionExporter.Services;

namespace PowerPlatformSolutionExporter;

public partial class frmTableViewer : Form
{
    private static readonly Color GeneratedColumnBackColor = Color.FromArgb(255, 247, 218);
    private static readonly Color GeneratedColumnHeaderBackColor = Color.FromArgb(246, 225, 163);
    private static readonly Color GeneratedColumnSelectionBackColor = Color.FromArgb(240, 213, 138);
    private static readonly Color BuiltInColumnBackColor = Color.FromArgb(235, 242, 248);
    private static readonly Color BuiltInColumnHeaderBackColor = Color.FromArgb(211, 225, 237);
    private static readonly Color BuiltInColumnSelectionBackColor = Color.FromArgb(190, 214, 233);
    private const int StandardDataColumnMinimumWidth = 180;
    private const int PrimaryIdColumnMinimumWidth = 300;
    private const int MaximumDataColumnWidth = 480;

    private readonly DataverseService _dataverseService;
    private readonly DataverseTableInfo _table;
    private readonly IReadOnlyList<TableColumnInfo> _tableColumns;
    private readonly List<string?> _pageStartCookies = [null];
    private CancellationTokenSource? _loadCancellation;
    private int? _metadataSortColumnIndex;
    private bool _metadataSortAscending = true;
    private string _dataSortLogicalName;
    private bool _dataSortAscending = true;
    private int _currentPage = 1;
    private bool _busy;

    public frmTableViewer(DataverseService dataverseService, DataverseTableInfo table)
    {
        _dataverseService = dataverseService;
        _table = table;
        _tableColumns = _dataverseService.GetTableColumns(_table);
        _dataSortLogicalName = table.PrimaryIdAttribute;
        InitializeComponent();
        Text = $"{table.DisplayName} - Table Inspector";
        lblTableTitle.Text = table.DisplayName;
        lblTableIdentity.Text = $"{table.LogicalName}  |  {table.SchemaName}  |  Primary ID: {table.PrimaryIdAttribute}";
        PopulateColumns();
    }

    private void PopulateColumns()
    {
        dgvColumns.Rows.Clear();
        var columns = GetMetadataDisplayColumns();
        foreach (var column in columns)
        {
            var rowIndex = dgvColumns.Rows.Add(
                column.DisplayName,
                column.LogicalName,
                column.SchemaName,
                column.OriginDisplayName,
                column.DataType,
                column.Details,
                column.RequiredLevel,
                column.IsPrimaryId ? "Yes" : string.Empty,
                column.IsPrimaryName ? "Yes" : string.Empty,
                column.IsValidForRead ? "Yes" : "No",
                column.IsValidForCreate ? "Yes" : "No",
                column.IsValidForUpdate ? "Yes" : "No");
            var row = dgvColumns.Rows[rowIndex];
            row.Tag = column;
            ApplyOriginStyle(row.DefaultCellStyle, column.Origin);
        }

        var customCount = _tableColumns.Count(column => column.Origin == DataverseColumnOrigin.Custom);
        var generatedCount = _tableColumns.Count(column => column.Origin == DataverseColumnOrigin.DataverseGenerated);
        var systemCount = _tableColumns.Count(column => column.Origin == DataverseColumnOrigin.DataverseSystem);
        lblColumnSummary.Text = $"{_tableColumns.Count:N0} columns  |  {customCount:N0} custom  |  " +
                                $"{generatedCount:N0} generated (yellow)  |  {systemCount:N0} system (blue)  |  Click a header to sort";
        ResizeMetadataColumns();
        UpdateMetadataSortGlyph();
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
                _loadCancellation.Token,
                _dataSortLogicalName,
                _dataSortAscending);
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
                var headerText = $"{column.DisplayName}\n{column.LogicalName} · {column.DataType}";
                var tooltipText = $"{headerText}\nOrigin: {column.OriginDisplayName}";
                var dataColumn = new DataGridViewTextBoxColumn
                {
                    Name = column.LogicalName,
                    HeaderText = headerText,
                    MinimumWidth = column.IsPrimaryId ? PrimaryIdColumnMinimumWidth : StandardDataColumnMinimumWidth,
                    Width = CalculateDataColumnWidth(column),
                    SortMode = DataGridViewColumnSortMode.Programmatic,
                    Tag = column,
                    ToolTipText = tooltipText,
                };
                dataColumn.HeaderCell.ToolTipText = tooltipText;
                ApplyOriginStyle(dataColumn.DefaultCellStyle, column.Origin);
                var headerBackColor = GetOriginHeaderBackColor(column.Origin);
                if (headerBackColor.HasValue)
                {
                    dataColumn.HeaderCell.Style.BackColor = headerBackColor.Value;
                    dataColumn.HeaderCell.Style.ForeColor = Color.Black;
                }

                dgvData.Columns.Add(dataColumn);
            }

            foreach (var row in page.Rows)
            {
                dgvData.Rows.Add(page.Columns
                    .Select(column => row.TryGetValue(column.LogicalName, out var value) ? value ?? string.Empty : string.Empty)
                    .Cast<object>()
                    .ToArray());
            }

            UpdateDataSortGlyph();
            dgvData.AutoResizeColumnHeadersHeight();
        }
        finally
        {
            dgvData.ResumeLayout();
        }
    }

    private IReadOnlyList<TableColumnInfo> GetMetadataDisplayColumns()
    {
        if (!_metadataSortColumnIndex.HasValue)
        {
            return _tableColumns;
        }

        var primaryIdColumns = _tableColumns.Where(column => column.IsPrimaryId);
        var remainingColumns = _tableColumns.Where(column => !column.IsPrimaryId);
        Func<TableColumnInfo, string> selector = column => GetMetadataSortValue(
            column,
            _metadataSortColumnIndex.Value);
        remainingColumns = _metadataSortAscending
            ? remainingColumns.OrderBy(selector, StringComparer.CurrentCultureIgnoreCase)
            : remainingColumns.OrderByDescending(selector, StringComparer.CurrentCultureIgnoreCase);
        return primaryIdColumns
            .Concat(remainingColumns)
            .ToList();
    }

    private static string GetMetadataSortValue(TableColumnInfo column, int columnIndex)
    {
        return columnIndex switch
        {
            0 => column.DisplayName,
            1 => column.LogicalName,
            2 => column.SchemaName,
            3 => column.OriginDisplayName,
            4 => column.DataType,
            5 => column.Details,
            6 => column.RequiredLevel,
            7 => column.IsPrimaryId ? "Yes" : string.Empty,
            8 => column.IsPrimaryName ? "Yes" : string.Empty,
            9 => column.IsValidForRead ? "Yes" : "No",
            10 => column.IsValidForCreate ? "Yes" : "No",
            11 => column.IsValidForUpdate ? "Yes" : "No",
            _ => column.DisplayName,
        };
    }

    private void dgvColumns_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || e.ColumnIndex < 0)
        {
            return;
        }

        if (_metadataSortColumnIndex == e.ColumnIndex)
        {
            _metadataSortAscending = !_metadataSortAscending;
        }
        else
        {
            _metadataSortColumnIndex = e.ColumnIndex;
            _metadataSortAscending = true;
        }

        PopulateColumns();
    }

    private async void dgvData_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (_busy || e.Button != MouseButtons.Left || e.ColumnIndex < 0 ||
            dgvData.Columns[e.ColumnIndex].Tag is not TableColumnInfo column)
        {
            return;
        }

        ToggleDataSort(column);
        await LoadPageAsync(1, resetPaging: true);
    }

    private void ToggleDataSort(TableColumnInfo column)
    {
        if (string.Equals(_dataSortLogicalName, column.LogicalName, StringComparison.OrdinalIgnoreCase))
        {
            _dataSortAscending = !_dataSortAscending;
        }
        else
        {
            _dataSortLogicalName = column.LogicalName;
            _dataSortAscending = true;
        }
    }

    private void UpdateMetadataSortGlyph()
    {
        foreach (DataGridViewColumn column in dgvColumns.Columns)
        {
            column.HeaderCell.SortGlyphDirection = SortOrder.None;
        }

        if (_metadataSortColumnIndex.HasValue)
        {
            dgvColumns.Columns[_metadataSortColumnIndex.Value].HeaderCell.SortGlyphDirection =
                _metadataSortAscending ? SortOrder.Ascending : SortOrder.Descending;
        }
    }

    private void UpdateDataSortGlyph()
    {
        foreach (DataGridViewColumn column in dgvData.Columns)
        {
            column.HeaderCell.SortGlyphDirection = string.Equals(
                column.Name,
                _dataSortLogicalName,
                StringComparison.OrdinalIgnoreCase)
                ? (_dataSortAscending ? SortOrder.Ascending : SortOrder.Descending)
                : SortOrder.None;
        }
    }

    private int CalculateDataColumnWidth(TableColumnInfo column)
    {
        var headerFont = dgvData.ColumnHeadersDefaultCellStyle.Font ?? dgvData.Font;
        var displayNameWidth = TextRenderer.MeasureText(column.DisplayName, headerFont).Width;
        var metadataWidth = TextRenderer.MeasureText($"{column.LogicalName} · {column.DataType}", headerFont).Width;
        var minimumWidth = column.IsPrimaryId ? PrimaryIdColumnMinimumWidth : StandardDataColumnMinimumWidth;
        return Math.Clamp(Math.Max(displayNameWidth, metadataWidth) + 30, minimumWidth, MaximumDataColumnWidth);
    }

    private void ResizeMetadataColumns()
    {
        foreach (DataGridViewColumn column in dgvColumns.Columns)
        {
            var minimumWidth = column.Name switch
            {
                "DisplayName" or "LogicalName" or "SchemaName" => 180,
                "Origin" => 150,
                "DataType" or "Details" => 140,
                _ => 80,
            };
            var maximumWidth = column.Name switch
            {
                "DisplayName" or "LogicalName" or "SchemaName" => MaximumDataColumnWidth,
                "Origin" => 190,
                "Details" => 400,
                "DataType" => 240,
                _ => 140,
            };
            var headerFont = dgvColumns.ColumnHeadersDefaultCellStyle.Font ?? dgvColumns.Font;
            var cellFont = column.DefaultCellStyle.Font ?? dgvColumns.DefaultCellStyle.Font ?? dgvColumns.Font;
            var contentWidth = TextRenderer.MeasureText(column.HeaderText, headerFont).Width;
            foreach (DataGridViewRow row in dgvColumns.Rows)
            {
                var value = row.Cells[column.Index].Value?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    contentWidth = Math.Max(contentWidth, TextRenderer.MeasureText(value, cellFont).Width);
                }
            }

            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = Math.Clamp(contentWidth + 30, minimumWidth, maximumWidth);
        }

        foreach (DataGridViewRow row in dgvColumns.Rows)
        {
            foreach (DataGridViewCell cell in row.Cells)
            {
                cell.ToolTipText = cell.Value?.ToString() ?? string.Empty;
            }
        }
    }

    private static void ApplyOriginStyle(DataGridViewCellStyle style, DataverseColumnOrigin origin)
    {
        var colors = origin switch
        {
            DataverseColumnOrigin.DataverseGenerated =>
                (GeneratedColumnBackColor, GeneratedColumnSelectionBackColor),
            DataverseColumnOrigin.DataverseSystem =>
                (BuiltInColumnBackColor, BuiltInColumnSelectionBackColor),
            _ => ((Color BackColor, Color SelectionBackColor)?)null,
        };
        if (!colors.HasValue)
        {
            return;
        }

        style.BackColor = colors.Value.BackColor;
        style.ForeColor = Color.Black;
        style.SelectionBackColor = colors.Value.SelectionBackColor;
        style.SelectionForeColor = Color.Black;
    }

    private static Color? GetOriginHeaderBackColor(DataverseColumnOrigin origin)
    {
        return origin switch
        {
            DataverseColumnOrigin.DataverseGenerated => GeneratedColumnHeaderBackColor,
            DataverseColumnOrigin.DataverseSystem => BuiltInColumnHeaderBackColor,
            _ => null,
        };
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
