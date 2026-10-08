using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KuaaApp.Host
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private WebView2 _webView;
        private string _webRoot;
        private HttpListener _localServer;
        private CancellationTokenSource _serverCts;
        private string _baseUrl;

        public MainForm()
        {
            Text = "kuaa Estudante";
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = System.Drawing.Color.FromArgb(0x17, 0x32, 0x4D);

            _webRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

            // O app web usa caminhos absolutos (/assets/...) e ES modules, que nao
            // funcionam sobre file:// (e quebram com espacos no caminho, como em
            // "Program Files"). Servimos a pasta via http://localhost em vez disso.
            _baseUrl = StartLocalServer();

            InitializeWebView();
        }

        private static int FreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private string StartLocalServer()
        {
            int port = FreeTcpPort();
            _localServer = new HttpListener();
            _localServer.Prefixes.Add($"http://127.0.0.1:{port}/");
            _localServer.Prefixes.Add($"http://localhost:{port}/");
            _localServer.Start();
            _serverCts = new CancellationTokenSource();
            Task.Run(() => ServeLoop(_serverCts.Token));
            return $"http://127.0.0.1:{port}";
        }

        private async Task ServeLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext context = null;
                try
                {
                    context = await _localServer.GetContextAsync();
                }
                catch
                {
                    return; // Listener parado no fechamento do app.
                }
                _ = Task.Run(() => ServeRequest(context));
            }
        }

        private void ServeRequest(HttpListenerContext context)
        {
            try
            {
                string rawPath = context.Request.Url.AbsolutePath;
                string relative = Uri.UnescapeDataString(rawPath).TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                if (string.IsNullOrEmpty(relative))
                    relative = "index.html";
                string fullPath = Path.GetFullPath(Path.Combine(_webRoot, relative));
                // Trava anti path-traversal + fallback SPA (rotas como /404 caem no index).
                if (!fullPath.StartsWith(_webRoot, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = 403;
                    context.Response.Close();
                    return;
                }
                if (!File.Exists(fullPath) && string.IsNullOrEmpty(Path.GetExtension(fullPath)))
                    fullPath = Path.Combine(_webRoot, "index.html");
                if (!File.Exists(fullPath))
                {
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    return;
                }
                context.Response.ContentType = ContentTypeFor(fullPath);
                using (var stream = File.OpenRead(fullPath))
                {
                    context.Response.ContentLength64 = stream.Length;
                    stream.CopyTo(context.Response.OutputStream);
                }
                context.Response.Close();
            }
            catch
            {
                try { context.Response.StatusCode = 500; context.Response.Close(); } catch { /* ignora */ }
            }
        }

        private static string ContentTypeFor(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".html": return "text/html; charset=utf-8";
                case ".js": return "text/javascript; charset=utf-8";
                case ".css": return "text/css; charset=utf-8";
                case ".json": return "application/json; charset=utf-8";
                case ".webmanifest": return "application/manifest+json";
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".webp": return "image/webp";
                case ".svg": return "image/svg+xml";
                case ".ico": return "image/vnd.microsoft.icon";
                case ".woff2": return "font/woff2";
                case ".woff": return "font/woff";
                case ".ttf": return "font/ttf";
                default: return "application/octet-stream";
            }
        }

        private async void InitializeWebView()
        {
            _webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(_webView);

            // O diretorio de instalacao do MSIX e somente-leitura: os dados do
            // WebView2 (cache, cookies, LocalStorage do app) ficam em LocalAppData,
            // que dentro do container MSIX redireciona para o armazenamento
            // privado do app. (Antes era _webRoot/WebView2Data e quebrava com
            // CO_E_SERVER_EXEC_FAILURE na versao da loja.)
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "wavizo", "Kuaa", "EBWebView");
            Directory.CreateDirectory(userDataDir);
            var env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            _webView.CoreWebView2.Navigate(_baseUrl + "/index.html");

            _webView.CoreWebView2.NavigationCompleted += (sender, args) =>
            {
                if (!args.IsSuccess)
                {
                    _webView.CoreWebView2.Navigate(_baseUrl + "/index.html");
                }
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try { _serverCts?.Cancel(); } catch { /* ignora */ }
            try
            {
                if (_localServer != null && _localServer.IsListening)
                {
                    _localServer.Stop();
                    _localServer.Close();
                }
            }
            catch { /* ignora */ }
            _webView?.CoreWebView2?.TrySuspendAsync();
            _webView?.Dispose();
            base.OnFormClosing(e);
        }
    }
}