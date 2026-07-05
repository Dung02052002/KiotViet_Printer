using KiotVietLabelPrinter.Services.Handlers;
using KiotVietLabelPrinter.Services.Interfaces;

namespace KiotVietLabelPrinter.Services;

public class LabelHandlerFactory
{
    private readonly Dictionary<string, ILabelHandler> _handlers;

    public LabelHandlerFactory()
    {
        _handlers = new Dictionary<string, ILabelHandler>(StringComparer.OrdinalIgnoreCase)
        {
            { "FULL", new FullLabelHandler() },
            { "BARCODE", new BarcodeLabelHandler() },
            { "GENERIC", new GenericLabelHandler() }
        };
    }

    public ILabelHandler GetHandler(string handlerType)
    {
        if (!_handlers.TryGetValue(handlerType, out var handler))
            throw new Exception($"Chưa hỗ trợ handler: {handlerType}");

        return handler;
    }
}