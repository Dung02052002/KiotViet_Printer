namespace KiotVietLabelPrinter.Models;

// Định nghĩa một "công cụ" trong mục CÔNG CỤ HÌNH ẢNH — tách riêng khỏi
// LabelDefinition (DANH MỤC TEM) vì đây là nhóm chức năng khác, không phải
// một loại tem, dù cả hai đều là card lấy từ cấu hình.
public class ToolDefinition
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
}
