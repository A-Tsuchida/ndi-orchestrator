if (!ndiWrapper.NdiLibrary.Initialize())
{
  Console.Error.WriteLine("Failed to initialize NDI library. Ensure your CPU supports SSE4.2 and that the NDI 6 runtime is installed.");
  Environment.Exit(1);
}

DoDiscovery();

static void DoDiscovery()
{
  var limit = 10;
  Console.WriteLine("Looking for NDI sources for up to {0} seconds...", limit);

  var res = ndiWrapper.NdiCameraDiscovery.Discover(TimeSpan.FromSeconds(limit), true, null, ["192.168.18.55"]);

  Console.WriteLine("Found {0} sources:", res.Count);

  foreach (var item in res)
  {
    Console.WriteLine("{0}@{1}", item.Name, item.UrlAddress);
  }
}

static void DoControl()
{
  var source = new ndiWrapper.NdiSource("XC-446961 (Virtual PTZ Camera)", "169.254.201.128:5961");

  var camera = new ndiWrapper.NdiPtzCamera(source);

  camera.Zoom(0.5f);
  Thread.Sleep(2000);

  camera.ZoomSpeed(1f);
  Thread.Sleep(500);
  camera.ZoomSpeed(0f);
}
