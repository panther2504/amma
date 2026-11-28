package app.amma.companion.alarm;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.os.Build;

import androidx.core.app.NotificationCompat;
import androidx.core.app.NotificationManagerCompat;

import org.json.JSONArray;
import org.json.JSONObject;

import app.amma.companion.R;

public final class AlarmNotifications {
    /** Silent channel: the alarm sound is played by AlarmService on the ALARM volume, so it works in silent mode. */
    public static final String ALARM_CHANNEL = "amma-alarm-v2";
    public static final String INFO_CHANNEL = "amma-info-v2";

    private AlarmNotifications() {}

    public static void ensureChannels(Context c) {
        if (Build.VERSION.SDK_INT < 26) return;
        NotificationManager nm = c.getSystemService(NotificationManager.class);
        if (nm == null) return;
        NotificationChannel alarm = new NotificationChannel(ALARM_CHANNEL, "Reminder alarms", NotificationManager.IMPORTANCE_HIGH);
        alarm.setDescription("Water, food, juice, tablet and checkup alarms");
        alarm.setSound(null, null);
        alarm.enableVibration(false);
        alarm.setBypassDnd(true);
        alarm.setLockscreenVisibility(Notification.VISIBILITY_PUBLIC);
        nm.createNotificationChannel(alarm);
        NotificationChannel info = new NotificationChannel(INFO_CHANNEL, "Amma notices", NotificationManager.IMPORTANCE_DEFAULT);
        nm.createNotificationChannel(info);
    }

    static PendingIntent action(Context c, String action, String json, int id) {
        Intent i = new Intent(c, AlarmService.class).setAction(action).putExtra("alarm", json);
        return PendingIntent.getService(c, (action.hashCode() * 31) + id, i, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    static PendingIntent fullScreen(Context c, String json, int id) {
        Intent i = new Intent(c, AlarmActivity.class).putExtra("alarm", json)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK | Intent.FLAG_ACTIVITY_NO_USER_ACTION);
        return PendingIntent.getActivity(c, id, i, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    static PendingIntent openApp(Context c) {
        Intent launch = c.getPackageManager().getLaunchIntentForPackage(c.getPackageName());
        if (launch == null) launch = new Intent();
        return PendingIntent.getActivity(c, 7002, launch, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    static boolean hasItems(JSONObject a) {
        JSONArray items = a.optJSONArray("items");
        return items != null && items.length() > 0;
    }

    /** The reminder notification. While ringing it opens full screen over the lock screen. */
    public static Notification build(Context c, JSONObject a, boolean ringing) {
        ensureChannels(c);
        String json = a.toString();
        int id = a.optInt("id");
        NotificationCompat.Builder b = new NotificationCompat.Builder(c, ALARM_CHANNEL)
            .setSmallIcon(R.drawable.ic_stat_amma)
            .setColor(0xFFC2497A)
            .setContentTitle(a.optString("title", "Amma reminder"))
            .setContentText(a.optString("body", ""))
            .setStyle(new NotificationCompat.BigTextStyle().bigText(a.optString("body", "")))
            .setPriority(NotificationCompat.PRIORITY_MAX)
            .setCategory(NotificationCompat.CATEGORY_ALARM)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            .setContentIntent(openApp(c))
            .setAutoCancel(true)
            .setOnlyAlertOnce(true)
            .setWhen(a.optLong("at", System.currentTimeMillis()))
            .setShowWhen(true);
        if (ringing) {
            b.setOngoing(true).setFullScreenIntent(fullScreen(c, json, id), true);
            b.setDeleteIntent(action(c, AlarmService.ACTION_STOP, json, id));
        }
        if (hasItems(a)) {
            b.addAction(0, "✓ Done", action(c, AlarmService.ACTION_DONE, json, id));
            b.addAction(0, "Snooze 10 min", action(c, AlarmService.ACTION_SNOOZE, json, id));
        }
        if (ringing) b.addAction(0, "Stop sound", action(c, AlarmService.ACTION_STOP, json, id));
        return b.build();
    }

    static void notifySafe(Context c, int id, Notification n) {
        try { NotificationManagerCompat.from(c).notify(id, n); } catch (SecurityException ignored) { /* notifications not allowed */ }
    }

    /** Used only if the ringing service can't start: a normal high-priority notification. */
    public static void postFallback(Context c, String json) {
        try {
            JSONObject a = new JSONObject(json);
            notifySafe(c, a.optInt("id"), build(c, a, false));
        } catch (Exception ignored) { }
    }

    /** Gentle notices (e.g. "open Amma to keep reminders on time"): no alarm sound. */
    public static void postQuiet(Context c, String json) {
        try {
            ensureChannels(c);
            JSONObject a = new JSONObject(json);
            Notification n = new NotificationCompat.Builder(c, INFO_CHANNEL)
                .setSmallIcon(R.drawable.ic_stat_amma).setColor(0xFFC2497A)
                .setContentTitle(a.optString("title")).setContentText(a.optString("body"))
                .setContentIntent(openApp(c)).setAutoCancel(true).build();
            notifySafe(c, a.optInt("id"), n);
        } catch (Exception ignored) { }
    }

    public static void cancel(Context c, int id) {
        NotificationManagerCompat.from(c).cancel(id);
    }
}
