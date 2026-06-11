using System.Drawing;
using System.Windows.Forms;

namespace OneWireHID;

internal sealed class TrayAppContext : ApplicationContext
{
    private readonly int _portType;
    private readonly int _portNum;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _readerStatusItem;
    private TMEXAdapter? _adapter;
    private string? _ignoredRom;
    private string? _lastSentRom;
    private bool _learnedReaderRom;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public TrayAppContext(int portType, int portNum)
    {
        _portType = portType;
        _portNum = portNum;

        var menu = new ContextMenuStrip();
        _readerStatusItem = new ToolStripMenuItem("Reader not found") { Enabled = false };
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitThreadCore());

        menu.Items.AddRange(new ToolStripItem[]
        {
            _readerStatusItem,
            new ToolStripSeparator(),
            exitItem
        });

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Icon = CreateStatusIcon(false),
            Text = "1wireHID",
            ContextMenuStrip = menu,
            BalloonTipIcon = ToolTipIcon.Info,
            BalloonTipTitle = "1wireHID"
        };

        _timer = new System.Windows.Forms.Timer { Interval = 100 };
        _timer.Tick += (_, _) => PollOnce();
        _timer.Start();

        UpdateUi(false);
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _adapter?.Dispose();
        base.ExitThreadCore();
    }

    private void PollOnce()
    {
        try
        {
            if (_adapter == null)
            {
                _adapter = new TMEXAdapter(_portType, _portNum);
                if (!_adapter.Open())
                {
                    UpdateUi(false);
                    return;
                }

                _ignoredRom = null;
                _lastSentRom = null;
                _learnedReaderRom = false;
                UpdateUi(true);
            }

            var result = _adapter.Reset();
            var presentNow = result is TMEXResetResult.Presence or TMEXResetResult.Alarm;

            if (!presentNow)
            {
                return;
            }

            byte[] rom = new byte[8];
            if (!_adapter.SearchNext(rom, 0, false))
            {
                return;
            }

            string romHex = Program.FormatROM(rom);
            if (!Program.CheckCRC8(rom))
            {
                return;
            }

            if (!_learnedReaderRom)
            {
                _ignoredRom = romHex;
                _learnedReaderRom = true;
                return;
            }

            if (romHex == _ignoredRom)
            {
                return;
            }

            if (!string.Equals(_lastSentRom, romHex, StringComparison.OrdinalIgnoreCase))
            {
                KeyboardSimulator.SendROM(romHex);
                _lastSentRom = romHex;
            }
        }
        catch
        {
            _adapter?.Dispose();
            _adapter = null;
            UpdateUi(false);
        }
    }

    private void UpdateUi(bool connected)
    {
        _readerStatusItem.Text = connected ? "Reader connected" : "Reader not found";
        _notifyIcon.Icon = CreateStatusIcon(connected);
        _notifyIcon.Text = connected ? "1wireHID | Reader connected" : "1wireHID | Reader not found";
    }

    private static Icon CreateStatusIcon(bool connected)
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(connected ? Color.LimeGreen : Color.IndianRed);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(brush, 1, 1, 14, 14);
            g.DrawEllipse(Pens.Black, 1, 1, 14, 14);
        }

        var hIcon = bmp.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        var clone = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        return clone;
    }
}
