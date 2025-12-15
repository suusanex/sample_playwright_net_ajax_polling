using MessageStreamApp.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.IO;

namespace MessageStreamApp.PlaywrightTests;

public sealed class TestHost : IAsyncDisposable
{
    private IHost? _host;

    public Uri BaseAddress { get; private set; } = null!;

    public async Task StartAsync(IDictionary<string, string?> configuration)
    {
        // 前提条件の設定：アプリケーションのコンテンツルートパスを指定
        // テストから相対的に、ソースコードの MessageStreamApp フォルダーを指定
        var contentRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "MessageStreamApp"));
        
        // 前提条件の設定：WebアプリケーションのPathを設定（コンテンツ、静的ファイルなど）
        var options = new WebApplicationOptions
        {
            ContentRootPath = contentRoot,
            WebRootPath = Path.Combine(contentRoot, "wwwroot"),
            ApplicationName = typeof(Program).Assembly.FullName,
        };

        // 前処理：WebApplicationBuilderを作成してアプリケーションを構成
        var builder = WebApplication.CreateBuilder(options);
        
        // 前処理：ポート0を使用してアプリケーションをリッスン（OSが自動的に空きポートを割り当てる）
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        
        // 前提条件の設定：テストから指定された設定（モード、間隔、バッファ容量など）をアプリケーション設定に追加
        builder.Configuration.AddInMemoryCollection(configuration);
        
        // 前処理：アプリケーションのサービスを登録・設定
        AppConfiguration.ConfigureServices(builder);

        // 前処理：Webアプリケーションをビルド
        var app = builder.Build();
        
        // 前処理：アプリケーションのパイプライン（ミドルウェア）を設定
        AppConfiguration.ConfigurePipeline(app);

        // 前処理：Webアプリケーション（テストホスト）を起動
        await app.StartAsync();
        
        // 前処理：起動後のアプリケーションのURLを取得してBaseAddressに保存
        // このURLをPlaywrightのページで BaseURL として使用する
        var url = app.Urls.First();
        BaseAddress = new Uri(url);
        _host = app;
    }

    public async ValueTask DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    // 前提条件の設定用メソッド：テストに必要な設定を生成
    public static IDictionary<string, string?> CreateConfig(string mode)
    {
        // テスト用の設定を返す（各テストシナリオに応じてモードを指定）
        // Mode：PollingまたはStreamingを指定
        // MessageGenerationIntervalMs：サーバー側でメッセージを生成する間隔（ミリ秒）
        // ClientFetchIntervalMs：クライアント側がポーリングでメッセージを取得する間隔（ミリ秒、Pollingモード時）
        // BufferCapacity：サーバー側でメッセージをバッファリングする容量
        return new Dictionary<string, string?>
        {
            [$"{StreamConfiguration.SectionName}:Mode"] = mode,
            [$"{StreamConfiguration.SectionName}:MessageGenerationIntervalMs"] = "100",
            [$"{StreamConfiguration.SectionName}:ClientFetchIntervalMs"] = "100",
            [$"{StreamConfiguration.SectionName}:BufferCapacity"] = "1000",
        };
    }
}
