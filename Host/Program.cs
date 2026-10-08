using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Reflection;
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

        public MainForm()
        {
            Text = "kuaa Estudante";
            WindowState = FormWindowState.Maximized;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = System.Drawing.Color.FromArgb(0x17, 0x32, 0x4D);

            _webRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            
            InitializeWebView();
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

            _webView.CoreWebView2.Navigate($"file:///{_webRoot.Replace("\\", "/")}/index.html");

            _webView.CoreWebView2.NavigationCompleted += (sender, args) =>
            {
                if (!args.IsSuccess)
                {
                    _webView.CoreWebView2.Navigate($"file:///{_webRoot.Replace("\\", "/")}/index.html");
                }
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _webView?.CoreWebView2?.TrySuspendAsync();
            _webView?.Dispose();
            base.OnFormClosing(e);
        }
    }
}