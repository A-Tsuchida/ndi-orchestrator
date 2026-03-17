using System.Runtime.InteropServices;

if (!ndiWrapper.NdiLibrary.Initialize())
{
  Console.Error.WriteLine("Failed to initialize NDI library. Ensure your CPU supports SSE4.2 and that the NDI 6 runtime is installed.");
  Environment.Exit(1);
}

  var limit = 10;
  Console.WriteLine("Looking for NDI sources for up to {0} seconds...", limit);

  var res = ndiWrapper.NdiCameraDiscovery.Discover(TimeSpan.FromSeconds(limit), true, null, ["192.168.18.55"]);

  Console.WriteLine("Found {0} sources:", res.Count);

Console.WriteLine("Select the device do you want to control:");
int opt = 0;
ConsoleKey key;

while (true)
  {
  for (var i = 0; i < res.Count; i++)
  {
    if (i == opt)
    {
      Console.BackgroundColor = ConsoleColor.Gray;
      Console.ForegroundColor = ConsoleColor.Black;
  }
    Console.WriteLine("{0}@{1}", res[i].Name, res[i].UrlAddress);
    Console.ResetColor();
}
  switch (Console.ReadKey(true).Key)
  {
    case ConsoleKey.UpArrow:
      if (opt > 0) opt -= 1;
      break;
    case ConsoleKey.DownArrow:
      if (opt < res.Count - 1) opt += 1;
      break;
    case ConsoleKey.Enter:
      DoControl(res[opt]);
      return;
  }

  var (left, top) = Console.GetCursorPosition();
  Console.SetCursorPosition(left, top - res.Count);
}

static void DoControl(ndiWrapper.NdiSource source)
{
  Console.Clear();
  var camera = new ndiWrapper.NdiPtzCamera(source);

  var state = camera.TryReceiveMetadata(500);
  Console.WriteLine("Current state: {0}", state);

  (float pan, float tilt) prev = (0f, 0f);
  
  var inputHandle = GetStdHandle(-10); // STD_INPUT_HANDLE

  while (true)
  {
    // Track held keys
    var heldKeys = GetKeys(inputHandle);
    float pan = 0f, tilt = 0f;
    if (heldKeys.Contains(ConsoleKey.W)) tilt += 0.5f;
    if (heldKeys.Contains(ConsoleKey.S)) tilt -= 0.5f;
    if (heldKeys.Contains(ConsoleKey.A)) pan -= 0.5f;
    if (heldKeys.Contains(ConsoleKey.D)) pan += 0.5f;

    if (prev.pan != pan || prev.tilt != tilt)
    {
      Console.WriteLine("Pan: {0} | Tilt: {1}", pan, tilt);
      camera.PanTiltSpeed(pan, tilt);
      prev = (pan, tilt);
    }

    Thread.Sleep(16); // ~60 polls/sec
  }
}

static HashSet<ConsoleKey> GetKeys(nint hConsoleInput)
{
  HashSet<ConsoleKey> heldKeys = [];

  var buf = new INPUT_RECORD[16];
  ReadConsoleInput(hConsoleInput, buf, buf.Length, out int read);
  for (int i = 0; i < read; i++)
  {
    if (buf[i].EventType == 1) // KEY_EVENT
    {
      var ke = buf[i].KeyEvent;
      var key = (ConsoleKey)ke.VirtualKeyCode;
      if (ke.bKeyDown != 0)
        heldKeys.Add(key);
      else
        heldKeys.Remove(key);
    }
  }

  return heldKeys;
}

[DllImport("kernel32.dll")]
static extern nint GetStdHandle(int nStdHandle);

[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
static extern bool ReadConsoleInput(nint hConsoleInput, [Out] INPUT_RECORD[] lpBuffer, int nLength, out int lpNumberOfEventsRead);

[StructLayout(LayoutKind.Explicit)]
struct INPUT_RECORD
{
  [FieldOffset(0)] public short EventType;
  [FieldOffset(4)] public KEY_EVENT_RECORD KeyEvent;
}

[StructLayout(LayoutKind.Sequential)]
struct KEY_EVENT_RECORD
{
  public int bKeyDown;
  public short wRepeatCount;
  public short VirtualKeyCode;
  public short VirtualScanCode;
  public char UnicodeChar;
  public int dwControlKeyState;
}