using System.Runtime.InteropServices;

namespace OneWireHID;

internal static class TMEX64
{
    [DllImport("IBFS64.dll", EntryPoint = "TMExtendedStartSession")]
    public static extern int TMExtendedStartSession(short portNum, short portType, ref int sessionOptions);

    [DllImport("IBFS64.dll", EntryPoint = "TMValidSession")]
    public static extern short TMValidSession(int sessionHandle);

    [DllImport("IBFS64.dll", EntryPoint = "TMEndSession")]
    public static extern short TMEndSession(int sessionHandle);

    [DllImport("IBFS64.dll", EntryPoint = "TMFirst")]
    public static extern short TMFirst(int sessionHandle, byte[] stateBuffer);

    [DllImport("IBFS64.dll", EntryPoint = "TMNext")]
    public static extern short TMNext(int sessionHandle, byte[] stateBuffer);

    [DllImport("IBFS64.dll", EntryPoint = "TMSearch")]
    public static extern short TMSearch(int sessionHandle, byte[] stateBuffer, short doResetFlag, short skipResetOnSearchFlag, short searchCommand);

    [DllImport("IBFS64.dll", EntryPoint = "TMRom")]
    public static extern short TMRom(int sessionHandle, byte[] stateBuffer, short[] ROM);

    [DllImport("IBFS64.dll", EntryPoint = "TMSetup")]
    public static extern short TMSetup(int sessionHandle);

    [DllImport("IBFS64.dll", EntryPoint = "TMTouchReset")]
    public static extern short TMTouchReset(int sessionHandle);

    [DllImport("IBFS64.dll", EntryPoint = "TMTouchByte")]
    public static extern short TMTouchByte(int sessionHandle, short byteValue);

    [DllImport("IBFS64.dll", EntryPoint = "TMTouchBit")]
    public static extern short TMTouchBit(int sessionHandle, short bitValue);

    [DllImport("IBFS64.dll", EntryPoint = "TMBlockIO")]
    public static extern short TMBlockIO(int sessionHandle, byte[] dataBlock, short len);

    [DllImport("IBFS64.dll", EntryPoint = "TMBlockStream")]
    public static extern short TMBlockStream(int sessionHandle, byte[] dataBlock, short len);

    [DllImport("IBFS64.dll", EntryPoint = "TMClose")]
    public static extern short TMClose(int sessionHandle);

    [DllImport("IBFS64.dll", EntryPoint = "TMOneWireCom")]
    public static extern short TMOneWireCom(int sessionHandle, short command, short argument);

    [DllImport("IBFS64.dll", EntryPoint = "TMGetTypeVersion")]
    public static extern short TMGetTypeVersion(int portType, System.Text.StringBuilder sbuff);

    [DllImport("IBFS64.dll", EntryPoint = "Get_Version")]
    public static extern short Get_Version(System.Text.StringBuilder sbuff);

    [DllImport("IBFS64.dll", EntryPoint = "TMGetAdapterSpec")]
    public static extern short TMGetAdapterSpec(int sessionHandle, byte[] adapterSpec);

    [DllImport("IBFS64.dll", EntryPoint = "TMReadDefaultPort")]
    public static extern short TMReadDefaultPort(ref short portTypeRef, ref short portNumRef);
}

internal static class TMEXConstants
{
    public const int PORT_TYPE_USB = 6;
    public const int PORT_TYPE_SERIAL = 5;
    public const int SESSION_INFINITE = 1;
    public const int STATE_BUFFER_SIZE = 5120;
    public const short SEARCH_COMMAND = 0xF0;
}

public class TMEXAdapter : IDisposable
{
    private int _sessionHandle = -1;
    private readonly byte[] _stateBuffer;
    private int _portNum;
    private int _portType;
    private bool _disposed;

    public string AdapterDescription { get; private set; } = "";
    public int PortNum => _portNum;

    public TMEXAdapter(int portType = TMEXConstants.PORT_TYPE_USB, int portNum = 0)
    {
        _portType = portType;
        _portNum = portNum;
        _stateBuffer = new byte[TMEXConstants.STATE_BUFFER_SIZE];
    }

    public bool Open()
    {
        foreach (int candidatePort in EnumerateCandidatePorts())
        {
            int sessionOptions = TMEXConstants.SESSION_INFINITE;
            int sessionHandle = TMEX64.TMExtendedStartSession((short)candidatePort, (short)_portType, ref sessionOptions);

            if (sessionHandle <= 0)
                continue;

            if (TMEX64.TMSetup(sessionHandle) != 1)
            {
                TMEX64.TMEndSession(sessionHandle);
                continue;
            }

            var versionBuffer = new System.Text.StringBuilder(256);
            TMEX64.TMGetTypeVersion(_portType, versionBuffer);

            byte[] specBuffer = new byte[319];
            if (TMEX64.TMGetAdapterSpec(sessionHandle, specBuffer) > 0)
            {
                int i;
                for (i = 64; i < 319; i++)
                    if (specBuffer[i] == 0) break;
                AdapterDescription = System.Text.UTF8Encoding.UTF8.GetString(specBuffer, 64, i - 64);
            }

            TMEX64.TMFirst(sessionHandle, _stateBuffer);
            _sessionHandle = sessionHandle;
            _portNum = candidatePort;
            return true;
        }

        return false;
    }

    private IEnumerable<int> EnumerateCandidatePorts()
    {
        yield return _portNum;

        for (int i = 0; i < 16; i++)
        {
            if (i != _portNum)
                yield return i;
        }
    }

    public void Close()
    {
        if (_sessionHandle > 0)
        {
            TMEX64.TMClose(_sessionHandle);
            TMEX64.TMEndSession(_sessionHandle);
            _sessionHandle = -1;
        }
    }

    public TMEXResetResult Reset()
    {
        if (_sessionHandle < 0) throw new InvalidOperationException("Session not open");
        int rt = TMEX64.TMTouchReset(_sessionHandle);
        return (TMEXResetResult)rt;
    }

    public bool SearchNext(byte[] romBuffer, int offset, bool doReset = true)
    {
        if (_sessionHandle < 0) throw new InvalidOperationException("Session not open");

        short[] ROM = new short[8];
        short rt = TMEX64.TMSearch(_sessionHandle, _stateBuffer,
            (short)(doReset ? 1 : 0), (short)0, TMEXConstants.SEARCH_COMMAND);

        if (rt <= 0) return false;

        ROM[0] = 0;
        if (TMEX64.TMRom(_sessionHandle, _stateBuffer, ROM) != 1) return false;

        for (int i = 0; i < 8; i++)
            romBuffer[i + offset] = (byte)ROM[i];

        return true;
    }

    public bool IsValidRom(byte[] romBuffer, int offset)
    {
        if (romBuffer[offset + 7] != 0) return true;
        return false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
    }
}

public enum TMEXResetResult
{
    NoPresence = 0,
    Presence = 1,
    Alarm = 2,
    Short = 3
}
