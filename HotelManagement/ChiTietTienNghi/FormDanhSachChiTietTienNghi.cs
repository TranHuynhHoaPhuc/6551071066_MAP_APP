using HotelManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.ChiTietTienNghi;

public sealed class FormDanhSachChiTietTienNghi : Form
{
    private readonly string _roomTypeCode;
    private readonly string _roomTypeName;
    private readonly DataGridView _grid = new();

    public FormDanhSachChiTietTienNghi(string roomTypeCode, string roomTypeName)
    {
        _roomTypeCode = roomTypeCode;
        _roomTypeName = roomTypeName;
        Text = "Tiện nghi theo loại phòng";
        ClientSize = new Size(820, 560);
        MinimumSize = new Size(680, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = false;
        BuildLayout();
        Load += (_, _) => LoadRows();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        var heading = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"TIỆN NGHI HIỆN CÓ\nLoại phòng: {_roomTypeName} ({_roomTypeCode})",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft Sans Serif", 14F, FontStyle.Bold),
            ForeColor = Color.DarkSlateBlue
        };
        root.Controls.Add(heading, 0, 0);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0)
        };
        var close = new Button { Text = "Đóng", Size = new Size(110, 34), DialogResult = DialogResult.Cancel };
        var add = new Button { Text = "Thêm tiện nghi", Size = new Size(140, 34), BackColor = Color.LightPink, UseVisualStyleBackColor = false };
        close.Click += (_, _) => Close();
        add.Click += (_, _) => EditDetail(null);
        actions.Controls.Add(close);
        actions.Controls.Add(add);
        root.Controls.Add(actions, 0, 2);
        CancelButton = close;
    }

    private void ConfigureGrid()
    {
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = Color.DarkGray;
        _grid.BorderStyle = BorderStyle.Fixed3D;
        _grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        _grid.GridColor = SystemColors.ControlDark;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.RowTemplate.Height = 36;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MaTn", DataPropertyName = "MaTn", HeaderText = "Mã tiện nghi", FillWeight = 20, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TenTn", DataPropertyName = "TenTn", HeaderText = "Tên tiện nghi", FillWeight = 42, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SoLuong", DataPropertyName = "SoLuong", HeaderText = "Số lượng", FillWeight = 20, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewButtonColumn { Name = "colSua", HeaderText = "Cập nhật", Text = "Sửa", UseColumnTextForButtonValue = true, Width = 75, FillWeight = 10, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewButtonColumn { Name = "colXoa", HeaderText = "Xóa", Text = "Xóa", UseColumnTextForButtonValue = true, Width = 65, FillWeight = 8, ReadOnly = true });
        _grid.CellClick += Grid_CellClick;
    }

    private void LoadRows()
    {
        try
        {
            using var db = new HotelManagementContext();
            _grid.DataSource = db.Cttns.AsNoTracking()
                .Where(item => item.MaLph == _roomTypeCode && item.DaXoa != true && item.MaTnNavigation.DaXoa != true)
                .OrderBy(item => item.MaTn)
                .Select(item => new AmenityDetailRow
                {
                    MaTn = item.MaTn,
                    TenTn = item.MaTnNavigation.TenTn,
                    SoLuong = item.Sl == null || item.Sl < 0 ? "Không giới hạn" : item.Sl.Value.ToString()
                })
                .ToList();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được tiện nghi của loại phòng.\n{ex.Message}", "Lỗi CSDL",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || _grid.Rows[e.RowIndex].DataBoundItem is not AmenityDetailRow row)
            return;
        if (_grid.Columns[e.ColumnIndex].Name == "colSua")
        {
            EditDetail(row.MaTn);
        }
        else if (_grid.Columns[e.ColumnIndex].Name == "colXoa" &&
                 MessageBox.Show($"Xóa tiện nghi {row.TenTn} khỏi loại phòng này?", "Xác nhận",
                     MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            try
            {
                using var db = new HotelManagementContext();
                var entity = db.Cttns.FirstOrDefault(item => item.MaLph == _roomTypeCode && item.MaTn == row.MaTn && item.DaXoa != true);
                if (entity is not null)
                {
                    entity.DaXoa = true;
                    db.SaveChanges();
                }
                LoadRows();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không xóa được tiện nghi.\n{ex.Message}", "Lỗi CSDL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void EditDetail(string? amenityCode)
    {
        using var dialog = new FormChiTietTienNghiDialog(_roomTypeCode, amenityCode);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadRows();
    }

    private sealed class AmenityDetailRow
    {
        public string MaTn { get; init; } = "";
        public string TenTn { get; init; } = "";
        public string SoLuong { get; init; } = "";
    }
}

internal sealed class FormChiTietTienNghiDialog : Form
{
    private readonly string _roomTypeCode;
    private readonly string? _amenityCode;
    private readonly ComboBox _amenity = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _quantity = new() { MaxLength = 8, Text = "-1" };

    public FormChiTietTienNghiDialog(string roomTypeCode, string? amenityCode)
    {
        _roomTypeCode = roomTypeCode;
        _amenityCode = amenityCode;
        Text = amenityCode is null ? "Thêm tiện nghi cho loại phòng" : "Cập nhật số lượng tiện nghi";
        ClientSize = new Size(500, 220);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        BuildLayout();
        Load += (_, _) => LoadData();
    }

    private void BuildLayout()
    {
        var title = new Label
        {
            Text = _amenityCode is null ? "Thêm tiện nghi" : "Cập nhật tiện nghi",
            Dock = DockStyle.Top,
            Height = 48,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft Sans Serif", 14F, FontStyle.Bold),
            ForeColor = Color.FromArgb(192, 64, 0)
        };
        Controls.Add(title);
        AddField("Tiện nghi", _amenity, 68);
        AddField("Số lượng (-1: không giới hạn)", _quantity, 112);
        var save = new Button { Text = "Lưu", Size = new Size(105, 32), Location = new Point(135, 170), BackColor = Color.Moccasin, UseVisualStyleBackColor = false };
        var cancel = new Button { Text = "Thoát", Size = new Size(105, 32), Location = new Point(260, 170), DialogResult = DialogResult.Cancel };
        save.Click += Save_Click;
        cancel.Click += (_, _) => Close();
        Controls.Add(save);
        Controls.Add(cancel);
        AcceptButton = save;
        CancelButton = cancel;
        _quantity.KeyPress += (_, e) =>
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && !(e.KeyChar == '-' && _quantity.SelectionStart == 0))
                e.Handled = true;
        };
    }

    private void AddField(string caption, Control input, int y)
    {
        Controls.Add(new Label { Text = caption, Location = new Point(28, y + 4), Size = new Size(235, 27), TextAlign = ContentAlignment.MiddleRight });
        input.Location = new Point(274, y);
        input.Size = new Size(190, 29);
        Controls.Add(input);
    }

    private void LoadData()
    {
        try
        {
            using var db = new HotelManagementContext();
            var usedCodes = db.Cttns.Where(item => item.MaLph == _roomTypeCode && item.DaXoa != true).Select(item => item.MaTn).ToList();
            var choices = db.TienNghis.AsNoTracking().Where(item => item.DaXoa != true &&
                    (_amenityCode != null ? item.MaTn == _amenityCode : !usedCodes.Contains(item.MaTn)))
                .OrderBy(item => item.TenTn)
                .Select(item => new AmenityChoice { MaTn = item.MaTn, TenTn = item.TenTn })
                .ToList();
            _amenity.DataSource = choices;
            _amenity.DisplayMember = nameof(AmenityChoice.Display);
            _amenity.ValueMember = nameof(AmenityChoice.MaTn);
            if (_amenityCode is not null)
            {
                _amenity.SelectedValue = _amenityCode;
                _amenity.Enabled = false;
                var detail = db.Cttns.AsNoTracking().FirstOrDefault(item => item.MaLph == _roomTypeCode && item.MaTn == _amenityCode);
                _quantity.Text = detail?.Sl?.ToString() ?? "-1";
            }
            if (choices.Count == 0)
                _amenity.Enabled = false;
        }
        catch (Exception ex)
        {
            _amenity.Enabled = false;
            MessageBox.Show($"Không tải được tiện nghi.\n{ex.Message}", "Lỗi CSDL", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Save_Click(object? sender, EventArgs e)
    {
        if (_amenity.SelectedValue is not string amenityCode)
        {
            MessageBox.Show("Không còn tiện nghi nào để thêm.", "Thông tin", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!int.TryParse(_quantity.Text.Trim(), out var quantity) || quantity < -1)
        {
            MessageBox.Show("Số lượng phải là số nguyên không âm hoặc -1 nếu không giới hạn.", "Thông tin chưa hợp lệ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _quantity.Focus();
            return;
        }
        try
        {
            using var db = new HotelManagementContext();
            var item = db.Cttns.FirstOrDefault(detail => detail.MaLph == _roomTypeCode && detail.MaTn == amenityCode);
            if (_amenityCode is null && item is { DaXoa: not true })
            {
                MessageBox.Show("Tiện nghi này đã có trong loại phòng.", "Thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (item is null)
            {
                item = new Cttn { MaLph = _roomTypeCode, MaTn = amenityCode };
                db.Cttns.Add(item);
            }
            item.Sl = quantity;
            item.DaXoa = false;
            db.SaveChanges();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không lưu được tiện nghi.\n{ex.Message}", "Lỗi CSDL", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed class AmenityChoice
    {
        public string MaTn { get; init; } = "";
        public string TenTn { get; init; } = "";
        public string Display => $"{MaTn} - {TenTn}";
    }
}
