using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace RadioReel.App.Infrastructure.Accessibility;

public static class AccessibilityHelper
{
    private static TextBlock? _assertiveBlock;
    private static TextBlock? _politeBlock;

    /// <summary>
    /// Must be called once from MainWindow after it loads.
    /// Pass two hidden TextBlocks that serve as live regions.
    /// </summary>
    public static void Initialize(TextBlock assertiveBlock, TextBlock politeBlock)
    {
        _assertiveBlock = assertiveBlock;
        _politeBlock = politeBlock;
        AutomationProperties.SetLiveSetting(assertiveBlock, AutomationLiveSetting.Assertive);
        AutomationProperties.SetLiveSetting(politeBlock, AutomationLiveSetting.Polite);
    }

    public static void AnnounceAssertive(string message)
    {
        Announce(_assertiveBlock, message);
    }

    public static void AnnouncePolite(string message)
    {
        Announce(_politeBlock, message);
    }

    private static void Announce(TextBlock? block, string message)
    {
        if (block is null || Application.Current is null) return;

        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            // Clear and re-set to trigger the live region announcement
            block.Text = string.Empty;
            block.Text = message;

            var peer = UIElementAutomationPeer.CreatePeerForElement(block);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        });
    }
}
