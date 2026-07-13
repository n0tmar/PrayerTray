// ==WindhawkMod==
// @id prayertray-taskbar-slot
// @name PrayerTray taskbar slot
// @description Adds PrayerTray as a real Windows 11 tray text slot beside network, sound, and the clock.
// @version 1.1.2
// @author Omar Alhami (mar)
// @homepage https://github.com/n0tmar/PrayerTray
// @include explorer.exe
// @architecture x86-64
// @compilerOptions -lole32 -loleaut32 -lruntimeobject -lshlwapi
// ==/WindhawkMod==

// ==WindhawkModReadme==
/*
# PrayerTray taskbar slot

Prayer time belongs near the clock.

<p align="center">
  <img src="https://raw.githubusercontent.com/n0tmar/PrayerTray/main/docs/assets/taskbar-time.png" width="330" alt="PrayerTray taskbar prayer time">
  <img src="https://raw.githubusercontent.com/n0tmar/PrayerTray/main/docs/assets/taskbar-countdown.png" width="330" alt="PrayerTray taskbar countdown">
</p>

## What it does

This Windhawk mod gives PrayerTray a real Windows 11 tray slot. It places the
text beside network, sound, and the clock, so Windows moves the tray icons
around it.

PrayerTray handles prayer times, language, settings, cache, and notification
sound. This mod only handles the taskbar slot.

## Install

Install PrayerTray first, then enable this Windhawk mod. PrayerTray writes the
taskbar text to `%APPDATA%\PrayerTray\taskbar-slot.txt`; this mod reads it and
places it in the real tray layout.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://github.com/n0tmar/PrayerTray/releases/latest/download/install.ps1 | iex"
```

Left-click the taskbar prayer time for today's times. Right-click it for
settings.

## Screenshots

<p align="center">
  <img src="https://raw.githubusercontent.com/n0tmar/PrayerTray/main/docs/assets/prayer-times.png" width="360" alt="PrayerTray prayer times flyout">
  <img src="https://raw.githubusercontent.com/n0tmar/PrayerTray/main/docs/assets/settings.png" width="360" alt="PrayerTray settings window">
</p>

## Project

Made by Omar Alhami, aka mar.

GitHub:
https://github.com/n0tmar/PrayerTray

Support the developer:
https://www.patreon.com/n0tmar

Contributions are welcome. Small fixes, language improvements, and Windows
taskbar testing help a lot.

## Notes

- Requires Windows 11.
- Targets `explorer.exe`.
- Mod version matches the latest GitHub release.
*/
// ==/WindhawkModReadme==

#include <windhawk_utils.h>

#include <atomic>
#include <algorithm>
#include <fstream>
#include <iterator>
#include <list>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

#undef GetCurrentTime

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.UI.Input.h>
#include <winrt/Windows.UI.Xaml.h>
#include <winrt/Windows.UI.Xaml.Controls.h>
#include <winrt/Windows.UI.Xaml.Controls.Primitives.h>
#include <winrt/Windows.UI.Xaml.Input.h>
#include <winrt/Windows.UI.Xaml.Markup.h>
#include <winrt/Windows.UI.Xaml.Media.h>
#include <winrt/base.h>

using namespace winrt::Windows::UI::Xaml;

namespace {

std::atomic<bool> g_taskbarViewDllLoaded;
std::atomic<bool> g_unloading;
std::atomic<bool> g_slotInjected;
std::atomic<int> g_applySlotAttempts;

using FrameworkElementLoadedEventRevoker = winrt::impl::event_revoker<
    IFrameworkElement,
    &winrt::impl::abi<IFrameworkElement>::type::remove_Loaded>;

std::list<FrameworkElementLoadedEventRevoker> g_loadedRevokers;
std::list<winrt::weak_ref<FrameworkElement>> g_observedElements;

Controls::TextBlock g_topTextBlock{nullptr};
Controls::TextBlock g_bottomTextBlock{nullptr};
Controls::Button g_slotButton{nullptr};
FrameworkElement g_slotRoot{nullptr};
Controls::Grid g_slotParentGrid{nullptr};

UINT_PTR g_updateTimer;
std::atomic<bool> g_slotPointerOver;
std::atomic<bool> g_slotPointerPressed;
std::atomic<int> g_lastCommandId;
std::atomic<ULONGLONG> g_lastCommandTick;

struct SlotContent {
    bool visible = false;
    std::wstring top;
    std::wstring bottom;
    std::wstring color = L"FFFFFF";
    std::wstring exePath;
};

FrameworkElement FindChildByName(FrameworkElement element, PCWSTR name) {
    int childrenCount = Media::VisualTreeHelper::GetChildrenCount(element);
    for (int i = 0; i < childrenCount; i++) {
        auto child =
            Media::VisualTreeHelper::GetChild(element, i).try_as<FrameworkElement>();
        if (!child) {
            continue;
        }

        if (child.Name() == name) {
            return child;
        }

        if (auto match = FindChildByName(child, name)) {
            return match;
        }
    }

    return nullptr;
}

Controls::TextBlock FindTextBlockByName(FrameworkElement element, PCWSTR name) {
    if (auto named = element.FindName(name).try_as<Controls::TextBlock>()) {
        return named;
    }

    return FindChildByName(element, name).try_as<Controls::TextBlock>();
}

FrameworkElement FindChildByClassName(FrameworkElement element,
                                    PCWSTR className) {
    if (winrt::get_class_name(element) == className) {
        return element;
    }

    int childrenCount = Media::VisualTreeHelper::GetChildrenCount(element);
    for (int i = 0; i < childrenCount; i++) {
        auto child =
            Media::VisualTreeHelper::GetChild(element, i).try_as<FrameworkElement>();
        if (!child) {
            continue;
        }

        if (auto match = FindChildByClassName(child, className)) {
            return match;
        }
    }

    return nullptr;
}

FrameworkElement FindMainStackIconStackPanel(FrameworkElement mainStack) {
    FrameworkElement child = mainStack;
    if ((child = FindChildByName(child, L"Content")) &&
        (child = FindChildByName(child, L"IconStack")) &&
        (child = FindChildByClassName(
             child, L"Windows.UI.Xaml.Controls.ItemsPresenter")) &&
        (child = FindChildByClassName(
             child, L"Windows.UI.Xaml.Controls.StackPanel"))) {
        return child;
    }

    return nullptr;
}

FrameworkElement FindDirectChildByName(Controls::Panel panel, PCWSTR name) {
    for (uint32_t i = 0; i < panel.Children().Size(); i++) {
        auto child = panel.Children().GetAt(i).try_as<FrameworkElement>();
        if (child && child.Name() == name) {
            return child;
        }
    }

    return nullptr;
}

Controls::Grid FindTrayGridFromAnchor(FrameworkElement root) {
    FrameworkElement anchor =
        FindChildByName(root, L"NotificationCenterButton");
    if (!anchor) {
        anchor = FindChildByName(root, L"ControlCenterButton");
    }
    if (!anchor) {
        return nullptr;
    }

    DependencyObject current = anchor;
    while (current) {
        current = Media::VisualTreeHelper::GetParent(current);
        if (!current) {
            break;
        }

        if (auto grid = current.try_as<Controls::Grid>()) {
            if (FindDirectChildByName(grid, L"NotificationCenterButton") ||
                FindDirectChildByName(grid, L"ControlCenterButton")) {
                return grid;
            }
        }
    }

    return nullptr;
}

FrameworkElement ResolveMainStack(FrameworkElement root) {
    if (root.Name() == L"MainStack" ||
        winrt::get_class_name(root) == L"SystemTray.Stack") {
        return root;
    }

    return FindChildByName(root, L"MainStack");
}

bool IsSlotRootAlive() {
    if (!g_slotInjected) {
        return false;
    }

    try {
        auto root = g_slotRoot;
        return root && winrt::get_abi(root) != nullptr;
    } catch (...) {
        return false;
    }
}

void ResetSlotState() {
    g_slotRoot = nullptr;
    g_slotButton = nullptr;
    g_topTextBlock = nullptr;
    g_bottomTextBlock = nullptr;
    g_slotParentGrid = nullptr;
    g_slotInjected = false;
    g_slotPointerOver = false;
    g_slotPointerPressed = false;
}

void RemoveSlotElement(FrameworkElement slotElement) {
    if (!slotElement) {
        return;
    }

    if (auto parent = Media::VisualTreeHelper::GetParent(slotElement)) {
        if (auto panel = parent.try_as<Controls::Panel>()) {
            auto children = panel.Children();
            for (uint32_t i = 0; i < children.Size(); i++) {
                if (children.GetAt(i) == slotElement) {
                    children.RemoveAt(i);
                    break;
                }
            }
        } else if (auto grid = parent.try_as<Controls::Grid>()) {
            auto children = grid.Children();
            for (uint32_t i = 0; i < children.Size(); i++) {
                if (children.GetAt(i) == slotElement) {
                    children.RemoveAt(i);
                    break;
                }
            }
        }
    }
}

void CollectChildrenByName(FrameworkElement element,
                           PCWSTR name,
                           std::vector<FrameworkElement>& matches) {
    if (element.Name() == name) {
        matches.push_back(element);
    }

    const int childrenCount = Media::VisualTreeHelper::GetChildrenCount(element);
    for (int i = 0; i < childrenCount; i++) {
        if (auto child =
                Media::VisualTreeHelper::GetChild(element, i)
                    .try_as<FrameworkElement>()) {
            CollectChildrenByName(child, name, matches);
        }
    }
}

void CleanupDuplicatePrayerTraySlots(FrameworkElement root) {
    std::vector<FrameworkElement> matches;
    CollectChildrenByName(root, L"PrayerTraySlotRoot", matches);

    for (auto slotElement : matches) {
        RemoveSlotElement(slotElement);
    }

    if (!matches.empty()) {
        ResetSlotState();
    }
}

std::wstring GetSlotFilePath() {
    wchar_t appData[MAX_PATH]{};
    DWORD len = GetEnvironmentVariableW(L"APPDATA", appData, MAX_PATH);
    if (len == 0 || len >= MAX_PATH) {
        return L"";
    }

    return std::wstring(appData) + L"\\PrayerTray\\taskbar-slot.txt";
}

std::wstring GetStatusFilePath() {
    wchar_t appData[MAX_PATH]{};
    DWORD len = GetEnvironmentVariableW(L"APPDATA", appData, MAX_PATH);
    if (len == 0 || len >= MAX_PATH) {
        return L"";
    }

    return std::wstring(appData) + L"\\PrayerTray\\taskbar-slot-status.txt";
}

void WriteSlotStatus(bool visible) {
    const auto path = GetStatusFilePath();
    if (path.empty()) {
        return;
    }

    std::ofstream out(path.c_str(), std::ios::binary | std::ios::trunc);
    if (!out) {
        return;
    }

    out << "INJECTED=1\n";
    out << "VISIBLE=" << (visible ? "1" : "0") << "\n";
    out << "PID=" << GetCurrentProcessId() << "\n";
    out << "TICK=" << GetTickCount64() << "\n";
}

std::wstring GetCommandFilePath() {
    wchar_t appData[MAX_PATH]{};
    DWORD len = GetEnvironmentVariableW(L"APPDATA", appData, MAX_PATH);
    if (len == 0 || len >= MAX_PATH) {
        return L"";
    }

    return std::wstring(appData) + L"\\PrayerTray\\taskbar-command.txt";
}

std::wstring GetMessageHwndFilePath() {
    wchar_t appData[MAX_PATH]{};
    DWORD len = GetEnvironmentVariableW(L"APPDATA", appData, MAX_PATH);
    if (len == 0 || len >= MAX_PATH) {
        return L"";
    }

    return std::wstring(appData) + L"\\PrayerTray\\message-hwnd.txt";
}

bool PostNativeCommandToApp(int commandId) {
    const UINT message = RegisterWindowMessageW(L"PrayerTray_NativeSlotCommand");
    if (!message) {
        return false;
    }

    const auto path = GetMessageHwndFilePath();
    if (path.empty()) {
        return false;
    }

    std::ifstream in(path.c_str());
    if (!in) {
        return false;
    }

    unsigned long long hwndValue = 0;
    in >> hwndValue;
    if (!hwndValue) {
        return false;
    }

    const auto hwnd = reinterpret_cast<HWND>(static_cast<uintptr_t>(hwndValue));
    if (!IsWindow(hwnd)) {
        return false;
    }

    return PostMessageW(hwnd, message, static_cast<WPARAM>(commandId), 0) != FALSE;
}

std::optional<std::wstring> ParseLineValue(std::wstring_view line,
                                           std::wstring_view key) {
    if (!line.starts_with(key) || line.size() <= key.size() + 1 ||
        line[key.size()] != L'=') {
        return std::nullopt;
    }

    return std::wstring(line.substr(key.size() + 1));
}

std::wstring Utf8ToWide(std::string_view value) {
    if (value.empty()) {
        return {};
    }

    int size = MultiByteToWideChar(CP_UTF8, 0, value.data(),
                                   static_cast<int>(value.size()), nullptr, 0);
    if (size <= 0) {
        std::wstring fallback;
        fallback.reserve(value.size());
        for (unsigned char ch : value) {
            fallback.push_back(static_cast<wchar_t>(ch));
        }
        return fallback;
    }

    std::wstring result(size, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, value.data(),
                        static_cast<int>(value.size()), result.data(), size);
    return result;
}

SlotContent ReadSlotContent() {
    SlotContent content;
    const auto path = GetSlotFilePath();
    if (path.empty()) {
        return content;
    }

    std::ifstream in(path.c_str(), std::ios::binary);
    if (!in) {
        return content;
    }

    std::string bytes((std::istreambuf_iterator<char>(in)),
                      std::istreambuf_iterator<char>());
    std::wistringstream lines(Utf8ToWide(bytes));
    std::wstring line;
    while (std::getline(lines, line)) {
        if (!line.empty() && line.front() == L'\ufeff') {
            line.erase(0, 1);
        }
        if (!line.empty() && line.back() == L'\r') {
            line.pop_back();
        }

        if (auto value = ParseLineValue(line, L"VISIBLE")) {
            content.visible = *value == L"1" || *value == L"true";
        } else if (auto value = ParseLineValue(line, L"TOP")) {
            content.top = *value;
        } else if (auto value = ParseLineValue(line, L"BOTTOM")) {
            content.bottom = *value;
        } else if (auto value = ParseLineValue(line, L"COLOR")) {
            content.color = *value;
        } else if (auto value = ParseLineValue(line, L"EXE")) {
            content.exePath = *value;
        }
    }

    return content;
}

winrt::Windows::UI::Color ParseColor(const std::wstring& color) {
    std::wstring hex = color;
    if (hex.starts_with(L"#")) {
        hex = hex.substr(1);
    }

    if (hex.size() != 6) {
        return winrt::Windows::UI::Color{255, 255, 255, 255};
    }

    auto parseByte = [&hex](size_t offset) -> uint8_t {
        wchar_t buf[3] = {hex[offset], hex[offset + 1], 0};
        return static_cast<uint8_t>(wcstoul(buf, nullptr, 16));
    };

    return winrt::Windows::UI::Color{255, parseByte(0), parseByte(2), parseByte(4)};
}

Media::SolidColorBrush Brush(uint8_t alpha, uint8_t red, uint8_t green,
                             uint8_t blue) {
    return Media::SolidColorBrush({alpha, red, green, blue});
}

void ApplySlotButtonVisualState() {
    auto button = g_slotButton;
    if (!button) {
        return;
    }

    uint8_t alpha = 0;
    if (g_slotPointerPressed.load()) {
        alpha = 0x3A;
    } else if (g_slotPointerOver.load()) {
        alpha = 0x26;
    }

    button.Background(Brush(alpha, 255, 255, 255));
}

void ResetSlotButtonVisualState() {
    g_slotPointerOver = false;
    g_slotPointerPressed = false;
    ApplySlotButtonVisualState();
}

const char* CommandText(int commandId) {
    switch (commandId) {
        case 1:
            return "SHOW_FLYOUT";
        case 2:
            return "SHOW_SETTINGS";
        case 3:
            return "REFRESH_LOCATION";
        default:
            return "";
    }
}

void WriteCommandFile(int commandId) {
    const char* command = CommandText(commandId);
    if (!command[0]) {
        return;
    }

    const auto path = GetCommandFilePath();
    if (path.empty()) {
        return;
    }

    std::ofstream out(path.c_str(), std::ios::binary | std::ios::trunc);
    if (out) {
        out << command;
    }
}

bool IsPrayerTrayRunning() {
    HANDLE mutex =
        OpenMutexW(SYNCHRONIZE, FALSE, L"PrayerTray_SingleInstance_Mutex");
    if (!mutex) {
        return false;
    }

    CloseHandle(mutex);
    return true;
}

void LaunchPrayerTrayIfNeeded(const std::wstring& exePath) {
    if (exePath.empty() || IsPrayerTrayRunning()) {
        return;
    }

    std::wstring commandLine = L"\"" + exePath + L"\"";
    std::wstring workingDirectory;
    if (size_t slash = exePath.find_last_of(L"\\/"); slash != std::wstring::npos) {
        workingDirectory = exePath.substr(0, slash);
    }

    STARTUPINFOW startupInfo{};
    startupInfo.cb = sizeof(startupInfo);
    PROCESS_INFORMATION processInfo{};
    if (CreateProcessW(
            exePath.c_str(), commandLine.data(), nullptr, nullptr, FALSE, 0,
            nullptr, workingDirectory.empty() ? nullptr : workingDirectory.c_str(),
            &startupInfo, &processInfo)) {
        CloseHandle(processInfo.hThread);
        CloseHandle(processInfo.hProcess);
    }
}

void DispatchPrayerTrayCommand(int commandId) {
    const auto now = GetTickCount64();
    const auto lastCommandId = g_lastCommandId.load();
    const auto lastCommandTick = g_lastCommandTick.load();
    if (lastCommandId == commandId && now >= lastCommandTick &&
        now - lastCommandTick < 250) {
        return;
    }

    g_lastCommandId = commandId;
    g_lastCommandTick = now;

    if (IsPrayerTrayRunning()) {
        if (!PostNativeCommandToApp(commandId)) {
            WriteCommandFile(commandId);
        }
    } else {
        WriteCommandFile(commandId);
        LaunchPrayerTrayIfNeeded(ReadSlotContent().exePath);
    }

}

void ApplySlotContent(const SlotContent& content) {
    auto top = g_topTextBlock;
    auto bottom = g_bottomTextBlock;
    auto root = g_slotRoot;
    if (!top || !bottom || !root) {
        g_slotInjected = false;
        return;
    }

    const bool show = content.visible && (!content.top.empty() || !content.bottom.empty());
    root.Visibility(show ? Visibility::Visible : Visibility::Collapsed);
    WriteSlotStatus(show);
    if (!show) {
        return;
    }

    const auto brush = winrt::Windows::UI::Xaml::Media::SolidColorBrush(
        ParseColor(content.color));

    top.Text(content.top);
    top.FlowDirection(FlowDirection::LeftToRight);
    top.Foreground(brush);
    bottom.Text(content.bottom);
    bottom.Foreground(brush);
    root.UpdateLayout();
}

void TrackObservedElement(FrameworkElement element) {
    if (!element) {
        return;
    }

    for (auto it = g_observedElements.begin(); it != g_observedElements.end();) {
        auto existing = it->get();
        if (!existing) {
            it = g_observedElements.erase(it);
            continue;
        }

        if (winrt::get_abi(existing) == winrt::get_abi(element)) {
            return;
        }

        ++it;
    }

    g_observedElements.emplace_back(element);
    while (g_observedElements.size() > 64) {
        g_observedElements.pop_front();
    }
}

void AttachSlotInteractions(FrameworkElement slotRoot) {
    auto button = slotRoot.try_as<Controls::Button>();
    if (!button) {
        return;
    }

    if (auto existing = g_slotButton) {
        if (winrt::get_abi(existing) == winrt::get_abi(button)) {
            return;
        }
    }

    g_slotButton = button;
    ResetSlotButtonVisualState();

    button.PointerEntered(
        [](winrt::Windows::Foundation::IInspectable const&,
           Input::PointerRoutedEventArgs const&) {
            g_slotPointerOver = true;
            ApplySlotButtonVisualState();
        });

    button.PointerExited(
        [](winrt::Windows::Foundation::IInspectable const&,
           Input::PointerRoutedEventArgs const&) {
            g_slotPointerOver = false;
            g_slotPointerPressed = false;
            ApplySlotButtonVisualState();
        });

    button.Click([](winrt::Windows::Foundation::IInspectable const&,
                    RoutedEventArgs const&) {
        DispatchPrayerTrayCommand(1);
    });

    button.RightTapped(
        [](winrt::Windows::Foundation::IInspectable const&,
           Input::RightTappedRoutedEventArgs const& args) {
            DispatchPrayerTrayCommand(2);
            args.Handled(true);
        });

    button.PointerPressed(
        [](winrt::Windows::Foundation::IInspectable const& sender,
           Input::PointerRoutedEventArgs const& args) {
            auto element = sender.try_as<UIElement>();
            if (!element) {
                return;
            }

            auto point = args.GetCurrentPoint(element);
            auto properties = point.Properties();
            if (properties.IsRightButtonPressed()) {
                g_slotPointerOver = true;
                g_slotPointerPressed = true;
                ApplySlotButtonVisualState();
                args.Handled(true);
            } else if (properties.IsLeftButtonPressed()) {
                g_slotPointerOver = true;
                g_slotPointerPressed = true;
                ApplySlotButtonVisualState();
            }
        });

    button.PointerReleased(
        [](winrt::Windows::Foundation::IInspectable const&,
           Input::PointerRoutedEventArgs const&) {
            g_slotPointerPressed = false;
            ApplySlotButtonVisualState();
        });

    button.PointerCanceled(
        [](winrt::Windows::Foundation::IInspectable const&,
           Input::PointerRoutedEventArgs const&) {
            g_slotPointerPressed = false;
            ApplySlotButtonVisualState();
        });

    button.PointerCaptureLost(
        [](winrt::Windows::Foundation::IInspectable const&,
           Input::PointerRoutedEventArgs const&) {
            g_slotPointerPressed = false;
            ApplySlotButtonVisualState();
        });
}

void RemovePrayerTraySlotFromGrid(Controls::Grid trayGrid) {
    auto children = trayGrid.Children();
    int slotColumn = -1;

    for (uint32_t i = 0; i < children.Size(); i++) {
        auto child = children.GetAt(i).try_as<FrameworkElement>();
        if (child && child.Name() == L"PrayerTraySlotRoot") {
            slotColumn = Controls::Grid::GetColumn(child);
            children.RemoveAt(i);
            break;
        }
    }

    if (slotColumn >= 0 &&
        slotColumn < static_cast<int>(trayGrid.ColumnDefinitions().Size())) {
        for (uint32_t i = 0; i < children.Size(); i++) {
            auto child = children.GetAt(i).try_as<FrameworkElement>();
            if (!child) {
                continue;
            }

            int childColumn = Controls::Grid::GetColumn(child);
            if (childColumn > slotColumn) {
                Controls::Grid::SetColumn(child, childColumn - 1);
            }
        }

        trayGrid.ColumnDefinitions().RemoveAt(slotColumn);
    }

    g_slotRoot = nullptr;
    g_slotButton = nullptr;
    g_topTextBlock = nullptr;
    g_bottomTextBlock = nullptr;
    g_slotParentGrid = nullptr;
    g_slotInjected = false;
}

void RemoveAllPrayerTraySlotsFromGrid(Controls::Grid trayGrid) {
    while (FindDirectChildByName(trayGrid, L"PrayerTraySlotRoot")) {
        RemovePrayerTraySlotFromGrid(trayGrid);
    }
}

int ResolveTraySlotColumn(Controls::Grid trayGrid) {
    if (auto clock = FindDirectChildByName(trayGrid, L"NotificationCenterButton")) {
        return Controls::Grid::GetColumn(clock);
    }

    if (auto controlCenter =
            FindDirectChildByName(trayGrid, L"ControlCenterButton")) {
        return Controls::Grid::GetColumn(controlCenter) + 1;
    }

    return -1;
}

bool EnsurePrayerTraySlotInTrayGrid(Controls::Grid trayGrid) {
    if (IsSlotRootAlive() && g_slotParentGrid &&
        winrt::get_abi(g_slotParentGrid) == winrt::get_abi(trayGrid)) {
        AttachSlotInteractions(g_slotRoot);
        return true;
    }

    RemoveAllPrayerTraySlotsFromGrid(trayGrid);

    if (auto existing = FindDirectChildByName(trayGrid, L"PrayerTraySlotRoot")) {
        auto top = FindTextBlockByName(existing, L"PrayerTrayTop");
        auto bottom = FindTextBlockByName(existing, L"PrayerTrayBottom");
        if (top && bottom) {
            g_slotRoot = existing;
            g_topTextBlock = top;
            g_bottomTextBlock = bottom;
            g_slotParentGrid = trayGrid;
            g_slotInjected = true;
            AttachSlotInteractions(existing);
            return true;
        }

        RemovePrayerTraySlotFromGrid(trayGrid);
    }

    int slotColumn = ResolveTraySlotColumn(trayGrid);
    if (slotColumn < 0) {
        return false;
    }

    slotColumn = std::clamp(
        slotColumn, 0, static_cast<int>(trayGrid.ColumnDefinitions().Size()));

    Controls::Button slotButton;
    slotButton.Name(L"PrayerTraySlotRoot");
    slotButton.Background(Brush(0, 255, 255, 255));
    slotButton.BorderBrush(Brush(0, 255, 255, 255));
    slotButton.BorderThickness({0, 0, 0, 0});
    slotButton.Height(40);
    slotButton.MinWidth(52);
    slotButton.Padding({7, 0, 7, 0});
    slotButton.Margin({2, 0, 2, 0});
    slotButton.VerticalAlignment(VerticalAlignment::Center);
    slotButton.HorizontalAlignment(HorizontalAlignment::Center);
    slotButton.HorizontalContentAlignment(HorizontalAlignment::Center);
    slotButton.VerticalContentAlignment(VerticalAlignment::Center);

    Controls::StackPanel slotStack;
    slotStack.Orientation(Controls::Orientation::Vertical);
    slotStack.VerticalAlignment(VerticalAlignment::Center);
    slotStack.HorizontalAlignment(HorizontalAlignment::Center);
    slotStack.IsHitTestVisible(false);

    Controls::TextBlock topText;
    topText.Name(L"PrayerTrayTop");
    topText.FontFamily(Media::FontFamily(L"Segoe UI Variable Text"));
    topText.FontSize(11.5);
    topText.LineHeight(13);
    topText.TextAlignment(TextAlignment::Center);
    topText.FlowDirection(FlowDirection::LeftToRight);

    Controls::TextBlock bottomText;
    bottomText.Name(L"PrayerTrayBottom");
    bottomText.FontFamily(Media::FontFamily(L"Segoe UI Variable Text"));
    bottomText.FontSize(11.5);
    bottomText.LineHeight(13);
    bottomText.TextAlignment(TextAlignment::Center);

    slotStack.Children().Append(topText);
    slotStack.Children().Append(bottomText);
    slotButton.Content(slotStack);
    FrameworkElement slotElement = slotButton;

    Controls::ColumnDefinition column;
    column.Width({1.0, GridUnitType::Auto});
    if (slotColumn >= static_cast<int>(trayGrid.ColumnDefinitions().Size())) {
        trayGrid.ColumnDefinitions().Append(column);
    } else {
        trayGrid.ColumnDefinitions().InsertAt(slotColumn, column);
        for (uint32_t i = 0; i < trayGrid.Children().Size(); i++) {
            auto child = trayGrid.Children().GetAt(i).try_as<FrameworkElement>();
            if (!child) {
                continue;
            }

            int childColumn = Controls::Grid::GetColumn(child);
            if (childColumn >= slotColumn) {
                Controls::Grid::SetColumn(child, childColumn + 1);
            }
        }
    }

    Controls::Grid::SetColumn(slotElement, slotColumn);
    trayGrid.Children().Append(slotElement);

    g_slotRoot = slotElement;
    g_topTextBlock = topText;
    g_bottomTextBlock = bottomText;
    g_slotParentGrid = trayGrid;
    g_slotInjected = true;
    AttachSlotInteractions(slotElement);

    trayGrid.UpdateLayout();
    return true;
}

bool EnsurePrayerTraySlot(FrameworkElement mainStack) {
    if (IsSlotRootAlive()) {
        AttachSlotInteractions(g_slotRoot);
        return true;
    }

    auto stackPanelElement = FindMainStackIconStackPanel(mainStack);
    if (!stackPanelElement) {
        Wh_Log(L"MainStack IconStack panel not found");
        return false;
    }

    auto stackPanel = stackPanelElement.try_as<Controls::StackPanel>();
    if (!stackPanel) {
        Wh_Log(L"IconStack panel cast failed");
        return false;
    }

    auto children = stackPanel.Children();
    for (int i = static_cast<int>(children.Size()) - 1; i >= 0; --i) {
        auto child = children.GetAt(i).try_as<FrameworkElement>();
        if (child && child.Name() == L"PrayerTraySlotRoot") {
            children.RemoveAt(i);
        }
    }

    auto existing = FindChildByName(stackPanelElement, L"PrayerTraySlotRoot");
    if (existing) {
        g_slotRoot = existing;
        g_topTextBlock = FindTextBlockByName(existing, L"PrayerTrayTop");
        g_bottomTextBlock = FindTextBlockByName(existing, L"PrayerTrayBottom");
        g_slotInjected = true;
        AttachSlotInteractions(existing);
        return true;
    }

    PCWSTR xaml = LR"(
        <Button
            xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            Name="PrayerTraySlotRoot"
            Background="Transparent"
            BorderThickness="0"
            Padding="4,0,6,0"
            Margin="4,0,6,0"
            VerticalAlignment="Center"
            HorizontalAlignment="Center"
            HorizontalContentAlignment="Center"
            VerticalContentAlignment="Center">
            <StackPanel Orientation="Vertical" VerticalAlignment="Center">
                <TextBlock
                    Name="PrayerTrayTop"
                    FontFamily="Segoe UI Variable Text"
                    FontSize="12"
                    LineHeight="14"
                    TextAlignment="Center"
                    FlowDirection="LeftToRight"
                    TextTrimming="None"
                    IsHitTestVisible="False" />
                <TextBlock
                    Name="PrayerTrayBottom"
                    FontFamily="Segoe UI Variable Text"
                    FontSize="12"
                    LineHeight="14"
                    TextAlignment="Center"
                    TextTrimming="None"
                    IsHitTestVisible="False" />
            </StackPanel>
        </Button>
    )";

    auto slotButton =
        Markup::XamlReader::Load(xaml).try_as<Controls::Button>();
    if (!slotButton) {
        Wh_Log(L"Failed to load PrayerTray slot XAML");
        return false;
    }

    children.Append(slotButton);
    g_slotRoot = slotButton;
    g_topTextBlock = FindTextBlockByName(slotButton, L"PrayerTrayTop");
    g_bottomTextBlock = FindTextBlockByName(slotButton, L"PrayerTrayBottom");
    g_slotInjected = true;
    AttachSlotInteractions(slotButton);

    Wh_Log(L"PrayerTray slot injected into MainStack");
    return true;
}

bool ApplyStyle(XamlRoot xamlRoot) {
    FrameworkElement child = xamlRoot.Content().try_as<FrameworkElement>();
    if (!child) {
        return false;
    }

    if (IsSlotRootAlive()) {
        ApplySlotContent(ReadSlotContent());
        return true;
    }

    CleanupDuplicatePrayerTraySlots(child);

    if (auto systemTrayFrameGrid = FindChildByName(child, L"SystemTrayFrameGrid")) {
        if (auto trayGrid = systemTrayFrameGrid.try_as<Controls::Grid>()) {
            if (EnsurePrayerTraySlotInTrayGrid(trayGrid)) {
                ApplySlotContent(ReadSlotContent());
                return true;
            }
        }
    }

    if (auto trayGrid = FindTrayGridFromAnchor(child)) {
        if (EnsurePrayerTraySlotInTrayGrid(trayGrid)) {
            ApplySlotContent(ReadSlotContent());
            return true;
        }
    }

    if (auto mainStack = ResolveMainStack(child)) {
        if (EnsurePrayerTraySlot(mainStack)) {
            ApplySlotContent(ReadSlotContent());
            return true;
        }
    }

    return false;
}

bool ApplyStyleFromObservedElements() {
    if (IsSlotRootAlive()) {
        ApplySlotContent(ReadSlotContent());
        return true;
    }

    bool applied = false;

    for (auto it = g_observedElements.begin(); it != g_observedElements.end();) {
        auto element = it->get();
        if (!element) {
            it = g_observedElements.erase(it);
            continue;
        }

        try {
            if (auto xamlRoot = element.XamlRoot()) {
                applied = ApplyStyle(xamlRoot) || applied;
            }
        } catch (...) {
        }

        ++it;
    }

    return applied;
}

HWND FindCurrentProcessTaskbarWnd() {
    HWND hTaskbarWnd = nullptr;
    EnumWindows(
        [](HWND hWnd, LPARAM lParam) -> BOOL {
            DWORD dwProcessId;
            WCHAR className[32];
            if (GetWindowThreadProcessId(hWnd, &dwProcessId) &&
                dwProcessId == GetCurrentProcessId() &&
                GetClassName(hWnd, className, ARRAYSIZE(className)) &&
                _wcsicmp(className, L"Shell_TrayWnd") == 0) {
                *reinterpret_cast<HWND*>(lParam) = hWnd;
                return FALSE;
            }
            return TRUE;
        },
        reinterpret_cast<LPARAM>(&hTaskbarWnd));
    return hTaskbarWnd;
}

void* CTaskBand_ITaskListWndSite_vftable;
using CTaskBand_GetTaskbarHost_t = void*(WINAPI*)(void* pThis, void** result);
CTaskBand_GetTaskbarHost_t CTaskBand_GetTaskbarHost_Original;
void* TaskbarHost_FrameHeight_Original;
using std__Ref_count_base__Decref_t = void(WINAPI*)(void* pThis);
std__Ref_count_base__Decref_t std__Ref_count_base__Decref_Original;

XamlRoot GetTaskbarXamlRoot(HWND hTaskbarWnd) {
    HWND hTaskSwWnd = reinterpret_cast<HWND>(GetProp(hTaskbarWnd, L"TaskbandHWND"));
    if (!hTaskSwWnd) {
        return nullptr;
    }

    void* taskBand = reinterpret_cast<void*>(GetWindowLongPtr(hTaskSwWnd, 0));
    if (!taskBand) {
        return nullptr;
    }

    void* taskBandForTaskListWndSite = taskBand;
    int vtableIndex = -1;
    for (int i = 0; *reinterpret_cast<void**>(taskBandForTaskListWndSite) !=
                    CTaskBand_ITaskListWndSite_vftable;
         i++) {
        if (i == 20) {
            return nullptr;
        }
        taskBandForTaskListWndSite =
            reinterpret_cast<void**>(taskBandForTaskListWndSite) + 1;
        vtableIndex = i + 1;
    }
    if (vtableIndex < 0) {
        vtableIndex = 0;
    }

    void* taskbarHostSharedPtr[2]{};
    CTaskBand_GetTaskbarHost_Original(taskBandForTaskListWndSite,
                                      taskbarHostSharedPtr);
    if (!taskbarHostSharedPtr[0] && !taskbarHostSharedPtr[1]) {
        return nullptr;
    }

    size_t taskbarElementIUnknownOffset = 0x10;
#if defined(_M_X64)
    const BYTE* b = reinterpret_cast<const BYTE*>(TaskbarHost_FrameHeight_Original);
    if (b[0] == 0x48 && b[1] == 0x83 && b[2] == 0xEC && b[4] == 0x48 &&
        b[5] == 0x83 && b[6] == 0xC1 && b[7] <= 0x7F) {
        taskbarElementIUnknownOffset = b[7];
    } else {
        Wh_Log(L"Unsupported TaskbarHost::FrameHeight");
    }
#endif

    auto* taskbarElementIUnknown = *reinterpret_cast<IUnknown**>(
        static_cast<BYTE*>(taskbarHostSharedPtr[0]) + taskbarElementIUnknownOffset);
    if (!taskbarElementIUnknown) {
        std__Ref_count_base__Decref_Original(taskbarHostSharedPtr[1]);
        return nullptr;
    }

    FrameworkElement taskbarElement = nullptr;
    HRESULT queryResult = taskbarElementIUnknown->QueryInterface(
        winrt::guid_of<FrameworkElement>(), winrt::put_abi(taskbarElement));

    auto result = taskbarElement ? taskbarElement.XamlRoot() : nullptr;
    (void)queryResult;
    std__Ref_count_base__Decref_Original(taskbarHostSharedPtr[1]);
    return result;
}

using RunFromWindowThreadProc_t = void(WINAPI*)(void* parameter);

bool RunFromWindowThread(HWND hWnd, RunFromWindowThreadProc_t proc,
                         void* procParam) {
    static const UINT runFromWindowThreadRegisteredMsg =
        RegisterWindowMessage(L"Windhawk_RunFromWindowThread_" WH_MOD_ID);

    struct RUN_FROM_WINDOW_THREAD_PARAM {
        RunFromWindowThreadProc_t proc;
        void* procParam;
    };

    DWORD dwThreadId = GetWindowThreadProcessId(hWnd, nullptr);
    if (dwThreadId == 0) {
        return false;
    }

    if (dwThreadId == GetCurrentThreadId()) {
        proc(procParam);
        return true;
    }

    HHOOK hook = SetWindowsHookEx(
        WH_CALLWNDPROC,
        [](int nCode, WPARAM wParam, LPARAM lParam) -> LRESULT {
            if (nCode == HC_ACTION) {
                const CWPSTRUCT* cwp = reinterpret_cast<const CWPSTRUCT*>(lParam);
                if (cwp->message == runFromWindowThreadRegisteredMsg) {
                    auto* param =
                        reinterpret_cast<RUN_FROM_WINDOW_THREAD_PARAM*>(cwp->lParam);
                    param->proc(param->procParam);
                }
            }
            return CallNextHookEx(nullptr, nCode, wParam, lParam);
        },
        nullptr, dwThreadId);
    if (!hook) {
        return false;
    }

    RUN_FROM_WINDOW_THREAD_PARAM param{proc, procParam};
    SendMessage(hWnd, runFromWindowThreadRegisteredMsg, 0,
                reinterpret_cast<LPARAM>(&param));
    UnhookWindowsHookEx(hook);
    return true;
}

void ApplySlotFromFile() {
    const int attempt = ++g_applySlotAttempts;

    HWND hTaskbarWnd = FindCurrentProcessTaskbarWnd();
    if (!hTaskbarWnd) {
        return;
    }

    bool posted = RunFromWindowThread(
        hTaskbarWnd,
        [](void*) {
            ApplyStyleFromObservedElements();
            if (g_slotInjected && g_slotRoot) {
                AttachSlotInteractions(g_slotRoot);
                ApplySlotContent(ReadSlotContent());
            }
        },
        nullptr);

    if (!posted && attempt <= 8) {
        Wh_Log(L"PrayerTray slot window-thread dispatch failed");
    }
}

VOID CALLBACK UpdateTimerProc(HWND, UINT, UINT_PTR, DWORD) {
    if (g_unloading) {
        return;
    }

    ApplySlotFromFile();
}

void StartUpdateTimer() {
    if (g_updateTimer) {
        return;
    }

    g_updateTimer = SetTimer(nullptr, 0, 1000, UpdateTimerProc);
}

void StopUpdateTimer() {
    if (g_updateTimer) {
        KillTimer(nullptr, g_updateTimer);
        g_updateTimer = 0;
    }
}

void ApplySettings() {
    if (!g_slotInjected) {
        return;
    }

    HWND hTaskbarWnd = FindCurrentProcessTaskbarWnd();
    if (!hTaskbarWnd) {
        Wh_Log(L"No taskbar found");
        return;
    }

    RunFromWindowThread(
        hTaskbarWnd,
        [](void*) {
            ApplySlotContent(ReadSlotContent());
        },
        nullptr);
}

bool HookTaskbarDllSymbols() {
    HMODULE module =
        LoadLibraryEx(L"taskbar.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
    if (!module) {
        Wh_Log(L"Failed to load taskbar.dll");
        return false;
    }

    WindhawkUtils::SYMBOL_HOOK taskbarDllHooks[] = {
        {
            {LR"(const CTaskBand::`vftable'{for `ITaskListWndSite'})"},
            &CTaskBand_ITaskListWndSite_vftable,
        },
        {
            {LR"(public: virtual class std::shared_ptr<class TaskbarHost> __cdecl CTaskBand::GetTaskbarHost(void)const )"},
            &CTaskBand_GetTaskbarHost_Original,
        },
        {
            {LR"(public: int __cdecl TaskbarHost::FrameHeight(void)const )"},
            &TaskbarHost_FrameHeight_Original,
        },
        {
            {LR"(public: void __cdecl std::_Ref_count_base::_Decref(void))"},
            &std__Ref_count_base__Decref_Original,
        },
    };

    return HookSymbols(module, taskbarDllHooks, ARRAYSIZE(taskbarDllHooks));
}

using IconView_IconView_t = void*(WINAPI*)(void* pThis);
IconView_IconView_t IconView_IconView_Original;

FrameworkElement TryGetFrameworkElementFromImplementation(void* pThis) {
    if (!pThis) {
        return nullptr;
    }

    FrameworkElement element = nullptr;
    try {
        auto object = reinterpret_cast<IUnknown**>(pThis)[1];
        if (object) {
            object->QueryInterface(winrt::guid_of<FrameworkElement>(),
                                   winrt::put_abi(element));
        }
    } catch (...) {
        return nullptr;
    }

    return element;
}

void TryApplySlotFromElement(FrameworkElement element) {
    if (g_unloading || !element) {
        return;
    }

    try {
        TrackObservedElement(element);

        if (IsSlotRootAlive()) {
            ApplySlotContent(ReadSlotContent());
            return;
        }

        auto xamlRoot = element.XamlRoot();
        if (!xamlRoot) {
            return;
        }

        ApplyStyle(xamlRoot);
    } catch (...) {
    }
}

void* WINAPI IconView_IconView_Hook(void* pThis) {
    void* ret = IconView_IconView_Original(pThis);

    if (g_unloading) {
        return ret;
    }

    FrameworkElement iconView = TryGetFrameworkElementFromImplementation(pThis);

    if (!iconView) {
        return ret;
    }

    TryApplySlotFromElement(iconView);

    g_loadedRevokers.emplace_back();
    auto revokerIt = g_loadedRevokers.end();
    --revokerIt;

    *revokerIt = iconView.Loaded(
        winrt::auto_revoke_t{},
        [revokerIt](winrt::Windows::Foundation::IInspectable const& sender,
                    RoutedEventArgs const&) {
            g_loadedRevokers.erase(revokerIt);

            if (g_unloading) {
                return;
            }

            auto iconView = sender.try_as<FrameworkElement>();
            if (!iconView) {
                return;
            }

            TryApplySlotFromElement(iconView);
        });

    return ret;
}

using SystemTrayFrame_OnApplyTemplate_t = void(WINAPI*)(void* pThis);
SystemTrayFrame_OnApplyTemplate_t SystemTrayFrame_OnApplyTemplate_Original;

void WINAPI SystemTrayFrame_OnApplyTemplate_Hook(void* pThis) {
    SystemTrayFrame_OnApplyTemplate_Original(pThis);

    if (g_unloading) {
        return;
    }

    TryApplySlotFromElement(TryGetFrameworkElementFromImplementation(pThis));
}

using SystemTrayFrame_MeasureOverride_t =
    winrt::Windows::Foundation::Size(WINAPI*)(
        void* pThis, winrt::Windows::Foundation::Size const& availableSize);
SystemTrayFrame_MeasureOverride_t SystemTrayFrame_MeasureOverride_Original;

winrt::Windows::Foundation::Size WINAPI SystemTrayFrame_MeasureOverride_Hook(
    void* pThis, winrt::Windows::Foundation::Size const& availableSize) {
    auto result = SystemTrayFrame_MeasureOverride_Original(pThis, availableSize);

    if (!g_unloading) {
        TryApplySlotFromElement(TryGetFrameworkElementFromImplementation(pThis));
    }

    return result;
}

bool HookTaskbarViewDllSymbols(HMODULE module) {
    WindhawkUtils::SYMBOL_HOOK symbolHooks[] = {
        {
            {LR"(public: __cdecl winrt::SystemTray::implementation::IconView::IconView(void))"},
            &IconView_IconView_Original,
            IconView_IconView_Hook,
            true,
        },
        {
            {LR"(protected: virtual void __cdecl winrt::SystemTray::implementation::SystemTrayFrame::OnApplyTemplate(void))",
             LR"(public: virtual void __cdecl winrt::SystemTray::implementation::SystemTrayFrame::OnApplyTemplate(void))"},
            &SystemTrayFrame_OnApplyTemplate_Original,
            SystemTrayFrame_OnApplyTemplate_Hook,
            true,
        },
        {
            {LR"(protected: virtual struct winrt::Windows::Foundation::Size __cdecl winrt::SystemTray::implementation::SystemTrayFrame::MeasureOverride(struct winrt::Windows::Foundation::Size const &))",
             LR"(public: virtual struct winrt::Windows::Foundation::Size __cdecl winrt::SystemTray::implementation::SystemTrayFrame::MeasureOverride(struct winrt::Windows::Foundation::Size const &))"},
            &SystemTrayFrame_MeasureOverride_Original,
            SystemTrayFrame_MeasureOverride_Hook,
            true,
        },
    };

    return HookSymbols(module, symbolHooks, ARRAYSIZE(symbolHooks));
}

HMODULE GetTaskbarViewModuleHandle() {
    HMODULE module = GetModuleHandle(L"Taskbar.View.dll");
    if (!module) {
        module = GetModuleHandle(L"ExplorerExtensions.dll");
    }
    return module;
}

void HandleLoadedModuleIfTaskbarView(HMODULE module, LPCWSTR lpLibFileName) {
    if (!g_taskbarViewDllLoaded && GetTaskbarViewModuleHandle() == module &&
        !g_taskbarViewDllLoaded.exchange(true)) {
        Wh_Log(L"Loaded %s", lpLibFileName);
        if (HookTaskbarViewDllSymbols(module)) {
            Wh_ApplyHookOperations();
        }
    }
}

using LoadLibraryExW_t = decltype(&LoadLibraryExW);
LoadLibraryExW_t LoadLibraryExW_Original;

HMODULE WINAPI LoadLibraryExW_Hook(LPCWSTR lpLibFileName, HANDLE hFile,
                                 DWORD dwFlags) {
    HMODULE module = LoadLibraryExW_Original(lpLibFileName, hFile, dwFlags);
    if (module) {
        HandleLoadedModuleIfTaskbarView(module, lpLibFileName);
    }
    return module;
}

}  // namespace

BOOL Wh_ModInit() {
    Wh_Log(L">");

    if (HMODULE taskbarViewModule = GetTaskbarViewModuleHandle()) {
        g_taskbarViewDllLoaded = true;
        if (!HookTaskbarViewDllSymbols(taskbarViewModule)) {
            Wh_Log(L"Hooking Taskbar.View.dll symbols failed; continuing");
        }
    } else {
        HMODULE kernelBaseModule = GetModuleHandle(L"kernelbase.dll");
        auto pKernelBaseLoadLibraryExW = reinterpret_cast<LoadLibraryExW_t>(
            GetProcAddress(kernelBaseModule, "LoadLibraryExW"));
        WindhawkUtils::SetFunctionHook(pKernelBaseLoadLibraryExW,
                                       LoadLibraryExW_Hook,
                                       &LoadLibraryExW_Original);
    }

    StartUpdateTimer();
    return TRUE;
}

void Wh_ModAfterInit() {
    Wh_Log(L">");

    if (!g_taskbarViewDllLoaded) {
        if (HMODULE taskbarViewModule = GetTaskbarViewModuleHandle()) {
            if (!g_taskbarViewDllLoaded.exchange(true)) {
                if (HookTaskbarViewDllSymbols(taskbarViewModule)) {
                    Wh_ApplyHookOperations();
                } else {
                    Wh_Log(L"Hooking Taskbar.View.dll symbols failed in AfterInit");
                }
            }
        }
    }

    ApplySettings();
}

void Wh_ModBeforeUninit() {
    g_unloading = true;
    StopUpdateTimer();
    g_loadedRevokers.clear();
    g_observedElements.clear();

    if (auto root = g_slotRoot) {
        root.Visibility(Visibility::Collapsed);
    }

    g_slotButton = nullptr;
}

void Wh_ModUninit() {
    Wh_Log(L">");
}

void Wh_ModSettingsChanged() {
    ApplySettings();
}
