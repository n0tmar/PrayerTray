using System.Windows;
using PrayerTray.Native;
using PrayerTray.Services;
using WpfApplication = System.Windows.Application;

namespace PrayerTray;

public partial class App : WpfApplication
{
    private AppHost? _appHost;
    private SingleInstanceService? _singleInstance;
    private uint _taskbarCreatedMessage;
    private uint _nativeCommandMessage;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.IsFirstInstance)
        {
            SingleInstanceService.NotifyExistingInstance();
            Shutdown();
            return;
        }

        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("Resources/TaskbarTypography.xaml", UriKind.Relative)
        });
        Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("Resources/TaskbarFlyoutTheme.xaml", UriKind.Relative)
        });
        Resources.Add("HighlightConverter", new Converters.HighlightConverter());

        _appHost = new AppHost();
        _appHost.RegisterTaskbarCreatedMessage(msg => _taskbarCreatedMessage = msg);
        _nativeCommandMessage = Win32.RegisterWindowMessage("PrayerTray_NativeSlotCommand");

        var messageWindow = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false
        };
        messageWindow.SourceInitialized += (_, _) =>
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(messageWindow).Handle;
            NativeMessageHwndService.Write(handle);

            var source = System.Windows.Interop.HwndSource.FromHwnd(handle);
            source?.AddHook(WndProc);
        };
        messageWindow.Show();
        messageWindow.Hide();

        await _appHost.InitializeAsync();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_taskbarCreatedMessage != 0 && msg == _taskbarCreatedMessage)
        {
            _appHost?.OnTaskbarCreated();
            handled = true;
        }
        else if (_nativeCommandMessage != 0 && msg == _nativeCommandMessage)
        {
            _appHost?.HandleNativeSlotCommand((NativeSlotCommand)wParam.ToInt32());
            handled = true;
        }

        return IntPtr.Zero;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        NativeMessageHwndService.Delete();
        _appHost?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
