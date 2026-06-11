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
    private readonly ToolStripMenuItem _deviceStatusItem;
    private readonly ToolStripMenuItem _activityStatusItem;
    private readonly ToolStripMenuItem _reconnectItem;
    private TMEXAdapter? _adapter;
    private string? _ignoredRom;
    private string? _lastSentRom;
    private bool _learnedReaderRom;

    public TrayAppContext(int portType, int portNum)
    {
        _portType = portType;
        _portNum = portNum;

        var menu = new ContextMenuStrip();
        _readerStatusItem = new ToolStripMenuItem("Reader: searching...") { Enabled = false };
        _deviceStatusItem = new ToolStripMenuItem("iButton: waiting...") { Enabled = false };
        _activityStatusItem = new ToolStripMenuItem("Activity: idle") { Enabled = false };
        _reconnectItem = new ToolStripMenuItem("Reconnect now", null, (_, _) => ForceReconnect());
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitThreadCore());

        menu.Items.AddRange(new ToolStripItem[]
        {
            _readerStatusItem,
            _deviceStatusItem,
            _activityStatusItem,
            new ToolStripSeparator(),
            _reconnectItem,
            new ToolStripSeparator(),
            exitItem
        });

        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Icon = SystemIcons.Application,
            Text = "1wireHID",
            ContextMenuStrip = menu,
            BalloonTipIcon = ToolTipIcon.Info,
            BalloonTipTitle = "1wireHID"
        };

        _timer = new System.Windows.Forms.Timer { Interval = 100 };
        _timer.Tick += (_, _) => PollOnce();
        _timer.Start();

        UpdateUi("Reader: starting...", "iButton: waiting...", "Activity: idle", SystemIcons.Information, false);
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _adapter?.Dispose();
        base.ExitThreadCore();
    }

    private void ForceReconnect()
    {
        _adapter?.Dispose();
        _adapter = null;
        _ignoredRom = null;
        _lastSentRom = null;
        _learnedReaderRom = false;
        UpdateUi("Reader: reconnecting...", "iButton: waiting...", "Activity: reconnect", SystemIcons.Warning, true);
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
                    UpdateUi("Reader: not connected", "iButton: waiting...", "Activity: searching", SystemIcons.Warning, false);
                    return;
                }

                _ignoredRom = null;
                _lastSentRom = null;
                _learnedReaderRom = false;
                UpdateUi($"Reader: connected (USB #{_adapter.PortNum})", "iButton: waiting...", "Activity: connected", SystemIcons.Information, true);
            }

            var result = _adapter.Reset();
            var presentNow = result is TMEXResetResult.Presence or TMEXResetResult.Alarm;

            if (!presentNow)
            {
                UpdateUi($"Reader: connected (USB #{_adapter.PortNum})", "iButton: none", "Activity: idle", SystemIcons.Information, false);
                return;
            }

            byte[] rom = new byte[8];
            if (!_adapter.SearchNext(rom, 0, false))
            {
                UpdateUi($"Reader: connected (USB #{_adapter.PortNum})", "iButton: reading...", "Activity: search failed", SystemIcons.Warning, false);
                return;
            }

            string romHex = Program.FormatROM(rom);
            if (!Program.CheckCRC8(rom))
            {
                UpdateUi($"Reader: connected (USB #{_adapter.PortNum})", "iButton: invalid CRC", "Activity: ignored", SystemIcons.Warning, false);
                return;
            }

            if (!_learnedReaderRom)
            {
                _ignoredRom = romHex;
                _learnedReaderRom = true;
                UpdateUi($"Reader: connected (USB #{_adapter.PortNum})", "iButton: reader ignored", "Activity: ready", SystemIcons.Information, false);
                return;
            }

            if (romHex == _ignoredRom)
            {
                UpdateUi($"Reader: connected (USB #{_adapter.PortNum})", "iButton: reader ignored", "Activity: waiting", SystemIcons.Information, false);
                return;
            }

            if (!string.Equals(_lastSentRom, romHex, StringComparison.OrdinalIgnoreCase))
            {
                KeyboardSimulator.SendROM(romHex);
                _lastSentRom = romHex;
                UpdateUi($"Reader: connected (USB #{_adapter.PortNum})", $"iButton: {romHex}", "Activity: sent", SystemIcons.Information, true);
                _notifyIcon.ShowBalloonTip(1000, "1wireHID", $"iButton sent: {romHex}", ToolTipIcon.Info);
            }
        }
        catch
        {
            _adapter?.Dispose();
            _adapter = null;
            UpdateUi("Reader: not connected", "iButton: waiting...", "Activity: reconnect", SystemIcons.Warning, false);
        }
    }

    private void UpdateUi(string reader, string device, string activity, Icon icon, bool reconnectEnabled)
    {
        _readerStatusItem.Text = reader;
        _deviceStatusItem.Text = device;
        _activityStatusItem.Text = activity;
        _reconnectItem.Enabled = reconnectEnabled;
        _notifyIcon.Icon = icon;
        _notifyIcon.Text = TrimTooltip($"1wireHID | {reader} | {device}");
    }

    private static string TrimTooltip(string text) => text.Length <= 63 ? text : text[..63];
}
