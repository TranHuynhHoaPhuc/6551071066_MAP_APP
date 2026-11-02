using ClosedXML.Excel;

namespace HotelManagement.UI;

public enum EditFieldType
{
    Text,
    Integer,
    Decimal,
    Choice,
    Date
}

public sealed record EditField(
    string Name,
    string Caption,
    EditFieldType Type = EditFieldType.Text,
    int MaxLength = 100,
    bool Required = true,
    string[]? Choices = null,
    bool ReadOnly = false);

public sealed record GridField(string PropertyName, string Caption, float FillWeight = 15, string? Format = null);

public abstract class EntityGridForm<TRow> : Form where TRow : class
{
    private readonly string _sheetName;
    private readonly IReadOnlyList<GridField> _fields;
    private readonly Func<string, List<TRow>> _load;
    private readonly Func<TRow, string> _key;
    private readonly Func<string?, bool> _edit;
    private readonly Action<string> _delete;
    private readonly string _addCaption;
    private readonly string _editCaption;
    private readonly string _deleteCaption;
    private readonly TextBox _search = new() { MaxLength = 100, Dock = DockStyle.Fill };
    private readonly DataGridView _grid = new();

    protected EntityGridForm(
        string title,
        string sheetName,
        IReadOnlyList<GridField> fields,
        Func<string, List<TRow>> load,
        Func<TRow, string> key,
        Func<string?, bool> edit,
        Action<string> delete,
        string addCaption = "Thêm mới",
        string editCaption = "Cập nhật",
        string deleteCaption = "Xóa")
    {
        Text = title;
        _sheetName = sheetName;
        _fields = fields;
        _load = load;
        _key = key;
        _edit = edit;
        _delete = delete;
        _addCaption = addCaption;
        _editCaption = editCaption;
        _deleteCaption = deleteCaption;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        BackColor = SystemColors.Control;
        BuildLayout();
        Load += (_, _) => ReloadRows();
        _search.TextChanged += (_, _) => ReloadRows();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(10, 9, 10, 8),
            Margin = Padding.Empty
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));

        var searchIcon = new PictureBox
        {
            Image = ResourceImages.FromBytes(Properties.Resources.search),
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.Fixed3D,
            Margin = new Padding(0, 2, 5, 3),
            TabStop = false
        };
        _search.Font = new Font("Microsoft Sans Serif", 10F);
        _search.Margin = new Padding(2, 2, 10, 3);
        toolbar.Controls.Add(searchIcon, 0, 0);
        toolbar.Controls.Add(_search, 1, 0);

        var export = ToolbarButton("Xuất file Excel", Color.DarkTurquoise);
        export.Click += (_, _) => ExportToExcel();
        toolbar.Controls.Add(export, 2, 0);
        var add = ToolbarButton(_addCaption, Color.LightPink);
        add.Click += (_, _) => OpenEditor(null);
        toolbar.Controls.Add(add, 3, 0);
        root.Controls.Add(toolbar, 0, 0);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 1);
    }

    private static Button ToolbarButton(string text, Color color) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        BackColor = color,
        Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold),
        Margin = new Padding(4, 0, 4, 0),
        UseVisualStyleBackColor = false
    };

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.BackgroundColor = Color.DarkGray;
        _grid.BorderStyle = BorderStyle.Fixed3D;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        _grid.GridColor = SystemColors.ControlDark;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersVisible = false;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.DefaultCellStyle.Font = new Font("Microsoft Sans Serif", 9F);
        _grid.RowTemplate.Height = 36;
        _grid.AutoGenerateColumns = false;

        foreach (var field in _fields)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = field.PropertyName,
                DataPropertyName = field.PropertyName,
                HeaderText = field.Caption,
                FillWeight = field.FillWeight,
                ReadOnly = true,
                DefaultCellStyle = { Format = field.Format ?? string.Empty }
            });
        }
        _grid.Columns.Add(ActionColumn("colSua", _editCaption));
        _grid.Columns.Add(ActionColumn("colXoa", _deleteCaption));
        _grid.CellClick += Grid_CellClick;
    }

    private static DataGridViewButtonColumn ActionColumn(string name, string caption) => new()
    {
        Name = name,
        HeaderText = caption,
        Text = caption,
        UseColumnTextForButtonValue = true,
        AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
        Width = 88,
        MinimumWidth = 76,
        FlatStyle = FlatStyle.Standard,
        ReadOnly = true,
        SortMode = DataGridViewColumnSortMode.NotSortable
    };

    private void ReloadRows()
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        try
        {
            _grid.DataSource = _load(_search.Text.Trim());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được danh sách.\n{ex.Message}", "Lỗi CSDL",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grid.Rows[e.RowIndex].DataBoundItem is not TRow row)
            return;

        var key = _key(row);
        switch (_grid.Columns[e.ColumnIndex].Name)
        {
            case "colSua":
                OpenEditor(key);
                break;
            case "colXoa":
                if (MessageBox.Show($"Bạn có chắc muốn xóa {key}?", "Xác nhận xóa",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        _delete(key);
                        ReloadRows();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Không xóa được {key}.\n{ex.Message}", "Lỗi CSDL",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                break;
        }
    }

    private void OpenEditor(string? key)
    {
        try
        {
            if (_edit(key))
                ReloadRows();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không mở được thông tin.\n{ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportToExcel()
    {
        if (_grid.Rows.Count == 0)
        {
            MessageBox.Show("Không có dữ liệu để xuất.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = $"Xuất {_sheetName}",
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = $"{_sheetName.Replace(' ', '_')}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        try
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(_sheetName.Length > 31 ? _sheetName[..31] : _sheetName);
            var visibleColumns = _fields
                .Select(field => _grid.Columns[field.PropertyName] ?? throw new InvalidOperationException($"Không tìm thấy cột {field.PropertyName}."))
                .ToList();
            for (int i = 0; i < visibleColumns.Count; i++)
            {
                sheet.Cell(1, i + 1).Value = visibleColumns[i].HeaderText;
                sheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

            var excelRow = 2;
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (!row.Visible || row.IsNewRow)
                    continue;
                for (int i = 0; i < visibleColumns.Count; i++)
                    sheet.Cell(excelRow, i + 1).Value = row.Cells[visibleColumns[i].Index].FormattedValue?.ToString() ?? string.Empty;
                excelRow++;
            }
            sheet.Columns().AdjustToContents();
            workbook.SaveAs(dialog.FileName);
            MessageBox.Show("Xuất Excel thành công.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không xuất được Excel.\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

public sealed class EntityEditDialog : Form
{
    private readonly IReadOnlyList<EditField> _fields;
    private readonly IReadOnlyDictionary<string, string> _initialValues;
    private readonly Func<IReadOnlyDictionary<string, string>, bool> _save;
    private readonly Dictionary<string, Control> _inputs = new(StringComparer.Ordinal);

    public EntityEditDialog(
        string title,
        IReadOnlyList<EditField> fields,
        IReadOnlyDictionary<string, string> initialValues,
        Func<IReadOnlyDictionary<string, string>, bool> save)
    {
        Text = title;
        _fields = fields;
        _initialValues = initialValues;
        _save = save;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Font = new Font("Microsoft Sans Serif", 10F);
        BackColor = SystemColors.Control;
        BuildLayout();
    }

    private void BuildLayout()
    {
        var bodyHeight = Math.Min(390, Math.Max(150, _fields.Count * 45 + 12));
        ClientSize = new Size(560, bodyHeight + 82);
        var body = new Panel
        {
            Location = new Point(14, 12),
            Size = new Size(532, bodyHeight),
            AutoScroll = true
        };
        Controls.Add(body);

        var y = 8;
        foreach (var field in _fields)
        {
            var label = new Label
            {
                Text = field.Caption,
                Location = new Point(4, y + 4),
                Size = new Size(174, 27),
                TextAlign = ContentAlignment.MiddleRight
            };
            var input = CreateInput(field);
            input.Location = new Point(190, y);
            input.Size = new Size(315, 29);
            if (input is TextBox textBox)
            {
                textBox.MaxLength = field.MaxLength;
                textBox.ReadOnly = field.ReadOnly;
            }
            if (input is ComboBox comboBox)
                comboBox.Enabled = !field.ReadOnly;
            if (_initialValues.TryGetValue(field.Name, out var value))
                SetInputValue(input, field.Type, value, field.Choices);
            _inputs[field.Name] = input;
            body.Controls.Add(label);
            body.Controls.Add(input);
            y += 45;
        }

        var save = new Button
        {
            Text = "Lưu",
            BackColor = Color.Moccasin,
            Size = new Size(120, 36),
            Location = new Point(160, body.Bottom + 17),
            UseVisualStyleBackColor = false
        };
        save.Click += Save_Click;
        Controls.Add(save);

        var cancel = new Button
        {
            Text = "Thoát",
            BackColor = SystemColors.GradientActiveCaption,
            Size = new Size(120, 36),
            Location = new Point(294, body.Bottom + 17),
            UseVisualStyleBackColor = false,
            DialogResult = DialogResult.Cancel
        };
        Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static Control CreateInput(EditField field)
    {
        if (field.Type == EditFieldType.Choice)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            if (field.Choices is not null)
                combo.Items.AddRange(field.Choices);
            return combo;
        }
        if (field.Type == EditFieldType.Date)
            return new DateTimePicker { Format = DateTimePickerFormat.Short };
        return new TextBox();
    }

    private static void SetInputValue(Control input, EditFieldType type, string value, string[]? choices)
    {
        switch (type)
        {
            case EditFieldType.Date:
                if (DateTime.TryParse(value, out var date) && input is DateTimePicker picker)
                    picker.Value = date < picker.MinDate ? picker.MinDate : date > picker.MaxDate ? picker.MaxDate : date;
                break;
            case EditFieldType.Choice:
                if (input is ComboBox combo && choices is not null)
                    combo.SelectedItem = choices.FirstOrDefault(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
                break;
            default:
                input.Text = value;
                break;
        }
    }

    private void Save_Click(object? sender, EventArgs e)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in _fields)
        {
            var input = _inputs[field.Name];
            var value = input switch
            {
                DateTimePicker picker => picker.Value.ToString("yyyy-MM-dd"),
                ComboBox combo => combo.SelectedItem?.ToString() ?? string.Empty,
                _ => input.Text.Trim()
            };
            if (field.Required && string.IsNullOrWhiteSpace(value))
            {
                MessageBox.Show($"Vui lòng nhập {field.Caption.ToLowerInvariant()}.", "Thiếu thông tin",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                input.Focus();
                return;
            }
            values[field.Name] = value;
        }

        try
        {
            if (_save(values))
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không lưu được thông tin.\n{ex.Message}", "Lỗi CSDL",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
