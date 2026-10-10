using System.Runtime.ExceptionServices;
using KiotVietLabelPrinter.Infrastructure.Storage;
using KiotVietLabelPrinter.UI;
using KiotVietLabelPrinter.Views;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

/// <summary>
/// Bố cục màn hình Phiếu xuất kho ở các kích thước cửa sổ thường gặp (nhỏ
/// nhất FormMain cho phép → toàn màn hình), có / không có dữ liệu: không vùng
/// nào đè lên nhau, không control nào lòi ra ngoài khung chứa, nút thao tác
/// luôn nhìn thấy, chữ trên nút không bị cắt. Chạy trên DPI hiện tại của máy
/// (các mức 125% / 150% cần đổi scaling của Windows rồi chạy lại).
/// </summary>
public class WarehouseExportViewLayoutTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kv-wh-layout-" + Guid.NewGuid().ToString("N"));

    public WarehouseExportViewLayoutTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Thư mục tạm của test — để hệ thống tự dọn nếu còn file bị khoá.
        }
    }

    // FormMain nhỏ nhất 940×815 → khung công cụ ~ 876×540; 1366×768; toàn màn hình 1920×1080.
    [Theory]
    [InlineData(876, 540, 0)]
    [InlineData(876, 540, 17)]
    [InlineData(1300, 640, 1)]
    [InlineData(1300, 640, 17)]
    [InlineData(1856, 820, 60)]
    public void Layout_NoOverlap_NoClipping_ActionsAlwaysVisible(int width, int height, int rows)
    {
        RunInMessageLoop(width, height, async (form, view) =>
        {
            if (rows > 0)
            {
                string template = WarehouseTestData.CreateTemplate(_dir, dataRows: 2, fileName: $"mau-{rows}.xlsx");
                object?[][] lines = Enumerable.Range(1, rows)
                    .Select(i => WarehouseTestData.Line($"SP{i:000}",
                        i % 3 == 0
                            ? $"Giày nữ Pucini mũi nhọn da bóng gót vuông 7cm kèm dây quai hậu màu đen size {35 + i % 6} mẫu {i}"
                            : $"Kính mắt gọng kim loại {i}", i))
                    .ToArray();
                string goods = WarehouseTestData.CreateGoods(_dir, ["Mã hàng", "Tên hàng", "Đơn vị tính", "Số lượng chuyển", "Số lượng nhận"],
                    lines, $"hang-{rows}.xlsx");
                await view.LoadFilesAsync(template, goods);
                await Settle();
                Assert.Equal(rows, Grid(view).RowCount);
            }

            SaveScreenshot(view, $"{width}x{height}-{rows}rows");
            AssertLayout(view);

            // Cuộn tới cuối trang: bảng nằm trọn trong vùng nhìn thấy, thanh nút vẫn ở đó.
            ScrollableControl scroll = Grid(view).Parent!.Parent!.Parent as ScrollableControl
                                       ?? throw new InvalidOperationException("Không tìm thấy vùng cuộn");
            scroll.AutoScrollPosition = new Point(0, int.MaxValue / 2);
            await Settle();
            Rectangle gridOnScreen = Grid(view).RectangleToScreen(Grid(view).ClientRectangle);
            Rectangle viewport = scroll.RectangleToScreen(scroll.ClientRectangle);
            Assert.True(viewport.Contains(gridOnScreen), $"Cuộn hết trang mà bảng {gridOnScreen} vẫn không nằm trọn trong vùng nhìn thấy {viewport}");
            SaveScreenshot(view, $"{width}x{height}-{rows}rows-bottom");
            AssertLayout(view);
        });
    }

    [Fact]
    public void Resize_RepeatedRelayout_DoesNotGrowContent()
    {
        RunInMessageLoop(1300, 640, async (form, view) =>
        {
            ScrollableControl scroll = (ScrollableControl)Grid(view).Parent!.Parent!.Parent!;
            Size first = scroll.DisplayRectangle.Size;
            foreach (int w in new[] { 900, 1800, 1000, 1300, 1300 })
            {
                form.ClientSize = new Size(w, 640);
                await Settle();
            }

            Assert.Equal(first, scroll.DisplayRectangle.Size);
        });
    }

    /// <summary>
    /// Lăn chuột để cuộn trang mà con trỏ lướt qua một ô chọn (chưa bấm vào ô
    /// đó): trang cuộn, giá trị ô KHÔNG đổi — không âm thầm đổi phạm vi font /
    /// máy in / khổ giấy.
    /// </summary>
    [Fact]
    public void MouseWheelOverClosedCombo_ScrollsPage_DoesNotChangeSelection()
    {
        RunInMessageLoop(1300, 640, async (form, view) =>
        {
            ComboBox scope = Visible(view).OfType<ComboBox>().Single(c => c.Items.Contains("Chỉ áp dụng cho tên hàng hóa"));
            ScrollableControl scroll = (ScrollableControl)Grid(view).Parent!.Parent!.Parent!;
            int selected = scope.SelectedIndex;
            int scrolledBefore = scroll.AutoScrollPosition.Y;

            Point center = scope.PointToScreen(new Point(scope.Width / 2, scope.Height / 2));
            const int wmMouseWheel = 0x020A;
            nint wParam = unchecked((nint)(uint)((-120 & 0xFFFF) << 16));
            nint lParam = (center.Y << 16) | (center.X & 0xFFFF);
            SendMessage(scope.Handle, wmMouseWheel, wParam, lParam);
            await Settle();

            Assert.Equal(selected, scope.SelectedIndex);
            Assert.True(scroll.AutoScrollPosition.Y < scrolledBefore, "Lăn chuột trên ô chọn phải cuộn trang");
        });
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    private static void AssertLayout(WarehouseExportView view)
    {
        List<string> problems = new();
        Rectangle viewOnScreen = view.RectangleToScreen(view.ClientRectangle);

        foreach (Control control in Visible(view))
        {
            Control parent = control.Parent!;
            // Trong vùng cuộn thì so với toàn bộ vùng nội dung; còn lại so với phần client của khung chứa.
            Rectangle room = parent is ScrollableControl { AutoScroll: true } s ? s.DisplayRectangle : parent.ClientRectangle;
            if (!room.Contains(control.Bounds))
                problems.Add($"{Name(control)} {control.Bounds} lòi ra ngoài {Name(parent)} {room}");

            List<Control> siblings = parent.Controls.Cast<Control>().Where(c => c.Visible && c != control).ToList();
            foreach (Control other in siblings.Where(o => parent.Controls.GetChildIndex(o) > parent.Controls.GetChildIndex(control)))
            {
                if (control.Bounds.IntersectsWith(other.Bounds))
                    problems.Add($"{Name(control)} {control.Bounds} đè lên {Name(other)} {other.Bounds}");
            }

            if (control is RoundedButton button && button.GetContentWidth(0) > button.Width)
                problems.Add($"Nút \"{button.Text}\" rộng {button.Width}px không đủ cho chữ ({button.GetContentWidth(0)}px)");

            // Font đã bị giải phóng mà nhãn vẫn dùng: đo / vẽ lại sẽ ném "Parameter is not valid".
            if (control is Label withFont && !FontUsable(withFont.Font))
            {
                problems.Add($"{Name(withFont)} đang dùng font đã bị giải phóng");
                continue;
            }

            // Nhãn một dòng bị kéo cao (hàng của TableLayoutPanel giãn rồi không co lại được) = vùng trống lớn bất thường.
            if (control is Label { AutoSize: false, AutoEllipsis: true } oneLine && oneLine.Height > oneLine.Font.Height * 2)
                problems.Add($"{Name(oneLine)} một dòng nhưng cao {oneLine.Height}px (chữ {oneLine.Font.Height}px)");

            if (control is Label { AutoSize: false } label && label.Height < label.Font.Height)
                problems.Add($"Nhãn \"{label.Text}\" cao {label.Height}px thấp hơn chữ ({label.Font.Height}px)");
        }

        // Không có vùng trống lớn bất thường: mỗi khối nội dung cao đúng bằng phần nó cần.
        Control content = Grid(view).Parent!.Parent!;
        // (So với chỗ các control con thật sự kết thúc — không so với PreferredSize, vì chính
        // PreferredSize có thể đã bị đo sai thành rất lớn.)
        foreach (Control section in content.Controls)
        {
            int used = section.Controls.Cast<Control>().Where(c => c.Visible).Select(c => c.Bottom + c.Margin.Bottom).DefaultIfEmpty(0).Max()
                       + section.Padding.Bottom;
            if (section.Height > used + 4)
                problems.Add($"Khối {Name(section)} ở Y={section.Top} cao {section.Height}px nhưng nội dung kết thúc ở {used}px");
        }

        // Thanh thao tác cố định: mọi nút thao tác nằm trọn trong màn hình mà không cần cuộn.
        foreach (RoundedButton button in Visible(view).OfType<RoundedButton>().Where(b => b.Text is "XUẤT PHIẾU EXCEL" or "IN PHIẾU" or "XEM TRƯỚC BẢN IN"))
        {
            Rectangle onScreen = button.RectangleToScreen(button.ClientRectangle);
            if (!viewOnScreen.Contains(onScreen))
                problems.Add($"Nút \"{button.Text}\" {onScreen} không nằm trọn trong màn hình {viewOnScreen}");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static bool FontUsable(Font font)
    {
        try
        {
            return font.Height > 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static IEnumerable<Control> Visible(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (!child.Visible)
                continue;
            yield return child;
            // Không soi vào bên trong control "đóng gói" sẵn (ô nhập, combo, bảng...).
            if (child is RoundedTextBox or NumericUpDown or ComboBox or DataGridView or RoundedButton)
                continue;
            foreach (Control nested in Visible(child))
                yield return nested;
        }
    }

    private static DataGridView Grid(Control view) => Visible(view).OfType<DataGridView>().Single();

    private static string Name(Control control) =>
        control switch
        {
            Label l => $"Label(\"{Shorten(l.Text)}\")",
            ButtonBase b => $"{control.GetType().Name}(\"{b.Text}\")",
            _ => control.GetType().Name
        };

    private static string Shorten(string text) => text.Length > 30 ? text[..30] + "…" : text;

    /// <summary>Cho vòng lặp thông điệp xử lý xong các lần dàn trang / vẽ đang chờ.</summary>
    private static Task Settle() => Task.Delay(120);

    /// <summary>Ảnh chụp để xem bằng mắt khi cần: đặt biến môi trường WAREHOUSE_LAYOUT_SHOTS = thư mục lưu.</summary>
    private static void SaveScreenshot(Control view, string name)
    {
        string? folder = Environment.GetEnvironmentVariable("WAREHOUSE_LAYOUT_SHOTS");
        if (string.IsNullOrEmpty(folder))
            return;
        Directory.CreateDirectory(folder);
        using Bitmap bitmap = new(view.Width, view.Height);
        view.DrawToBitmap(bitmap, view.ClientRectangle);
        bitmap.Save(Path.Combine(folder, name + ".png"));
    }

    /// <summary>
    /// Chạy kịch bản trong một vòng lặp thông điệp thật (Application.Run) trên
    /// luồng STA — giống hệt app: các await của màn hình quay về đúng luồng giao
    /// diện. (Bơm thông điệp bằng DoEvents ngoài Application.Run thì không có
    /// SynchronizationContext của WinForms, phần sau await chạy nhầm sang luồng
    /// khác và test hỏng ngẫu nhiên.)
    /// </summary>
    private void RunInMessageLoop(int width, int height, Func<Form, WarehouseExportView, Task> scenario)
    {
        Exception? error = null;
        Thread thread = new(() =>
        {
            using Form form = new()
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(0, 0),
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                ClientSize = new Size(width, height),
                BackColor = AppTheme.Colors.Background
            };
            WarehouseExportView view = new(new WarehouseSettingsStore(Path.Combine(_dir, $"settings-{Guid.NewGuid():N}.json")));
            form.Controls.Add(view);
            form.Shown += async (_, _) =>
            {
                try
                {
                    await Settle();
                    await scenario(form, view);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    form.Close();
                }
            };
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromMinutes(2)))
            throw new TimeoutException("Kịch bản bố cục không kết thúc sau 2 phút.");
        if (error != null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
