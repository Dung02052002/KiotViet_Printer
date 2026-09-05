using System.ComponentModel;

namespace KiotVietLabelPrinter.UI;

// Lightweight control that renders one IconGlyphs.Kind. Used anywhere an icon
// stands alone (header badge, card icon, card header) rather than inside a
// RoundedButton (which draws its own leading icon).
public class IconGlyph : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IconGlyphs.Kind Kind { get; set; } = IconGlyphs.Kind.Tag;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color IconColor { get; set; } = AppTheme.Colors.Primary;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float StrokeWidth { get; set; } = 1.75f;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ContainerColor { get; set; } = AppTheme.Colors.Background;

    public IconGlyph()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        Cursor = Cursors.Default;
        TabStop = false;
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        AppTheme.PaintContainerBackground(this, pevent, ContainerColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Explicit opaque fill instead of relying solely on OnPaintBackground's
        // erase — that pattern let stale sibling content show through on first
        // paint elsewhere in the app (RoundedButton, ToggleSwitch).
        using (SolidBrush bg = new(ContainerColor))
            e.Graphics.FillRectangle(bg, 0, 0, Width, Height);

        AppTheme.PrepareSmoothing(e.Graphics);
        IconGlyphs.Draw(e.Graphics, Kind, new RectangleF(0, 0, Width, Height), IconColor, StrokeWidth);
    }
}
