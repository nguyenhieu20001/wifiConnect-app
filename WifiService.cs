using Windows.Devices.WiFi;
using Windows.Security.Credentials;

namespace WifiConnect;

/// <summary>
/// Windows.Devices.WiFi をラップした Wi-Fi 操作クラス。
/// スキャン / 現在の接続確認 / 切断 / 接続 を提供する。
/// </summary>
public sealed class WifiService
{
    private WiFiAdapter? _adapter;
    private List<WifiNetworkItem> _lastScan = new();

    public bool IsInitialized => _adapter != null;

    /// <summary>アクセス許可を取得し、最初の Wi-Fi アダプターを確保する。</summary>
    public async Task InitializeAsync()
    {
        var access = await WiFiAdapter.RequestAccessAsync();
        if (access != WiFiAccessStatus.Allowed)
        {
            throw new InvalidOperationException(
                "Wi-Fi へのアクセスが拒否されました。\n" +
                "Windows の「設定 > プライバシーとセキュリティ > 位置情報」で\n" +
                "「デスクトップアプリに位置情報へのアクセスを許可する」をオンにしてください。");
        }

        var adapters = await WiFiAdapter.FindAllAdaptersAsync();
        _adapter = adapters.FirstOrDefault()
            ?? throw new InvalidOperationException("Wi-Fi アダプターが見つかりません。");
    }

    /// <summary>
    /// 周辺の Wi-Fi をスキャンし、SSID ごとに一番強い電波のものだけを返す（強い順）。
    /// </summary>
    public async Task<IReadOnlyList<WifiNetworkItem>> ScanAsync()
    {
        var adapter = EnsureAdapter();
        await adapter.ScanAsync();

        var connectedSsid = await GetConnectedSsidAsync();

        _lastScan = adapter.NetworkReport.AvailableNetworks
            .Where(n => !string.IsNullOrWhiteSpace(n.Ssid))          // ステルス SSID は除外
            .GroupBy(n => n.Ssid)
            .Select(g => g.OrderByDescending(n => n.NetworkRssiInDecibelMilliwatts).First())
            .Select(n => new WifiNetworkItem(n) { IsConnected = n.Ssid == connectedSsid })
            .OrderByDescending(i => i.IsConnected)
            .ThenByDescending(i => i.Rssi)
            .ToList();

        return _lastScan;
    }

    /// <summary>現在接続中の SSID（未接続なら null）</summary>
    public async Task<string?> GetConnectedSsidAsync()
    {
        var adapter = EnsureAdapter();
        var profile = await adapter.NetworkAdapter.GetConnectedProfileAsync();
        return profile is { IsWlanConnectionProfile: true }
            ? profile.WlanConnectionProfileDetails.GetConnectedSsid()
            : null;
    }

    /// <summary>現在の Wi-Fi 接続を切断する。</summary>
    public void Disconnect() => EnsureAdapter().Disconnect();

    /// <summary>
    /// 現在の接続を切断してから、指定のネットワークに接続する。
    /// 失敗した場合は、元のネットワークへの再接続を試みる。
    /// </summary>
    /// <param name="password">
    /// 空の場合は Windows に保存済みのプロファイルで接続を試みる。
    /// </param>
    public async Task<WiFiConnectionStatus> ConnectAsync(WifiNetworkItem target, string? password)
    {
        var adapter = EnsureAdapter();

        // 切断前に、元の接続先を覚えておく（失敗時の復帰用）
        var previousSsid = await GetConnectedSsidAsync();
        var previous = _lastScan.FirstOrDefault(i => i.Ssid == previousSsid);

        // ① 現在の接続を切断
        adapter.Disconnect();

        // ② 選択したネットワークに接続
        WiFiConnectionResult result;
        if (target.IsSecured && !string.IsNullOrEmpty(password))
        {
            var credential = new PasswordCredential { Password = password };
            result = await adapter.ConnectAsync(
                target.Network, WiFiReconnectionKind.Automatic, credential);
        }
        else
        {
            // オープンネットワーク、または保存済みプロファイルで接続
            result = await adapter.ConnectAsync(
                target.Network, WiFiReconnectionKind.Automatic);
        }

        // ③ 失敗したら元のネットワークに戻す
        if (result.ConnectionStatus != WiFiConnectionStatus.Success
            && previous != null && previous.Ssid != target.Ssid)
        {
            try
            {
                await adapter.ConnectAsync(previous.Network, WiFiReconnectionKind.Automatic);
            }
            catch
            {
                // 復帰失敗は無視（呼び出し元には本来の結果を返す）
            }
        }

        return result.ConnectionStatus;
    }

    public static string ToMessage(WiFiConnectionStatus status) => status switch
    {
        WiFiConnectionStatus.Success => "接続しました。",
        WiFiConnectionStatus.InvalidCredential => "パスワードが正しくありません。",
        WiFiConnectionStatus.NetworkNotAvailable => "ネットワークが見つかりません。",
        WiFiConnectionStatus.Timeout => "接続がタイムアウトしました。",
        WiFiConnectionStatus.AccessRevoked => "アクセスが拒否されました。",
        WiFiConnectionStatus.UnsupportedAuthenticationProtocol => "この認証方式には対応していません。",
        _ => "接続に失敗しました。",
    };

    private WiFiAdapter EnsureAdapter() =>
        _adapter ?? throw new InvalidOperationException("WifiService が初期化されていません。");
}
