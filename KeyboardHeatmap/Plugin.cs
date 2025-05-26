using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Numerics;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using KeyboardHeatmap.Windows;

namespace KeyboardHeatmap;

using UseActionDelegate = ActionManager.Delegates.UseAction;
using UseActionLocationDelegate = ActionManager.Delegates.UseActionLocation;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    private const string CommandName = "/kbheat";

    public Configuration Configuration { get; set; }

    public readonly WindowSystem WindowSystem = new("KeyboardHeatmap");
    private ConfigWindow ConfigWindow { get; set; }
    private MainWindow MainWindow { get; set; }

    private readonly Hook<UseActionDelegate>? useActionHook;
    private readonly Hook<UseActionLocationDelegate>? useActionLocationHook;

    public Plugin()
    {
        unsafe
        {
            DefaultInitialization();

            useActionHook = GameInteropProvider.HookFromAddress<UseActionDelegate>(
                ActionManager.MemberFunctionPointers.UseAction,
                UseActionOverride
            );

            useActionHook.Enable();

            useActionLocationHook = GameInteropProvider.HookFromAddress<UseActionLocationDelegate>(
                ActionManager.MemberFunctionPointers.UseActionLocation,
                UseActionLocationOverride
            );

            useActionLocationHook.Enable();
        }
    }

    private unsafe bool UseActionOverride(
        ActionManager* thisPtr,
        ActionType actionType,
        uint actionId,
        ulong targetId,
        uint extraParam,
        ActionManager.UseActionMode mode,
        uint comboRouteId,
        bool* outOptAreaTargeted)
    {
        var result = useActionHook!.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);

        try
        {
            MainWindow.LastUsedAction = Utils.GetActionName(actionId);;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured when handling a macro save event.");
        }

        return result;
    }

    private unsafe bool UseActionLocationOverride(
        ActionManager* thisPtr,
        ActionType actionType,
        uint actionId,
        ulong targetId = 0xE000_0000,
        Vector3* location = null,
        uint extraParam = 0)
    {
        var result = useActionLocationHook!.Original(thisPtr, actionType, actionId, targetId, location, extraParam);

        try
        {
            MainWindow.LastUsedActionLocation = Utils.GetActionName(actionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occured when handling a macro save event.");
        }

        return result;
    }

    private void DefaultInitialization()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // you might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [KeyboardHeatmap] ===A cool log message from KeyboardHeatmap Plugin===
        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        useActionHook?.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
