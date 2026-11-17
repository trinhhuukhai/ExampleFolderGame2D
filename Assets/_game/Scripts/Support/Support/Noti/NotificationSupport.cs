using System;
using UnityEngine;

#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

public static class NotificationSupport
{
    const string CHANNEL_ID = "SandFest";
    const string CHANNEL_NAME = "SandFest";
    const string CHANNEL_DESC = "SandFest";

    public static int CurrentUnix =>
        (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

#if UNITY_ANDROID
    public static void RegisterChannel()
    {
        //AndroidNotificationCenter.DeleteNotificationChannel(CHANNEL_ID);
        var c = new AndroidNotificationChannel
        {
            Id = CHANNEL_ID,
            Name = CHANNEL_NAME,
            Importance = Importance.High,
            Description = CHANNEL_DESC,
            EnableLights = true,
            EnableVibration = true,
            LockScreenVisibility = LockScreenVisibility.Public,
            CanBypassDnd = true,
            CanShowBadge = true,
        };
        AndroidNotificationCenter.RegisterNotificationChannel(c);
    }

    // Đặt lịch tại thời điểm cụ thể (local time)
    public static int CreateNotification(string title, string content, DateTime fireLocal)
    {
        var n = new AndroidNotification
        {
            Title = title,
            Text = content,
            FireTime = fireLocal,            // LocalTime
            ShowTimestamp = true,
            Style = NotificationStyle.BigTextStyle,
            SmallIcon = "small_icon",
            LargeIcon = "large_icon",
            Color = new Color32(255, 70, 0, 255)
        };
        return AndroidNotificationCenter.SendNotification(n, CHANNEL_ID);
    }

    // Đặt lịch sau một khoảng thời gian (tính từ bây giờ, local)
    public static int CreateAfter(string title, string content, TimeSpan delay)
    {
        return CreateNotification(title, content, DateTime.Now + delay);
    }
#endif

    public static void CreateNotificationDayZ(string content, int day, int hour, int minute)
    {
#if UNITY_ANDROID
        DateTime cur = DateTime.Now;
        DateTime next = cur.AddDays(day);
        next = new DateTime(next.Year, next.Month, next.Day, hour, minute, 0);
        if (next > cur)
            CreateNotification("Reminder", content, next);
#endif
    }
}
