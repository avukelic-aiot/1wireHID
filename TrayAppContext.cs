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
    private readonly HashSet<string> _ignoredRoms = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _activeRoms = new(StringComparer.OrdinalIgnoreCase);

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
        AppLogger.Info($"Tray app started. portType={_portType}, portNum={_portNum}");
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

                _ignoredRoms.Clear();
                _activeRoms.Clear();
                LearnReaderDevice();
                UpdateUi(true);
                AppLogger.Info($"Adapter opened in tray. description='{_adapter.AdapterDescription}', portType={_portType}, portNum={_adapter.PortNum}, adapterRom={_adapter.AdapterRom ?? ""}");
            }

            var result = _adapter.Reset();
            var presentNow = result is TMEXResetResult.Presence or TMEXResetResult.Alarm;

            if (!presentNow)
            {
                LogRemovedDevices(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                return;
            }

            var currentRoms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rom in _adapter.SearchAll())
            {
                if (!Program.CheckCRC8(rom))
                    continue;

                string romHex = Program.FormatROM(rom);
                if (!_ignoredRoms.Contains(romHex))
                    currentRoms.Add(romHex);
            }

            foreach (var romHex in currentRoms)
            {
                if (_activeRoms.Contains(romHex))
                    continue;

                bool forwarded = KeyboardSimulator.SendROM(romHex);
                AppLogger.IButtonTouched(romHex, "tray", forwarded);
                AppLogger.Info($"iButton touch detected. rom={romHex}, source=tray, forwarded={forwarded}");
            }

            LogRemovedDevices(currentRoms);
            _activeRoms = currentRoms;
        }
        catch (Exception ex)
        {
            AppLogger.Error("Tray polling error", ex);
            _adapter?.Dispose();
            _adapter = null;
            UpdateUi(false);
        }
    }

    private void LearnReaderDevice()
    {
        if (_adapter == null || string.IsNullOrWhiteSpace(_adapter.AdapterRom))
            return;

        _ignoredRoms.Add(_adapter.AdapterRom);
        AppLogger.Info($"Ignoring reader ROM {_adapter.AdapterRom}");
    }

    private void LogRemovedDevices(HashSet<string> currentRoms)
    {
        foreach (var romHex in _activeRoms)
            if (!currentRoms.Contains(romHex))
                AppLogger.Info($"iButton removed. rom={romHex}, source=tray");

        _activeRoms = currentRoms;
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
