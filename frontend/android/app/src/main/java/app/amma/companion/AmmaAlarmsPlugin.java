package app.amma.companion;

import android.app.AlarmManager;
import android.app.NotificationManager;
import android.content.Context;
import android.content.Intent;
import android.media.AudioManager;
import android.net.Uri;
import android.os.Build;
import android.os.PowerManager;
import android.provider.Settings;

import androidx.core.app.NotificationManagerCompat;

import com.getcapacitor.JSArray;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

import org.json.JSONArray;
import org.json.JSONObject;

import app.amma.companion.alarm.AlarmNotifications;
import app.amma.companion.alarm.AlarmScheduler;
import app.amma.companion.alarm.AlarmService;
import app.amma.companion.alarm.AlarmStore;

/** Reminder alarms that ring on time, with sound, even when the app is closed or the phone is locked/silent. */
@CapacitorPlugin(name = "AmmaAlarms")
public class AmmaAlarmsPlugin extends Plugin {

    @PluginMethod
    public void schedule(PluginCall call) {
        JSArray alarms = call.getArray("alarms", new JSArray());
        AlarmNotifications.ensureChannels(getContext());
        AlarmScheduler.replaceAll(getContext(), alarms);
        JSObject ret = new JSObject();
        ret.put("scheduled", AlarmStore.schedule(getContext()).length());
        call.resolve(ret);
    }

    /** "Done" taps made on the alarm screen / notification while the app was closed. */
    @PluginMethod
    public void takeActions(PluginCall call) {
        JSONArray a = AlarmStore.takeActions(getContext());
        JSObject ret = new JSObject();
        try { ret.put("actions", new JSArray(a.toString())); } catch (Exception e) { ret.put("actions", new JSArray()); }
        call.resolve(ret);
    }

    /** Stops / removes reminders that were completed (here or on the other phone). */
    @PluginMethod
    public void dismiss(PluginCall call) {
        JSArray ids = call.getArray("ids", new JSArray());
        Context c = getContext();
        for (int i = 0; i < ids.length(); i++) {
            int id = ids.optInt(i);
            AlarmNotifications.cancel(c, id);
            try {
                JSONObject a = new JSONObject().put("id", id);
                c.startService(new Intent(c, AlarmService.class).setAction(AlarmService.ACTION_STOP + ".SILENT").putExtra("alarm", a.toString()));
            } catch (Exception ignored) { }
        }
        call.resolve();
    }

    @PluginMethod
    public void status(PluginCall call) {
        Context c = getContext();
        JSObject ret = new JSObject();
        ret.put("notifications", NotificationManagerCompat.from(c).areNotificationsEnabled());
        AlarmManager am = (AlarmManager) c.getSystemService(Context.ALARM_SERVICE);
        ret.put("exact", Build.VERSION.SDK_INT < 31 || (am != null && am.canScheduleExactAlarms()));
        boolean fullScreen = true;
        if (Build.VERSION.SDK_INT >= 34) {
            NotificationManager nm = c.getSystemService(NotificationManager.class);
            fullScreen = nm != null && nm.canUseFullScreenIntent();
        }
        ret.put("fullScreen", fullScreen);
        PowerManager pm = (PowerManager) c.getSystemService(Context.POWER_SERVICE);
        ret.put("batteryUnrestricted", pm == null || pm.isIgnoringBatteryOptimizations(c.getPackageName()));
        AudioManager audio = (AudioManager) c.getSystemService(Context.AUDIO_SERVICE);
        ret.put("alarmVolume", audio == null ? 0 : audio.getStreamVolume(AudioManager.STREAM_ALARM));
        ret.put("alarmVolumeMax", audio == null ? 0 : audio.getStreamMaxVolume(AudioManager.STREAM_ALARM));
        ret.put("scheduled", AlarmStore.schedule(c).length());
        ret.put("manufacturer", Build.MANUFACTURER);
        call.resolve(ret);
    }

    /** Opens the right system screen to fix a setting: exact | fullscreen | battery | notifications | app. */
    @PluginMethod
    public void openSettings(PluginCall call) {
        Context c = getContext();
        String which = call.getString("which", "app");
        Uri pkg = Uri.parse("package:" + c.getPackageName());
        Intent i;
        switch (which) {
            case "exact":
                i = Build.VERSION.SDK_INT >= 31 ? new Intent(Settings.ACTION_REQUEST_SCHEDULE_EXACT_ALARM, pkg) : appDetails(pkg);
                break;
            case "fullscreen":
                i = Build.VERSION.SDK_INT >= 34 ? new Intent(Settings.ACTION_MANAGE_APP_USE_FULL_SCREEN_INTENT, pkg) : appDetails(pkg);
                break;
            case "battery":
                i = new Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS, pkg);
                break;
            case "notifications":
                i = Build.VERSION.SDK_INT >= 26
                    ? new Intent(Settings.ACTION_APP_NOTIFICATION_SETTINGS).putExtra(Settings.EXTRA_APP_PACKAGE, c.getPackageName())
                    : appDetails(pkg);
                break;
            default:
                i = appDetails(pkg);
        }
        i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        try { c.startActivity(i); } catch (Exception e) {
            try { c.startActivity(appDetails(pkg).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)); } catch (Exception ignored) { }
        }
        call.resolve();
    }

    /** Sets the phone's ALARM volume (0-100%). Reminders ring on this volume even in silent mode. */
    @PluginMethod
    public void setAlarmVolume(PluginCall call) {
        AudioManager audio = (AudioManager) getContext().getSystemService(Context.AUDIO_SERVICE);
        if (audio != null) {
            int max = audio.getStreamMaxVolume(AudioManager.STREAM_ALARM);
            int pct = Math.max(10, Math.min(100, call.getInt("percent", 70)));
            try { audio.setStreamVolume(AudioManager.STREAM_ALARM, Math.max(1, max * pct / 100), AudioManager.FLAG_SHOW_UI); } catch (Exception ignored) { }
        }
        call.resolve();
    }

    /** A real alarm in N seconds, so you can lock the phone and check it rings. */
    @PluginMethod
    public void test(PluginCall call) {
        int seconds = Math.max(5, call.getInt("seconds", 10));
        try {
            JSONObject a = new JSONObject()
                .put("id", 1_999_999_000 + (int) (System.currentTimeMillis() % 900))
                .put("at", System.currentTimeMillis() + seconds * 1000L)
                .put("title", "💧 Test reminder")
                .put("body", "This is how Amma reminders ring — on both phones, even when locked or on silent.")
                .put("items", new JSONArray());
            AlarmNotifications.ensureChannels(getContext());
            AlarmScheduler.add(getContext(), a);
        } catch (Exception e) {
            call.reject(e.getMessage());
            return;
        }
        call.resolve();
    }
}
