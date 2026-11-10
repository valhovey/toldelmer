using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NinaHaTelemetry;
using NinaHaTelemetry.Mqtt;
using NinaHaTelemetry.Phd2;

string fixture = args[0], outDir = args[1];
Directory.CreateDirectory(outDir);
int warns = 0;
Log.Info = m => Console.WriteLine($"    [info] {m}");
Log.Warn = m => { warns++; Console.WriteLine($"    [warn] {m}"); };
bool ok = true;
void Check(bool cond, string what) { Console.WriteLine($"  {(cond ? "PASS" : "FAIL")}  {what}"); ok &= cond; }

Console.WriteLine("1. GuideFrame on the real PHD2 FITS");
var img = GuideFrame.ParseFits(File.ReadAllBytes(fixture));
var (w, h, bgr) = GuideFrame.Render(img, 800, (960, 540), (967, 535));
var ppm = Path.Combine(outDir, "guide.ppm");
using (var f = File.Create(ppm)) { var hdr = Encoding.ASCII.GetBytes($"P6 {w} {h} 255\n"); f.Write(hdr); 
    var rgb = new byte[bgr.Length]; for (int i = 0; i < bgr.Length; i += 3) { rgb[i] = bgr[i + 2]; rgb[i + 1] = bgr[i + 1]; rgb[i + 2] = bgr[i]; } f.Write(rgb); }
Check(img.Width == 1920 && img.Height == 1080, $"parsed {img.Width}x{img.Height}, full scale {img.FullScale}");
Check(w == 640 && h == 360, $"rendered {w}x{h}");

static GuideFrame.Image Synth(Func<int, float> f) {
    var p = new float[1920 * 1080]; for (int i = 0; i < p.Length; i++) p[i] = f(i);
    return new GuideFrame.Image { Width = 1920, Height = 1080, Pixels = p, FullScale = 65535 };
}
static double Mean(byte[] b) { double s = 0; for (int i = 0; i < b.Length; i++) s += b[i]; return s / b.Length; }
var sat = GuideFrame.Render(Synth(_ => 65520), 800, null, null);
Check(Mean(sat.Bgr) > 250, $"saturated flat frame renders white (mean {Mean(sat.Bgr):F0})");
var blank = GuideFrame.Render(Synth(_ => 0), 800, null, null);
Check(Mean(blank.Bgr) < 5, $"zero frame renders black (mean {Mean(blank.Bgr):F0})");
var clipped = GuideFrame.Render(Synth(i => i % 1000 < 100 ? 40000 : 65520), 800, null, null);
Check(clipped.Bgr.Min() < 100 && clipped.Bgr.Max() > 200, $"mostly clipped frame keeps contrast ({clipped.Bgr.Min()}..{clipped.Bgr.Max()})");
var night = GuideFrame.Render(Synth(i => 1200 + (i * 7919 % 13) + (i % 1920 == 900 && i / 1920 == 500 ? 30000 : 0)), 800, null, null);
Check(Mean(night.Bgr) is > 30 and < 110, $"faint night background stretched to dark grey (mean {Mean(night.Bgr):F0})");

async Task MqttOffline(string label, string host, int port) {
    Console.WriteLine($"2. MQTT {label} ({host}:{port})");
    int w0 = warns;
    var d = new Discovery("homeassistant", "nina_test", "NINA test", "0", includeGuideFrame: true);
    var link = new MqttLink(new MqttSettings(host, port, false, "u", "p", "nina_test"), d);
    var sw = Stopwatch.StartNew();
    link.Start();
    Check(sw.ElapsedMilliseconds < 50, $"Start() returned in {sw.ElapsedMilliseconds} ms");
    sw.Restart();
    for (int i = 0; i < 10000; i++) link.Publish($"nina/nina_test/t{i % 50}", "{\"x\":1}");
    Check(sw.ElapsedMilliseconds < 200, $"10,000 Publish() calls in {sw.ElapsedMilliseconds} ms (never blocks)");
    await Task.Delay(TimeSpan.FromSeconds(13));
    Check(!link.IsConnected, "not connected, as expected");
    Check(warns - w0 == 1, $"{warns - w0} warning logged in 13 s (first failure reported, repeats throttled)");
    sw.Restart();
    await link.DisposeAsync();
    Check(sw.ElapsedMilliseconds < 1500, $"DisposeAsync() in {sw.ElapsedMilliseconds} ms");
    Log.Reset("mqtt");
}
await MqttOffline("unroutable, no LAN (black hole)", "192.0.2.1", 1883);
await MqttOffline("connection refused", "127.0.0.1", 1);
await MqttOffline("DNS failure", "ha.does-not-exist.invalid", 1883);

async Task Phd2Missing(string label, string host, int port) {
    Console.WriteLine($"3. PHD2 {label} ({host}:{port})");
    int w0 = warns, frames = 0;
    var c = new Phd2Client(host, port, TimeSpan.FromSeconds(1), 800, () => true, () => true, _ => frames++);
    var sw = Stopwatch.StartNew(); c.Start();
    Check(sw.ElapsedMilliseconds < 50, $"Start() returned in {sw.ElapsedMilliseconds} ms");
    await Task.Delay(TimeSpan.FromSeconds(8));
    Check(!c.IsConnected && frames == 0, $"not connected, {frames} frames");
    Check(warns - w0 <= 1, $"{warns - w0} warning(s) logged in 8 s (throttled)");
    sw.Restart(); await c.DisposeAsync();
    Check(sw.ElapsedMilliseconds < 3500, $"DisposeAsync() in {sw.ElapsedMilliseconds} ms");
    Log.Reset("phd2");
}
await Phd2Missing("not running", "127.0.0.1", 4400);
await Phd2Missing("host unreachable", "192.0.2.1", 4400);

Console.WriteLine("4. fake PHD2 server");
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
int fakePort = ((IPEndPoint)listener.LocalEndpoint).Port;
var tempFiles = new List<string>();
var accepted = new List<TcpClient>();
var serverCts = new CancellationTokenSource();
var server = Task.Run(async () => {
    while (!serverCts.IsCancellationRequested) {
        TcpClient cl;
        try { cl = await listener.AcceptTcpClientAsync(serverCts.Token); } catch { return; }
        lock (accepted) accepted.Add(cl);
        _ = Task.Run(async () => {
            using var cc = cl; var s = cc.GetStream();
            var r = new StreamReader(s); var wr = new StreamWriter(s) { AutoFlush = true, NewLine = "\r\n" };
            await wr.WriteLineAsync("{\"Event\":\"Version\",\"PHDVersion\":\"fake\"}");
            await wr.WriteLineAsync("{\"Event\":\"AppState\",\"State\":\"Guiding\"}");
            var steps = Task.Run(async () => { try { while (true) { await wr.WriteLineAsync("{\"Event\":\"GuideStep\",\"dx\":7.0,\"dy\":-5.0}"); await Task.Delay(500); } } catch { } });
            string line;
            try {
                while ((line = await r.ReadLineAsync()) != null) {
                    var req = Newtonsoft.Json.Linq.JObject.Parse(line); var id = (int)req["id"]; var m = (string)req["method"];
                    string result = m switch {
                        "get_app_state" => "\"Guiding\"",
                        "get_lock_position" => "[960.0,540.0]",
                        "save_image" => SaveCopy(),
                        _ => "null" };
                    await wr.WriteLineAsync($"{{\"jsonrpc\":\"2.0\",\"result\":{result},\"id\":{id}}}");
                }
            } catch { }
        });
    }
});
string SaveCopy() { var p = Path.Combine(Path.GetTempPath(), $"phdfake{Guid.NewGuid():N}.tmp"); File.Copy(fixture, p); lock (tempFiles) tempFiles.Add(p);
                    return Newtonsoft.Json.JsonConvert.SerializeObject(new { filename = p }); }

bool mqttUp = false; int published = 0; byte[] lastBgr = null; int lw = 0, lh = 0;
bool phd2Selected = false;
var client = new Phd2Client("127.0.0.1", fakePort, TimeSpan.FromSeconds(1), 800, () => phd2Selected, () => mqttUp,
    frame => { published++; lastBgr = frame.Bgr; lw = frame.Width; lh = frame.Height; });
client.Start();
await Task.Delay(TimeSpan.FromSeconds(3));
lock (accepted) Check(accepted.Count == 0 && client.AppState == Phd2Client.Inactive, $"no connection while NINA's guider is not PHD2 (AppState={client.AppState})");
phd2Selected = true;
await Task.Delay(TimeSpan.FromSeconds(6.5));
Check(client.IsConnected && client.AppState == "Guiding", $"connected, AppState={client.AppState}");
Check(published == 0 && tempFiles.Count == 0, "no capture while MQTT is down (no FITS churn)");
mqttUp = true;
await Task.Delay(TimeSpan.FromSeconds(3.5));
Check(published >= 2, $"{published} frames published once MQTT is up");
Check(tempFiles.All(p => !File.Exists(p)), $"all {tempFiles.Count} temp FITS deleted");
if (lastBgr != null) {
    using var f = File.Create(Path.Combine(outDir, "guide_from_fake_phd2.ppm"));
    f.Write(Encoding.ASCII.GetBytes($"P6 {lw} {lh} 255\n"));
    var rgb = new byte[lastBgr.Length]; for (int i = 0; i < rgb.Length; i += 3) { rgb[i] = lastBgr[i + 2]; rgb[i + 1] = lastBgr[i + 1]; rgb[i + 2] = lastBgr[i]; } f.Write(rgb);
}
Console.WriteLine("   killing the fake PHD2 mid-session");
serverCts.Cancel(); listener.Stop();
lock (accepted) foreach (var a in accepted) a.Close();
await Task.Delay(TimeSpan.FromSeconds(4));
Check(!client.IsConnected && client.AppState is Phd2Client.Disconnected, $"noticed PHD2 went away (AppState={client.AppState})");
var sw2 = Stopwatch.StartNew(); await client.DisposeAsync();
Check(sw2.ElapsedMilliseconds < 3500, $"DisposeAsync() in {sw2.ElapsedMilliseconds} ms");

Console.WriteLine(ok ? "\nALL PASS" : "\nSOME CHECKS FAILED");
return ok ? 0 : 1;
