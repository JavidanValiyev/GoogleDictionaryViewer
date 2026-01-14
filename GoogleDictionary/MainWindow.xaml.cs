using System.Net.Http;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace GoogleDictionary;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private static readonly HttpClient client = new HttpClient();

    public MainWindow()
    {
        InitializeComponent();
        InitializeWebView();
    }

    async void InitializeWebView()
    {
        await webView.EnsureCoreWebView2Async(null);

        // C# tarafında JavaScript'ten gelen mesajları dinliyoruz
        webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

        // HTML İçeriği
        string htmlContent = GetHtmlContent();
        webView.CoreWebView2.NavigateToString(htmlContent);
    }

    private async void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // JavaScript'ten gelen kelimeyi al (JSON formatında gelir, string'e çeviriyoruz)
        string word = e.TryGetWebMessageAsString();

        if (string.IsNullOrEmpty(word)) return;

        try
        {
            // C# üzerinden doğrudan istek atıyoruz (Proxy gerekmez)
            string url = $"https://googledictionary.freecollocation.com/meaning?word={Uri.EscapeDataString(word)}";
            string responseBody = await client.GetStringAsync(url);

            // Alınan ham HTML verisini tekrar JavaScript'e gönderiyoruz
            // 'PostWebMessageAsJson' kullanarak veriyi güvenli bir paket halinde yolluyoruz
            webView.CoreWebView2.PostWebMessageAsString(responseBody);
        }
        catch (Exception ex)
        {
            webView.CoreWebView2.ExecuteScriptAsync(
                $"document.getElementById('results').innerHTML = 'Hata: {ex.Message}';");
        }
    }

    private string GetHtmlContent()
    {
        return @"
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body { font-family: 'Segoe UI', sans-serif; padding: 20px; background: #f4f4f9; }
                    .search-container { display: flex; gap: 10px; margin-bottom: 20px; position: sticky; top: 0; background: #f4f4f9; padding: 10px 0; }
                    input { flex: 1; padding: 12px; border: 2px solid #ddd; border-radius: 6px; outline: none; font-size: 16px; }
                    input:focus { border-color: #4285f4; }
                    button { padding: 12px 20px; background: #4285f4; color: white; border: none; cursor: pointer; border-radius: 6px; font-weight: bold; }
                    .def-item { background: white; padding: 15px; margin-bottom: 12px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.05); border-left: 5px solid #4285f4; }
                    .definition { font-weight: bold; color: #333; font-size: 17px; }
                    .example { color: #666; font-style: italic; margin-top: 8px; background: #f9f9f9; padding: 5px; border-radius: 4px; }
                </style>
            </head>
            <body>
                <div class='search-container'>
                    <input type='text' id='wordInput' placeholder='Type word...' onkeydown=""if(event.key === 'Enter') sendToCSharp()"">
                    <button onclick='sendToCSharp()'>Search</button>
                </div>
                <div id='results'></div>

                <script>
                    // 1. ADIM: Kelimeyi C# tarafına gönder
                    function sendToCSharp() {
                        const word = document.getElementById('wordInput').value.trim();
                        if (!word) return;
                        
                        document.getElementById('results').innerHTML = 'Searching...';
                        
                        // WebView2 üzerinden C#'a mesaj yolla
                        window.chrome.webview.postMessage(word);
                    }

                    // 2. ADIM: C# tarafından gelen ham HTML cevabını dinle
                    window.chrome.webview.addEventListener('message', event => {
                        const rawHtml = event.data;
                        const parsedData = parseHtml(rawHtml);
                        displayResults(parsedData);
                    });

                    // Senin yazdığın meşhur parser
                    function parseHtml(htmlContent) {
                        const parser = new DOMParser();
                        const doc = parser.parseFromString(htmlContent, 'text/html');
                        const listItems = doc.querySelectorAll('ol > div > li');
                        const results = [];
                        
                        listItems.forEach(li => {
                            const definition = li.childNodes[0].textContent.trim();
                            const exampleLi = li.querySelector('.std ul li');
                            const example = exampleLi ? exampleLi.textContent.replace('-', '').trim() : '';
                            if (definition) results.push({ definition, example });
                        });
                        return results;
                    }

                    function displayResults(data) {
                        const resultsDiv = document.getElementById('results');
                        if (data.length === 0) {
                            resultsDiv.innerHTML = 'Sonuç bulunamadı.';
                            return;
                        }
                        resultsDiv.innerHTML = data.map(item => `
                            <div class='def-item'>
                                <div class='definition'>${item.definition}</div>
                                ${item.example ? `<div class='example'>${item.example}</div>` : ''}
                            </div>
                        `).join('');
                        document.getElementById('wordInput').select();
                    }
                </script>
            </body>
            </html>";
    }
}