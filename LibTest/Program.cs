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

do
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
  switch (key = Console.ReadKey(true).Key)
  {
    case ConsoleKey.UpArrow:
      if (opt > 0) opt -= 1;
      break;
    case ConsoleKey.DownArrow:
      if (opt < res.Count - 1) opt += 1;
      break;
    case ConsoleKey.Enter:
      DoControl(res[opt]);
      break;
  }

  var (left, top) = Console.GetCursorPosition();
  Console.SetCursorPosition(left, top - res.Count);
} while (key != ConsoleKey.Enter);

static void DoControl(ndiWrapper.NdiSource source)
{
  Console.Clear();
  var camera = new ndiWrapper.NdiPtzCamera(source);

  var state = camera.TryReceiveMetadata(500);
  Console.WriteLine("Current state: {0}", state);

  (float pan, float tilt) prevPos = (0f, 0f);
  float prevZoom = 0f;
  float prevFocus = 0f;
  
  var inputHandle = GetStdHandle(-10); // STD_INPUT_HANDLE

  while (true)
  {
    // Track held keys
    var heldKeys = GetKeys(inputHandle);
    float pan = 0f, tilt = 0f, zoom = 0f, focus = 0f;
    
    if (heldKeys.Contains(ConsoleKey.W)) tilt += 5f;
    if (heldKeys.Contains(ConsoleKey.S)) tilt -= 5f;
    if (heldKeys.Contains(ConsoleKey.A)) pan -= 5f;
    if (heldKeys.Contains(ConsoleKey.D)) pan += 5f;
    if (heldKeys.Contains(ConsoleKey.U)) zoom += 1f;
    if (heldKeys.Contains(ConsoleKey.I)) zoom += 1f;
    if (heldKeys.Contains(ConsoleKey.J)) focus += 1f;
    if (heldKeys.Contains(ConsoleKey.K)) focus += 1f;

    if (prevPos.pan != pan || prevPos.tilt != tilt)
    {
      camera.PanTiltSpeed(pan, tilt);
      prevPos = (pan, tilt);
    }
    if (zoom != prevZoom)
    {
      camera.ZoomSpeed(zoom);
      prevZoom = zoom;
    }
    if (focus != prevFocus)
    {
      camera.FocusSpeed(focus);
      prevFocus = focus;
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