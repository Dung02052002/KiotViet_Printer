using KiotVietLabelPrinter.Services.Labels.Handlers;

namespace KiotVietLabelPrinter.Services.Labels;

public class LabelHandlerFactory
{
    private readonly Dictionary<string, ILabelHandler> _handlers;

    public LabelHandlerFactory()
    {
        _handlers = new Dictionary<string, ILabelHandler>(StringComparer.OrdinalIgnoreCase)
        {
            { "FULL", new FullLabelHandler() },
            { "BARCODE", new BarcodeLabelHandler() },
            { "GENERIC", new GenericLabelHandler() },
            { "GLASSES", new GlassesLabelHandler() }
        };
    }

    public ILabelHandler GetHandler(string handlerType)
    {
        if (!_handlers.TryGetValue(handlerType, out var handler))
            throw new Exception($"Chưa hỗ trợ handler: {handlerType}");

        return handler;
    }
}