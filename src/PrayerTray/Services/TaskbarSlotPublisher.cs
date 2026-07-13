using System.IO;
using System.ComponentModel;
using System.Text;
using PrayerTray.ViewModels;

namespace PrayerTray.Services;

public sealed class TaskbarSlotPublisher : IDisposable
{
    private static readonly UTF8Encoding SlotFileEncoding = new(encoderShouldEmitUTF8Identifier: false);

    private readonly TaskbarWidgetViewModel _viewModel;
    private readonly string _slotFilePath;
    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private readonly PropertyChangedEventHandler _propertyChangedHandler;
    private string _lastPayload = string.Empty;
    private bool _disposed;

    public TaskbarSlotPublisher(TaskbarWidgetViewModel viewModel)
    {
        _viewModel = viewModel;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Directory.CreateDirectory(Path.Combine(appData, "PrayerTray"));
        _slotFilePath = Path.Combine(appData, "PrayerTray", "taskbar-slot.txt");

        _propertyChangedHandler = (_, _) => PublishIfVisible();
        _viewModel.PropertyChanged += _propertyChangedHandler;
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => PublishIfVisible();
    }

    public bool IsActive { get; private set; }

    public void SetActive(bool active)
    {
        if (_disposed)
        {
            return;
        }

        if (IsActive == active)
        {
            if (active)
            {
                Publish();
            }

            return;
        }

        IsActive = active;
        if (active)
        {
            _timer.Start();
            Publish(force: true);
        }
        else
        {
            _timer.Stop();
            PublishHidden();
        }
    }

    private void PublishIfVisible()
    {
        if (IsActive)
        {
            Publish();
        }
    }

    public void Publish(bool force = false)
    {
        if (!IsActive)
        {
            return;
        }

        var color = _viewModel.TextColor.TrimStart('#');
        var payload = new StringBuilder()
            .AppendLine("VISIBLE=1")
            .AppendLine($"TOP={EscapeValue(_viewModel.TopLine)}")
            .AppendLine($"BOTTOM={EscapeValue(_viewModel.BottomLine)}")
            .AppendLine($"COLOR={EscapeValue(color)}")
            .AppendLine($"EXE={EscapeValue(Environment.ProcessPath ?? string.Empty)}")
            .ToString();

        if (!force && payload == _lastPayload)
        {
            return;
        }

        _lastPayload = payload;
        File.WriteAllText(_slotFilePath, payload, SlotFileEncoding);
    }

    private void PublishHidden()
    {
        var payload = "VISIBLE=0" + Environment.NewLine;
        if (payload == _lastPayload)
        {
            return;
        }

        _lastPayload = payload;
        File.WriteAllText(_slotFilePath, payload, SlotFileEncoding);
    }

    private static string EscapeValue(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _viewModel.PropertyChanged -= _propertyChangedHandler;
        IsActive = false;
        PublishHidden();
    }
}
