using System.Runtime.CompilerServices;

namespace KiotVietLabelPrinter.UI;

public static class UiMotion
{
    private static readonly ConditionalWeakTable<Control, SlideState> SlideStates = new();

    public static void EnableFadeIn(Form form, int durationMs = 170)
    {
        if (!AppTheme.MotionEnabled)
            return;

        form.Opacity = 0;

        form.Shown += (_, _) =>
        {
            long startedAt = Environment.TickCount64;
            System.Windows.Forms.Timer timer = new() { Interval = 15 };

            timer.Tick += (_, _) =>
            {
                float progress = Math.Clamp(
                    (Environment.TickCount64 - startedAt) / (float)durationMs,
                    0f,
                    1f);

                form.Opacity = EaseOutCubic(progress);

                if (progress >= 1f || form.IsDisposed)
                {
                    timer.Stop();
                    timer.Dispose();

                    if (!form.IsDisposed)
                        form.Opacity = 1;
                }
            };

            timer.Start();
        };
    }

    public static void SlideIn(Control control, int targetLeft, int offset = 16, int durationMs = 190)
    {
        if (!AppTheme.MotionEnabled)
        {
            control.Left = targetLeft;
            control.Visible = true;
            return;
        }

        SlideState state = SlideStates.GetValue(control, static item => new SlideState(item));
        state.Start(targetLeft, offset, durationMs);
    }

    private static float EaseOutCubic(float value)
    {
        float inverse = 1f - value;
        return 1f - (inverse * inverse * inverse);
    }

    private sealed class SlideState : IDisposable
    {
        private readonly Control _control;
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
        private long _startedAt;
        private int _startLeft;
        private int _targetLeft;
        private int _durationMs;

        public SlideState(Control control)
        {
            _control = control;
            _timer.Tick += Animate;
            _control.Disposed += (_, _) => Dispose();
        }

        public void Start(int targetLeft, int offset, int durationMs)
        {
            _timer.Stop();
            _targetLeft = targetLeft;
            _startLeft = targetLeft + offset;
            _durationMs = Math.Max(1, durationMs);
            _startedAt = Environment.TickCount64;

            _control.Left = _startLeft;
            _control.Visible = true;
            _timer.Start();
        }

        private void Animate(object? sender, EventArgs e)
        {
            if (_control.IsDisposed)
            {
                Dispose();
                return;
            }

            float progress = Math.Clamp(
                (Environment.TickCount64 - _startedAt) / (float)_durationMs,
                0f,
                1f);
            float eased = EaseOutCubic(progress);

            _control.Left = (int)Math.Round(_startLeft + ((_targetLeft - _startLeft) * eased));

            if (progress >= 1f)
            {
                _control.Left = _targetLeft;
                _timer.Stop();
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
