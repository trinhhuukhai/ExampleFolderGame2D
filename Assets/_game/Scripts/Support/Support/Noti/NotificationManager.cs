using System;
using System.Globalization;
using UnityEngine;

#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

public class NotificationManager
    : SingletonMonoBehaviour<NotificationManager>
{
    [SerializeField] public bool isPauseToShowAds = false;

    private const int NOTI_HOUR = 20;
    private const int ACTIVE_WINDOW_HOURS = 24;

    // Giữ key hiện tại để đọc được dữ liệu đã lưu.
    private const string KEY_LAST_LEVEL_START_UTC =
        "noti_last_level_start_utc";

    private const string KEY_LAST_ACTIVE_DATE =
        "noti_last_active_local_date";

    private const string KEY_ACTIVE_DAY_COUNT =
        "noti_active_day_count";

    private bool _initialized;

    private struct NotiTemplate
    {
        public string title;
        public string msg;

        public NotiTemplate(string title, string msg)
        {
            this.title = title;
            this.msg = msg;
        }
    }

    private readonly NotiTemplate[] InactiveNotis =
    {
        new NotiTemplate(
            "⏳ Your sand is waiting!",
            "Tap a bucket and clear a quick puzzle now."),

        new NotiTemplate(
            "🖼️ A new picture is waiting!",
            "Come back and reveal your next pixel art today."),

        new NotiTemplate(
            "🌙 Ready to unwind?",
            "Sort some sand and relax for a few minutes.")
    };

    private readonly NotiTemplate[] ActiveNotis =
    {
        new NotiTemplate(
            "🎨 Love color sorting? Come back!",
            "Sand Fest is waiting for you."),

        new NotiTemplate(
            "🌈 Your next favorite puzzle is here",
            "Relax, sort, and watch the picture come alive!"),

        new NotiTemplate(
            "🔥 You're on a roll!",
            "Keep the streak going and clear the next level.")
    };

    private void Start()
    {
        Init();
    }

    public void Init()
    {
        if (_initialized)
            return;

        _initialized = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        NotificationSupport.RegisterChannel();
        CancelNotifications();

        Timer.ScheduleCondition(
            () => AdsManager.Ins != null &&
                  AdsManager.Ins.IsAdsSetupDone,
            RequestNotificationPermission);
#endif
    }

    private void RequestNotificationPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (AndroidNotificationCenter.UserPermissionToPost ==
                PermissionStatus.NotRequested)
            {
                new PermissionRequest();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning(
                "[Notification] Request permission failed: " +
                e.Message);
        }
#endif
    }

    /// <summary>
    /// Gọi mỗi khi thực sự bắt đầu level:
    /// Play, Next Level hoặc Retry.
    /// </summary>
    public void MarkLevelStarted()
    {
        Init();

        DateTime nowUtc = DateTime.UtcNow;

        string todayLocal = nowUtc
            .ToLocalTime()
            .ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

        string lastActiveDate = PlayerPrefs.GetString(
            KEY_LAST_ACTIVE_DATE, "");

        // Mỗi ngày có chơi chỉ tăng một lần.
        // Nhiều level trong cùng ngày không đổi mẫu.
        if (lastActiveDate != todayLocal)
        {
            int activeDayCount = PlayerPrefs.GetInt(
                KEY_ACTIVE_DAY_COUNT, 0);

            PlayerPrefs.SetInt(
                KEY_ACTIVE_DAY_COUNT,
                activeDayCount + 1);

            PlayerPrefs.SetString(
                KEY_LAST_ACTIVE_DATE,
                todayLocal);
        }

        PlayerPrefs.SetString(
            KEY_LAST_LEVEL_START_UTC,
            nowUtc.ToString(
                "o",
                CultureInfo.InvariantCulture));

        PlayerPrefs.Save();

        // Đang chơi: hủy lịch cũ.
        // Khi app ra nền sẽ đặt lại theo mốc mới.
        CancelNotifications();
    }

    private void OnApplicationPause(bool pause)
    {
        if (!_initialized)
            return;

        if (pause)
        {
            ScheduleNotifications();
        }
        else
        {
            CancelNotifications();
            isPauseToShowAds = false;
        }
    }

    private void OnApplicationQuit()
    {
        // Không reset isPauseToShowAds trước khi kiểm tra.
        ScheduleNotifications();
    }

    private void CancelNotifications()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidNotificationCenter.CancelAllNotifications();
#endif
    }

    private bool TryGetLastLevelStartUtc(
        out DateTime lastStartUtc)
    {
        string saved = PlayerPrefs.GetString(
            KEY_LAST_LEVEL_START_UTC, "");

        return DateTime.TryParse(
            saved,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal |
            DateTimeStyles.AdjustToUniversal,
            out lastStartUtc);
    }

    /// <summary>
    /// Mốc 20:00 đầu tiên >= thời điểm local truyền vào.
    /// </summary>
    private static DateTime NotiTimeAtOrAfter(
        DateTime fromLocal)
    {
        DateTime result =
            fromLocal.Date.AddHours(NOTI_HOUR);

        if (result < fromLocal)
            result = result.AddDays(1);

        return result;
    }

    private void ScheduleNotifications()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_initialized || isPauseToShowAds)
            return;

        CancelNotifications();

        // Chưa từng Start Level thì chưa đặt lịch.
        if (!TryGetLastLevelStartUtc(
                out DateTime lastStartUtc))
        {
            return;
        }

        DateTime nowUtc = DateTime.UtcNow;

        // Dữ liệu thời gian nằm trong tương lai:
        // không đặt lịch sai khi đồng hồ bị chỉnh lùi.
        if (lastStartUtc > nowUtc)
            return;

        DateTime nowLocal = nowUtc.ToLocalTime();

        DateTime inactiveFromUtc =
            lastStartUtc.AddHours(ACTIVE_WINDOW_HOURS);

        // ------------------------------------------
        // ACTIVE: xét tại thời điểm thông báo sẽ gửi.
        // ------------------------------------------
        DateTime next20h = NotiTimeAtOrAfter(nowLocal);

        if (next20h.ToUniversalTime() <= nowUtc)
            next20h = next20h.AddDays(1);

        // Chỉ gửi Active nếu đến 20h vẫn chưa đủ
        // 24 giờ kể từ lần Start Level cuối.
        if (next20h.ToUniversalTime() < inactiveFromUtc)
        {
            int activeDayCount = Mathf.Max(
                1,
                PlayerPrefs.GetInt(
                    KEY_ACTIVE_DAY_COUNT, 1));

            int index =
                (activeDayCount - 1) % ActiveNotis.Length;

            ScheduleAt(
                next20h,
                ActiveNotis[index],
                "Active " + (index + 1));
        }

        // ------------------------------------------
        // INACTIVE: neo theo lần Start Level cuối.
        // ------------------------------------------
        DateTime firstInactive20h = NotiTimeAtOrAfter(
            inactiveFromUtc.ToLocalTime());

        for (int i = 0; i < InactiveNotis.Length; i++)
        {
            DateTime fireLocal =
                firstInactive20h.AddDays(i);

            // Không gửi bù các ngày đã qua.
            // Mở app mà không chơi cũng không reset Day 1.
            if (fireLocal.ToUniversalTime() <= nowUtc)
                continue;

            ScheduleAt(
                fireLocal,
                InactiveNotis[i],
                "Inactive " + (i + 1));
        }
#endif
    }

    private void ScheduleAt(
        DateTime fireLocal,
        NotiTemplate template,
        string category)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (fireLocal.ToUniversalTime() <= DateTime.UtcNow)
            return;

        int id = NotificationSupport.CreateNotification(
            template.title,
            template.msg,
            fireLocal);

        Debug.Log(
            $"[Notification] {category}" +
            $" | ID={id}" +
            $" | Time={fireLocal:yyyy-MM-dd HH:mm} local" +
            $" | Title={template.title}");
#endif
    }
}