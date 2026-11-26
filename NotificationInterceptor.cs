using System.Windows.Threading;
using Windows.Foundation.Metadata;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DynamicIsland;

sealed class NotificationInterceptor
{
    readonly Dispatcher _dispatcher;
    readonly Action<string, string> _onNotification;
    UserNotificationListener? _listener;

    public NotificationInterceptor(Dispatcher dispatcher, Action<string, string> onNotification)
    {
        _dispatcher = dispatcher;
        _onNotification = onNotification;
    }

    public async Task StartAsync()
    {
        try
        {
            if (!ApiInformation.IsTypePresent("Windows.UI.Notifications.Management.UserNotificationListener"))
                return;

            _listener = UserNotificationListener.Current;
            var status = await _listener.RequestAccessAsync();
            if (status == UserNotificationListenerAccessStatus.Allowed)
            {
                _listener.NotificationChanged += Listener_NotificationChanged;
            }
        }
        catch (Exception ex)
        {
            App.Log(ex);
        }
    }

    void Listener_NotificationChanged(UserNotificationListener sender, UserNotificationChangedEventArgs args)
    {
        try
        {
            var notif = sender.GetNotification(args.UserNotificationId);
            if (notif?.Notification == null) return;

            var visual = notif.Notification.Visual;
            if (visual == null) return;

            var binding = visual.GetBinding(KnownNotificationBindings.ToastGeneric);
            if (binding == null) return;

            var texts = binding.GetTextElements();
            string title = texts.Count > 0 ? texts[0].Text : "";
            string body = texts.Count > 1 ? texts[1].Text : "";

            if (title.Length > 0 || body.Length > 0)
            {
                _dispatcher.InvokeAsync(() => _onNotification(title, body));
            }
        }
        catch { }
    }
}
