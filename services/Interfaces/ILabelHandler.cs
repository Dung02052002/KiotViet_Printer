using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Interfaces;

public interface ILabelHandler
{
    string HandlerType { get; }

    List<PreviewRow> BuildPreview(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode);

    void PrepareDataAndPrint(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode);
}