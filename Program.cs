using System.ServiceProcess;
using System.Text;

namespace OneWireHID;

internal class Program
{
    private const string VERSION = "0.1.1";
    private static bool _running = true;
    private static bool _verbose = false;
    private static bool _serviceMode = false;

    static void Main(string[] args)
    {
        Console.WriteLine($"1wireHID v{VERSION} - iButton Keyboard Emulator");
        Console.WriteLine("==========================================");
        Console.WriteLine();

        int portType = TMEXConstants.PORT_TYPE_USB;
        int portNum = 0;
        string? manualRom = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "-usb":
                    portType = TMEXConstants.PORT_TYPE_USB;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int pn)) { portNum = pn; i++; }
                    break;
                case "-com":
                    portType = TMEXConstants.PORT_TYPE_SERIAL;
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int pn2)) { portNum = pn2; i++; }
                    break;
                case "-v":
                case "-verbose":
                    _verbose = true;
                    break;
                case "-service":
                case "-svc":
                    _serviceMode = true;
                    break;
                case "-rom":
                    if (i + 1 < args.Length) { manualRom = args[i + 1]; i++; }
                    break;
                case "-h":
                case "-help":
                    ShowHelp();
                    return;
            }
        }

        if (manualRom != null)
        {
            Console.WriteLine($"Sending manual ROM: {manualRom}");
            KeyboardSimulator.SendROM(manualRom);
            Console.WriteLine("Done.");
            return;
        }

        if (_serviceMode)
        {
            RunAsService(portType, portNum);
        }
        else
        {
            RunTerminal(portType, portNum);
        }
    }

    static void RunTerminal(int portType, int portNum)
    {
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; _running = false; };

        using var adapter = new TMEXAdapter(portType, portNum);

        if (!adapter.Open())
        {
            Console.WriteLine("ERROR: Could not open 1-Wire adapter.");
            Console.WriteLine("Make sure the 1-Wire drivers (IBFS64.dll) are installed and");
            Console.WriteLine("the DS9490R/DS9097U adapter is connected.");
            return;
        }

        Console.WriteLine($"[OK] Adapter opened: {adapter.AdapterDescription}");
        Console.WriteLine($"[OK] Port type: {portType}, Port num: {portNum}");
        Console.WriteLine();
        Console.WriteLine("=== Terminal Mode - Press Ctrl+C to exit ===");
        Console.WriteLine();

        byte[] lastRom = new byte[8];
        Array.Fill(lastRom, (byte)0);
        bool lastWasPresent = false;
        int pollCount = 0;

        while (_running)
        {
            try
            {
                pollCount++;
                var result = adapter.Reset();
                bool presentNow = result == TMEXResetResult.Presence || result == TMEXResetResult.Alarm;

                if (_verbose)
                {
                    string resetStr = result switch
                    {
                        TMEXResetResult.NoPresence => "NO PRESENCE",
                        TMEXResetResult.Presence => "PRESENCE",
                        TMEXResetResult.Alarm => "ALARM",
                        TMEXResetResult.Short => "SHORT",
                        _ => $"UNKNOWN({(int)result})"
                    };
                    Console.WriteLine($"[Poll #{pollCount}] Reset: {resetStr}");
                }

                if (presentNow && !lastWasPresent)
                {
                    Console.WriteLine();
                    Console.WriteLine(">>> iButton DETECTED <<<");

                    byte[] rom = new byte[8];
                    if (adapter.SearchNext(rom, 0, false))
                    {
                        string romHex = FormatROM(rom);
                        string familyCode = $"0x{rom[0]:X2}";

                        Console.WriteLine($"  Family Code : {familyCode}");
                        Console.WriteLine($"  Serial No   : {romHex.Substring(2)}");
                        Console.WriteLine($"  Full ROM    : {romHex}");
                        Console.WriteLine($"  CRC         : 0x{rom[7]:X2} (valid: {CheckCRC8(rom)})");
                        Console.WriteLine();

                        Console.Write("  Sending as keyboard input... ");
                        KeyboardSimulator.SendROM(romHex);
                        Console.WriteLine("DONE");
                    }
                }

                lastWasPresent = presentNow;

                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                Thread.Sleep(500);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Exiting.");
    }

    static void RunAsService(int portType, int portNum)
    {
        var servicesToRun = new ServiceBase[]
        {
            new OneWireHIDService(portType, portNum)
        };
        ServiceBase.Run(servicesToRun);
    }

    static string FormatROM(byte[] rom)
    {
        var sb = new StringBuilder(16);
        for (int i = 7; i >= 0; i--)
            sb.Append(rom[i].ToString("X2"));
        return sb.ToString();
    }

    static bool CheckCRC8(byte[] rom)
    {
        byte crc = 0;
        for (int i = 0; i < 7; i++)
        {
            crc = Crc8Table[crc ^ rom[i]];
        }
        return crc == rom[7];
    }

    static readonly byte[] Crc8Table = new byte[]
    {
        0x00, 0x5E, 0xBC, 0xE2, 0x61, 0x3F, 0xDD, 0x83,
        0xC2, 0x9C, 0x7E, 0x20, 0xA3, 0xFD, 0x1F, 0x41,
        0x9D, 0xC3, 0x21, 0x7F, 0xFC, 0xA2, 0x40, 0x1E,
        0x5F, 0x01, 0xE3, 0xBD, 0x3E, 0x60, 0x82, 0xDC,
        0x23, 0x7D, 0x9F, 0xC1, 0x42, 0x1C, 0xFE, 0xA0,
        0xE1, 0xBF, 0x5D, 0x03, 0x80, 0xDE, 0x3C, 0x62,
        0xBE, 0xE0, 0x02, 0x5C, 0xDF, 0x81, 0x63, 0x3D,
        0x7C, 0x22, 0xC0, 0x9E, 0x1D, 0x43, 0xA1, 0xFF,
        0x46, 0x18, 0xFA, 0xA4, 0x27, 0x79, 0x9B, 0xC5,
        0x84, 0xDA, 0x38, 0x66, 0xE5, 0xBB, 0x59, 0x07,
        0xDB, 0x85, 0x67, 0x39, 0xBA, 0xE4, 0x06, 0x58,
        0x19, 0x47, 0xA5, 0xFB, 0x78, 0x26, 0xC4, 0x9A,
        0x65, 0x3B, 0xD9, 0x87, 0x04, 0x5A, 0xB8, 0xE6,
        0xA7, 0xF9, 0x1B, 0x45, 0xC6, 0x98, 0x7A, 0x24,
        0xF8, 0xA6, 0x44, 0x1A, 0x99, 0xC7, 0x25, 0x7B,
        0x3A, 0x64, 0x86, 0xD8, 0x5B, 0x05, 0xE7, 0xB9,
        0x8C, 0xD2, 0x30, 0x6E, 0xED, 0xB3, 0x51, 0x0F,
        0x4E, 0x10, 0xF2, 0xAC, 0x2F, 0x71, 0x93, 0xCD,
        0x11, 0x4F, 0xAD, 0xF3, 0x70, 0x2E, 0xCC, 0x92,
        0xD3, 0x8D, 0x6F, 0x31, 0xB2, 0xEC, 0x0E, 0x50,
        0xAF, 0xF1, 0x13, 0x4D, 0xCE, 0x90, 0x72, 0x2C,
        0x6D, 0x33, 0xD1, 0x8F, 0x0C, 0x52, 0xB0, 0xEE,
        0x32, 0x6C, 0x8E, 0xD0, 0x53, 0x0D, 0xEF, 0xB1,
        0xF0, 0xAE, 0x4C, 0x12, 0x91, 0xCF, 0x2D, 0x73,
        0xCA, 0x94, 0x76, 0x28, 0xAB, 0xF5, 0x17, 0x49,
        0x08, 0x56, 0xB4, 0xEA, 0x69, 0x37, 0xD5, 0x8B,
        0x57, 0x09, 0xEB, 0xB5, 0x36, 0x68, 0x8A, 0xD4,
        0x95, 0xCB, 0x29, 0x77, 0xF4, 0xAA, 0x48, 0x16,
        0xE9, 0xB7, 0x55, 0x0B, 0x88, 0xD6, 0x34, 0x6A,
        0x2B, 0x75, 0x97, 0xC9, 0x4A, 0x14, 0xF6, 0xA8,
        0x74, 0x2A, 0xC8, 0x96, 0x15, 0x4B, 0xA9, 0xF7,
        0xB6, 0xE8, 0x0A, 0x54, 0xD7, 0x89, 0x6B, 0x35
    };

    static void ShowHelp()
    {
        Console.WriteLine("Usage: 1wireHID [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -usb [n]     Use USB adapter (DS9490R), port number n (default: 0)");
        Console.WriteLine("  -com n       Use COM port adapter (DS9097U), port number n");
        Console.WriteLine("  -v, -verbose Verbose output showing raw driver data");
        Console.WriteLine("  -service     Run as Windows service");
        Console.WriteLine("  -rom <hex>   Send a specific ROM and exit (for testing)");
        Console.WriteLine("  -h, -help    Show this help");
        Console.WriteLine();
        Console.WriteLine("When running in terminal mode, touch an iButton to the reader and its ID will be");
        Console.WriteLine("typed as keyboard input followed by Enter.");
    }
}

public class OneWireHIDService : ServiceBase
{
    private readonly int _portType;
    private readonly int _portNum;
    private Thread? _workerThread;
    private bool _stopping = false;
    private byte[] _lastRom = new byte[8];
    private byte[] _currentRom = new byte[8];

    public OneWireHIDService(int portType, int portNum)
    {
        _portType = portType;
        _portNum = portNum;
        ServiceName = "OneWireHID";
        CanStop = true;
        CanShutdown = true;
    }

    protected override void OnStart(string[] args)
    {
        _workerThread = new Thread(ServiceWorker);
        _workerThread.IsBackground = true;
        _workerThread.Start();
    }

    protected override void OnStop()
    {
        _stopping = true;
        _workerThread?.Join(5000);
    }

    void ServiceWorker()
    {
        Array.Fill(_lastRom, (byte)0);
        Array.Fill(_currentRom, (byte)0);

        using var adapter = new TMEXAdapter(_portType, _portNum);

        if (!adapter.Open())
        {
            EventLog.WriteEntry("OneWireHID", "Could not open 1-Wire adapter.", EventLogEntryType.Error);
            return;
        }

        while (!_stopping)
        {
            try
            {
                var result = adapter.Reset();
                bool presentNow = result == TMEXResetResult.Presence || result == TMEXResetResult.Alarm;

                if (presentNow)
                {
                    if (adapter.SearchNext(_currentRom, 0, false))
                    {
                        bool isDifferent = false;
                        for (int i = 0; i < 8; i++)
                        {
                            if (_currentRom[i] != _lastRom[i])
                            {
                                isDifferent = true;
                                break;
                            }
                        }

                        if (isDifferent)
                        {
                            Array.Copy(_currentRom, _lastRom, 8);
                            string romHex = FormatROM(_currentRom);
                            KeyboardSimulator.SendROM(romHex);
                        }
                    }
                }

                Thread.Sleep(100);
            }
            catch
            {
                Thread.Sleep(500);
            }
        }

        adapter.Close();
    }

    static string FormatROM(byte[] rom)
    {
        var sb = new StringBuilder(16);
        for (int i = 7; i >= 0; i--)
            sb.Append(rom[i].ToString("X2"));
        return sb.ToString();
    }
}