using System.Globalization;
using HotelManagement.DangNhap;
using HotelManagement.Models;
using HotelManagement.UI;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.SoDoPhong;

public sealed class FormDanhSachDatPhong : EntityGridForm<FormDanhSachDatPhong.BookingRow>
{
    public FormDanhSachDatPhong() : base(
        "Danh sách đặt phòng",
        "Danh sách đặt phòng",
        [
            new(nameof(BookingRow.MaCtdp), "Mã chi tiết", 10),
            new(nameof(BookingRow.TenKhach), "Khách hàng", 17),
            new(nameof(BookingRow.MaPh), "Mã phòng", 9),
            new(nameof(BookingRow.CheckInText), "Nhận phòng", 15),
            new(nameof(BookingRow.CheckOutText), "Trả phòng", 15),
            new(nameof(BookingRow.TrangThai), "Trạng thái", 12),
            new(nameof(BookingRow.ThanhTien), "Thành tiền", 13, "N0")
        ],
        LoadRows,
        row => row.MaCtdp,
        OpenBooking,
        CancelBooking,
        addCaption: "Đặt phòng",
        editCaption: "Chi tiết",
        deleteCaption: "Hủy đặt")
    {
    }

    private static List<BookingRow> LoadRows(string keyword)
    {
        using var db = new HotelManagementContext();
        var query = db.Ctdps.AsNoTracking()
            .Where(item => item.DaXoa != true && item.MaPtNavigation.DaXoa != true);
        if (keyword.Length > 0)
            query = query.Where(item => item.MaCtdp.Contains(keyword) || item.MaPh.Contains(keyword) ||
                item.MaPtNavigation.MaKhNavigation.TenKh.Contains(keyword));

        return query.OrderByDescending(item => item.CheckIn)
            .Select(item => new BookingRow
            {
                MaCtdp = item.MaCtdp,
                TenKhach = item.MaPtNavigation.MaKhNavigation.TenKh,
                MaPh = item.MaPh,
                CheckIn = item.CheckIn,
                CheckOut = item.CheckOut,
                TrangThai = item.TrangThai,
                ThanhTien = item.ThanhTien ?? 0
            })
            .AsEnumerable()
            .Select(item => item with
            {
                CheckInText = item.CheckIn.ToString("dd/MM/yyyy HH:mm"),
                CheckOutText = item.CheckOut.ToString("dd/MM/yyyy HH:mm")
            })
            .ToList();
    }

    private static bool OpenBooking(string? code)
    {
        if (code is null)
        {
            using var dialog = new FormDatPhong();
            return dialog.ShowDialog() == DialogResult.OK;
        }

        using var db = new HotelManagementContext();
        var item = db.Ctdps.AsNoTracking()
            .Where(detail => detail.MaCtdp == code && detail.DaXoa != true)
            .Select(detail => new
            {
                detail.MaCtdp,
                detail.MaPh,
                detail.CheckIn,
                detail.CheckOut,
                detail.TrangThai,
                detail.SoNguoi,
                detail.ThanhTien,
                Khach = detail.MaPtNavigation.MaKhNavigation.TenKh,
                DienThoai = detail.MaPtNavigation.MaKhNavigation.Sdt
            })
            .FirstOrDefault();
        if (item is not null)
        {
            MessageBox.Show(
                $"Mã đặt phòng: {item.MaCtdp}\nKhách hàng: {item.Khach}\nĐiện thoại: {item.DienThoai}\n" +
                $"Phòng: {item.MaPh}\nSố người: {item.SoNguoi}\nNhận phòng: {item.CheckIn:dd/MM/yyyy HH:mm}\n" +
                $"Trả phòng: {item.CheckOut:dd/MM/yyyy HH:mm}\nTrạng thái: {item.TrangThai}\n" +
                $"Thành tiền: {(item.ThanhTien ?? 0):N0} đ",
                "Chi tiết đặt phòng", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        return false;
    }

    private static void CancelBooking(string code)
    {
        using var db = new HotelManagementContext();
        var item = db.Ctdps.FirstOrDefault(detail => detail.MaCtdp == code && detail.DaXoa != true);
        if (item is null)
            return;
        if (!string.Equals(item.TrangThai, "Đã đặt", StringComparison.OrdinalIgnoreCase) || item.CheckIn <= DateTime.Now)
            throw new InvalidOperationException("Chỉ có thể hủy phiếu đặt chưa đến giờ nhận phòng.");
        if (db.HoaDons.Any(invoice => invoice.MaCtdp == code && invoice.DaXoa != true))
            throw new InvalidOperationException("Đặt phòng đã có hóa đơn nên không thể hủy.");

        item.DaXoa = true;
        if (!db.Ctdps.Any(detail => detail.MaPt == item.MaPt && detail.MaCtdp != code && detail.DaXoa != true))
        {
            var ticket = db.PhieuThues.FirstOrDefault(booking => booking.MaPt == item.MaPt);
            if (ticket is not null)
                ticket.DaXoa = true;
        }
        db.SaveChanges();
    }

    public sealed record BookingRow
    {
        public string MaCtdp { get; init; } = "";
        public string TenKhach { get; init; } = "";
        public string MaPh { get; init; } = "";
        public DateTime CheckIn { get; init; }
        public DateTime CheckOut { get; init; }
        public string CheckInText { get; init; } = "";
        public string CheckOutText { get; init; } = "";
        public string TrangThai { get; init; } = "";
        public decimal ThanhTien { get; init; }
    }
}

public sealed class FormDatPhong : Form
{
    private readonly string? _initialRoomCode;
    private readonly TextBox _guestName = new() { MaxLength = 50 };
    private readonly TextBox _identity = new() { MaxLength = 12 };
    private readonly TextBox _phone = new() { MaxLength = 10 };
    private readonly TextBox _nationality = new() { MaxLength = 30, Text = "Việt Nam" };
    private readonly ComboBox _gender = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _people = new() { Minimum = 1, Maximum = 50, Value = 1 };
    private readonly DateTimePicker _checkIn = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd/MM/yyyy HH:mm",
        ShowUpDown = true
    };
    private readonly DateTimePicker _checkOut = new()
    {
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd/MM/yyyy HH:mm",
        ShowUpDown = true
    };
    private readonly DataGridView _availableGrid = new();
    private readonly DataGridView _selectedGrid = new();
    private readonly Label _summary = new() { AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly List<BookingRoomRow> _selectedRooms = [];
    private bool _initialRoomHandled;

    public FormDatPhong(string? initialRoomCode = null)
    {
        _initialRoomCode = initialRoomCode;
        Text = "Đặt phòng khách sạn";
        ClientSize = new Size(1080, 720);
        MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        ShowInTaskbar = false;
        Font = new Font("Microsoft Sans Serif", 9.5F);
        _gender.Items.AddRange(["Nam", "Nữ"]);
        _gender.SelectedIndex = 0;
        SetDefaultTime();
        BuildLayout();
        _checkIn.ValueChanged += (_, _) => LoadAvailableRooms();
        _checkOut.ValueChanged += (_, _) => LoadAvailableRooms();
        Load += (_, _) => LoadAvailableRooms();
    }

    private void SetDefaultTime()
    {
        var start = DateTime.Now.AddHours(1);
        start = new DateTime(start.Year, start.Month, start.Day, start.Hour, (start.Minute / 15) * 15, 0);
        _checkIn.Value = start;
        _checkOut.Value = start.AddDays(1);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10),
            Margin = Padding.Empty
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 182));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        Controls.Add(root);

        var details = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            Padding = new Padding(5),
            BackColor = SystemColors.ControlLightLight,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };
        for (var i = 0; i < 4; i++)
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        details.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var title = new Label
        {
            Text = "THÔNG TIN KHÁCH VÀ THỜI GIAN ĐẶT PHÒNG",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Microsoft Sans Serif", 14F, FontStyle.Bold),
            ForeColor = Color.FromArgb(192, 64, 0)
        };
        details.Controls.Add(title, 0, 0);
        details.SetColumnSpan(title, 4);
        AddInput(details, "Họ và tên", _guestName, 0, 1);
        AddInput(details, "CCCD / Passport", _identity, 1, 1);
        AddInput(details, "Số điện thoại", _phone, 2, 1);
        AddInput(details, "Giới tính", _gender, 3, 1);
        AddInput(details, "Quốc tịch", _nationality, 0, 2);
        AddInput(details, "Số người", _people, 1, 2);
        AddInput(details, "Nhận phòng", _checkIn, 2, 2);
        AddInput(details, "Trả phòng", _checkOut, 3, 2);
        root.Controls.Add(details, 0, 0);

        var rooms = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0, 8, 0, 5)
        };
        rooms.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        rooms.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));
        rooms.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        rooms.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        rooms.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rooms.Controls.Add(Heading("Phòng còn trống theo thời gian đã chọn"), 0, 0);
        rooms.Controls.Add(Heading("Phòng đã chọn"), 2, 0);
        ConfigureRoomGrid(_availableGrid);
        ConfigureRoomGrid(_selectedGrid);
        rooms.Controls.Add(_availableGrid, 0, 1);
        rooms.Controls.Add(_selectedGrid, 2, 1);

        var transferButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4, 80, 4, 0)
        };
        var add = new Button { Text = "Thêm  →", Size = new Size(88, 36) };
        var remove = new Button { Text = "←  Bỏ", Size = new Size(88, 36) };
        add.Click += (_, _) => AddSelectedRoom();
        remove.Click += (_, _) => RemoveSelectedRoom();
        transferButtons.Controls.Add(add);
        transferButtons.Controls.Add(remove);
        rooms.Controls.Add(transferButtons, 1, 1);
        root.Controls.Add(rooms, 0, 1);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0, 7, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        _summary.Text = "Đã chọn 0 phòng";
        footer.Controls.Add(_summary, 0, 0);
        var book = new Button { Text = "Đặt phòng", Dock = DockStyle.Fill, BackColor = Color.LightPink, Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold), UseVisualStyleBackColor = false };
        book.Click += SaveBooking_Click;
        footer.Controls.Add(book, 1, 0);
        var close = new Button { Text = "Thoát", Dock = DockStyle.Fill, DialogResult = DialogResult.Cancel };
        close.Click += (_, _) => Close();
        footer.Controls.Add(close, 2, 0);
        root.Controls.Add(footer, 0, 2);
        AcceptButton = book;
        CancelButton = close;
    }

    private static void AddInput(TableLayoutPanel layout, string caption, Control input, int column, int row)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(6, 3, 6, 3),
            Margin = Padding.Empty
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        input.Dock = DockStyle.Fill;
        input.Margin = Padding.Empty;
        panel.Controls.Add(input, 0, 1);
        layout.Controls.Add(panel, column, row);
    }

    private static Label Heading(string caption) => new()
    {
        Text = caption,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Microsoft Sans Serif", 10F, FontStyle.Bold),
        ForeColor = Color.DarkSlateBlue
    };

    private static void ConfigureRoomGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.BackgroundColor = Color.DarkGray;
        grid.BorderStyle = BorderStyle.Fixed3D;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.GridColor = SystemColors.ControlDark;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.RowTemplate.Height = 32;
        grid.Columns.Add(RoomColumn("MaPh", "Mã phòng", 13));
        grid.Columns.Add(RoomColumn("TenLph", "Loại phòng", 22));
        grid.Columns.Add(RoomColumn("SoNguoiToiDa", "Tối đa", 10));
        grid.Columns.Add(RoomColumn("GiaNgay", "Giá/ngày", 15, "N0"));
    }

    private static DataGridViewTextBoxColumn RoomColumn(string property, string title, float fill, string? format = null) => new()
    {
        Name = property,
        DataPropertyName = property,
        HeaderText = title,
        FillWeight = fill,
        ReadOnly = true,
        DefaultCellStyle = { Format = format ?? string.Empty }
    };

    private void LoadAvailableRooms()
    {
        if (!IsHandleCreated || IsDisposed)
            return;
        if (_checkOut.Value <= _checkIn.Value)
        {
            _availableGrid.DataSource = new List<BookingRoomRow>();
            return;
        }

        try
        {
            using var db = new HotelManagementContext();
            var start = _checkIn.Value;
            var end = _checkOut.Value;
            var busy = db.Ctdps.AsNoTracking()
                .Where(detail => detail.DaXoa != true && detail.MaPtNavigation.DaXoa != true &&
                    detail.CheckIn < end && detail.CheckOut > start)
                .Select(detail => detail.MaPh)
                .Distinct()
                .ToHashSet();
            var rooms = db.Phongs.AsNoTracking()
                .Where(room => room.DaXoa != true && room.Ttph != "Đang sửa chữa")
                .OrderBy(room => room.MaPh)
                .Select(room => new BookingRoomRow
                {
                    MaPh = room.MaPh,
                    TenLph = room.MaLphNavigation.TenLph,
                    SoNguoiToiDa = room.MaLphNavigation.SoNguoiToiDa,
                    GiaNgay = room.MaLphNavigation.GiaNgay,
                    GiaGio = room.MaLphNavigation.GiaGio
                })
                .ToList()
                .Where(room => !busy.Contains(room.MaPh) && _selectedRooms.All(selected => selected.MaPh != room.MaPh))
                .ToList();

            if (!_initialRoomHandled)
            {
                _initialRoomHandled = true;
                if (_initialRoomCode is not null)
                {
                    var initial = rooms.FirstOrDefault(room => room.MaPh == _initialRoomCode);
                    if (initial is not null)
                    {
                        _selectedRooms.Add(initial);
                        rooms.Remove(initial);
                        RefreshSelectedRooms();
                    }
                }
            }
            _availableGrid.DataSource = rooms;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không tải được phòng trống.\n{ex.Message}", "Lỗi CSDL", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void AddSelectedRoom()
    {
        if (_availableGrid.CurrentRow?.DataBoundItem is not BookingRoomRow room)
            return;
        if (_selectedRooms.Any(item => item.MaPh == room.MaPh))
            return;
        if (_people.Value > room.SoNguoiToiDa)
        {
            MessageBox.Show($"Phòng {room.MaPh} chỉ nhận tối đa {room.SoNguoiToiDa} người.", "Thông tin",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _selectedRooms.Add(room);
        LoadAvailableRooms();
        RefreshSelectedRooms();
    }

    private void RemoveSelectedRoom()
    {
        if (_selectedGrid.CurrentRow?.DataBoundItem is not BookingRoomRow room)
            return;
        _selectedRooms.RemoveAll(item => item.MaPh == room.MaPh);
        LoadAvailableRooms();
        RefreshSelectedRooms();
    }

    private void RefreshSelectedRooms()
    {
        _selectedGrid.DataSource = null;
        _selectedGrid.DataSource = _selectedRooms.ToList();
        _summary.Text = $"Đã chọn {_selectedRooms.Count} phòng";
    }

    private void SaveBooking_Click(object? sender, EventArgs e)
    {
        var name = _guestName.Text.Trim();
        var identity = _identity.Text.Trim();
        var phone = _phone.Text.Trim();
        var nationality = _nationality.Text.Trim();
        if (name.Length == 0 || identity.Length == 0 || nationality.Length == 0 || _gender.SelectedItem is null)
        {
            MessageBox.Show("Vui lòng nhập đầy đủ họ tên, CCCD / Passport, quốc tịch và giới tính.", "Thiếu thông tin",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (phone.Length > 0 && (phone.Length > 10 || phone.Any(character => !char.IsDigit(character))))
        {
            MessageBox.Show("Số điện thoại chỉ gồm tối đa 10 chữ số.", "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _phone.Focus();
            return;
        }
        if (_checkOut.Value <= _checkIn.Value)
        {
            MessageBox.Show("Thời gian trả phòng phải sau thời gian nhận phòng.", "Thông tin chưa hợp lệ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_checkIn.Value < DateTime.Now.AddMinutes(-1))
        {
            MessageBox.Show("Thời gian nhận phòng không thể nằm trong quá khứ.", "Thông tin chưa hợp lệ",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_selectedRooms.Count == 0)
        {
            MessageBox.Show("Hãy chọn ít nhất một phòng.", "Thiếu phòng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using var db = new HotelManagementContext();
            var employeeCode = FormDangNhap.MaNV_DangNhap;
            var employeeExists = db.NhanViens.Any(employee => employee.MaNv == employeeCode && employee.DaXoa != true);
            if (!employeeExists)
            {
                MessageBox.Show("Không tìm thấy nhân viên đang đăng nhập để lập phiếu đặt phòng.", "Lỗi tài khoản",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var start = _checkIn.Value;
            var end = _checkOut.Value;
            var selectedCodes = _selectedRooms.Select(room => room.MaPh).ToList();
            var conflict = db.Ctdps.AsNoTracking().Any(detail =>
                selectedCodes.Contains(detail.MaPh) && detail.DaXoa != true && detail.MaPtNavigation.DaXoa != true &&
                detail.CheckIn < end && detail.CheckOut > start);
            if (conflict)
            {
                MessageBox.Show("Có phòng vừa được đặt trùng thời gian. Hãy tải lại danh sách và chọn phòng khác.",
                    "Phòng không còn trống", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                LoadAvailableRooms();
                return;
            }

            foreach (var room in _selectedRooms)
            {
                if (_people.Value > room.SoNguoiToiDa)
                {
                    MessageBox.Show($"Phòng {room.MaPh} chỉ nhận tối đa {room.SoNguoiToiDa} người.", "Thông tin chưa hợp lệ",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            using var transaction = db.Database.BeginTransaction();
            var customer = db.KhachHangs.FirstOrDefault(item => item.CccdPassport == identity);
            if (customer is null)
            {
                customer = new HotelManagement.Models.KhachHang
                {
                    MaKh = NextCode(db.KhachHangs.Select(item => item.MaKh).ToList(), "KH", 5),
                    DaXoa = false
                };
                db.KhachHangs.Add(customer);
            }
            customer.TenKh = name;
            customer.CccdPassport = identity;
            customer.Sdt = string.IsNullOrWhiteSpace(phone) ? null : phone;
            customer.QuocTich = nationality;
            customer.GioiTinh = _gender.SelectedItem.ToString()!;
            customer.DaXoa = false;
            db.SaveChanges();

            var ticketCode = NextCode(db.PhieuThues.Select(item => item.MaPt).ToList(), "PT", 5);
            db.PhieuThues.Add(new PhieuThue
            {
                MaPt = ticketCode,
                NgPt = DateTime.Now,
                MaKh = customer.MaKh,
                MaNv = employeeCode,
                DaXoa = false
            });
            db.SaveChanges();

            foreach (var room in _selectedRooms)
            {
                var detailCode = NextCode(db.Ctdps.Select(item => item.MaCtdp).ToList(), "CTDP", 7);
                db.Ctdps.Add(new Ctdp
                {
                    MaCtdp = detailCode,
                    MaPt = ticketCode,
                    MaPh = room.MaPh,
                    CheckIn = start,
                    CheckOut = end,
                    SoNguoi = (int)_people.Value,
                    TrangThai = "Đã đặt",
                    DonGia = 0,
                    ThanhTien = 0,
                    TheoGio = (end - start).TotalHours < 4,
                    DaXoa = false
                });
                db.SaveChanges();
            }
            transaction.Commit();

            MessageBox.Show($"Đặt phòng thành công. Mã phiếu: {ticketCode}", "Thông báo",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (DbUpdateException ex)
        {
            MessageBox.Show($"Không lưu được đặt phòng.\n{ex.InnerException?.Message ?? ex.Message}", "Lỗi CSDL",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không lưu được đặt phòng.\n{ex.Message}", "Lỗi CSDL", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string NextCode(IReadOnlyCollection<string> codes, string prefix, int maxLength)
    {
        var numericWidth = maxLength - prefix.Length;
        var largest = codes
            .Where(code => code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(code => code[prefix.Length..])
            .Where(suffix => int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .Select(suffix => int.Parse(suffix, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();
        if (largest >= Math.Pow(10, numericWidth) - 1)
            throw new InvalidOperationException($"Đã hết mã {prefix} có thể tạo theo độ dài hiện tại.");
        return prefix + (largest + 1).ToString(new string('0', numericWidth), CultureInfo.InvariantCulture);
    }

    private sealed class BookingRoomRow
    {
        public string MaPh { get; init; } = "";
        public string TenLph { get; init; } = "";
        public int SoNguoiToiDa { get; init; }
        public decimal GiaNgay { get; init; }
        public decimal GiaGio { get; init; }
    }
}
