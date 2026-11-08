using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;

namespace WifiConnect;

/// <summary>
/// 一覧に表示する Wi-Fi ネットワーク 1 件分。
/// 接続時に必要な WiFiAvailableNetwork をそのまま保持する。
/// </summary>
public sealed class WifiNetworkItem : INotifyPropertyChanged
{
    private bool _isConnected;

    public WifiNetworkItem(WiFiAvailableNetwork network)
    {
        Network = network;
        var auth = network.SecuritySettings.NetworkAuthenticationType;
        IsSecured = auth != NetworkAuthenticationType.Open80211
                 && auth != NetworkAuthenticationType.None;
    }

    public WiFiAvailableNetwork Network { get; }

    public string Ssid => Network.Ssid;

    /// <summary>電波強度（0〜4 本）</summary>
    public byte SignalBars => Network.SignalBars;

    public double Rssi => Network.NetworkRssiInDecibelMilliwatts;

    public string Band =>
        Network.ChannelCenterFrequencyInKilohertz >= 5_000_000 ? "5 GHz" : "2.4 GHz";

    public bool IsSecured { get; }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (_isConnected == value) return;
            _isConnected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public string StatusText =>
        IsConnected ? "接続済み"
        : IsSecured ? $"セキュリティ保護あり・{Band}"
        : $"オープン・{Band}";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
