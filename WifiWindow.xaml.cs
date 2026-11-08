using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Windows.Devices.WiFi;

namespace WifiConnect;

public partial class WifiWindow : Window
{
    private readonly WifiService _wifi = new();
    private readonly ObservableCollection<WifiNetworkItem> _networks = new();

    public WifiWindow()
    {
        InitializeComponent();
        NetworkList.ItemsSource = _networks;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("Wi-Fi アダプターを確認しています…", async () =>
        {
            await _wifi.InitializeAsync();
            await RefreshAsync();
        });
    }

    // ---------------- ボタン操作 ----------------

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) =>
        await RunBusyAsync("スキャン中…", RefreshAsync);

    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        await RunBusyAsync("切断しています…", async () =>
        {
            _wifi.Disconnect();
            await Task.Delay(800);           // 状態が反映されるまで少し待つ
            await RefreshAsync();
            SetStatus("切断しました。");
        });
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e) => await ConnectSelectedAsync();

    private async void PasswordInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) await ConnectSelectedAsync();
    }

    private void NetworkList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NetworkList.SelectedItem is not WifiNetworkItem item)
        {
            ConnectPanel.Visibility = Visibility.Collapsed;
            return;
        }

        ConnectPanel.Visibility = Visibility.Visible;
        SelectedSsidText.Text = item.Ssid;
        PasswordInput.Clear();

        // 接続済みならボタンを無効化、オープンならパスワード欄を隠す
        PasswordArea.Visibility = item.IsSecured && !item.IsConnected
            ? Visibility.Visible : Visibility.Collapsed;
        ConnectButton.IsEnabled = !item.IsConnected;
        ConnectButton.Content = item.IsConnected ? "接続済み" : "接続";

        if (PasswordArea.Visibility == Visibility.Visible) PasswordInput.Focus();
    }

    // ---------------- 処理本体 ----------------

    /// <summary>現在の接続を切断し、選択中のネットワークに接続する。</summary>
    private async Task ConnectSelectedAsync()
    {
        if (NetworkList.SelectedItem is not WifiNetworkItem item || item.IsConnected) return;

        var password = PasswordInput.Password;

        await RunBusyAsync($"「{item.Ssid}」に接続しています…", async () =>
        {
            var status = await _wifi.ConnectAsync(item, password);
            await Task.Delay(1000);          // IP 取得などの反映待ち
            await RefreshAsync();

            var message = WifiService.ToMessage(status);
            if (status == WiFiConnectionStatus.InvalidCredential && string.IsNullOrEmpty(password))
                message = "パスワードを入力してください。";

            SetStatus(message, isError: status != WiFiConnectionStatus.Success);
        });
    }

    /// <summary>スキャンして一覧と「現在の接続」表示を更新する。</summary>
    private async Task RefreshAsync()
    {
        var selectedSsid = (NetworkList.SelectedItem as WifiNetworkItem)?.Ssid;

        var items = await _wifi.ScanAsync();
        _networks.Clear();
        foreach (var item in items) _networks.Add(item);

        var connected = items.FirstOrDefault(i => i.IsConnected)?.Ssid
                        ?? await _wifi.GetConnectedSsidAsync();
        CurrentSsidText.Text = connected ?? "未接続";
        DisconnectButton.IsEnabled = connected != null;

        // 選択状態を復元
        NetworkList.SelectedItem = _networks.FirstOrDefault(i => i.Ssid == selectedSsid);

        SetStatus($"{_networks.Count} 件のネットワークが見つかりました。");
    }

    /// <summary>処理中は操作を無効化し、例外はステータスに表示する。</summary>
    private async Task RunBusyAsync(string message, Func<Task> action)
    {
        SetBusy(true);
        SetStatus(message);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        BusyBar.Visibility = busy ? Visibility.Visible : Visibility.Hidden;
        RefreshButton.IsEnabled = !busy && _wifi.IsInitialized;
        NetworkList.IsEnabled = !busy;
        ConnectPanel.IsEnabled = !busy;
        if (busy) DisconnectButton.IsEnabled = false;
        else DisconnectButton.IsEnabled = _wifi.IsInitialized && CurrentSsidText.Text != "未接続";
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.Foreground = isError
            ? new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C))
            : new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x6B));
    }
}
