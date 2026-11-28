package app.amma.companion.alarm;

import android.app.AlarmManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.os.Build;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

/**
 * Schedules reminders as real alarms (AlarmManager.setAlarmClock): exact, allowed in Doze / battery saver,
 * and delivered whether or not the app is running.
 */
public final class AlarmScheduler {
    private AlarmScheduler() {}

    static PendingIntent firePendingIntent(Context c, int id, String json) {
        Intent i = new Intent(c, AlarmReceiver.class)
            .setAction("app.amma.alarm.FIRE." + id)
            .putExtra("alarm", json);
        return PendingIntent.getBroadcast(c, id, i, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    private static PendingIntent showPendingIntent(Context c) {
        Intent launch = c.getPackageManager().getLaunchIntentForPackage(c.getPackageName());
        if (launch == null) launch = new Intent();
        return PendingIntent.getActivity(c, 7001, launch, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    public static void set(Context c, JSONObject a) {
        AlarmManager am = (AlarmManager) c.getSystemService(Context.ALARM_SERVICE);
        long at = a.optLong("at");
        if (am == null || at <= System.currentTimeMillis()) return;
        PendingIntent op = firePendingIntent(c, a.optInt("id"), a.toString());
        try {
            if (Build.VERSION.SDK_INT < 31 || am.canScheduleExactAlarms()) {
                am.setAlarmClock(new AlarmManager.AlarmClockInfo(at, showPendingIntent(c)), op);
            } else {
                am.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, op);
            }
        } catch (SecurityException e) {
            am.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, op);
        }
    }

    public static void cancel(Context c, int id) {
        AlarmManager am = (AlarmManager) c.getSystemService(Context.ALARM_SERVICE);
        if (am != null) am.cancel(firePendingIntent(c, id, "{}"));
    }

    /** Replaces every scheduled alarm with the given list. */
    public static void replaceAll(Context c, JSONArray alarms) {
        JSONArray old = AlarmStore.schedule(c);
        for (int i = 0; i < old.length(); i++) {
            JSONObject o = old.optJSONObject(i);
            if (o != null) cancel(c, o.optInt("id"));
        }
        JSONArray keep = new JSONArray();
        long now = System.currentTimeMillis();
        for (int i = 0; i < alarms.length(); i++) {
            JSONObject a = alarms.optJSONObject(i);
            if (a == null || a.optLong("at") <= now) continue;
            set(c, a);
            keep.put(a);
        }
        AlarmStore.saveSchedule(c, keep);
    }

    /** Adds one alarm (e.g. a snooze) without touching the others. */
    public static void add(Context c, JSONObject a) {
        JSONArray s = AlarmStore.schedule(c);
        s.put(a);
        AlarmStore.saveSchedule(c, s);
        set(c, a);
    }

    /** After a reboot, app update or clock change: schedule again everything still in the future. */
    public static void restore(Context c) {
        replaceAll(c, AlarmStore.schedule(c));
    }

    /** When an item is marked done, its "still pending" follow-up alarm is no longer needed. */
    public static void removeFollowUps(Context c, JSONArray doneItems) {
        JSONArray s = AlarmStore.schedule(c);
        JSONArray keep = new JSONArray();
        for (int i = 0; i < s.length(); i++) {
            JSONObject a = s.optJSONObject(i);
            if (a == null) continue;
            JSONArray items = a.optJSONArray("items");
            if (items == null || items.length() == 0) { keep.put(a); continue; }
            JSONArray left = new JSONArray();
            for (int j = 0; j < items.length(); j++) {
                JSONObject it = items.optJSONObject(j);
                if (it == null) continue;
                if (it.optBoolean("followUp") && contains(doneItems, it)) continue;
                left.put(it);
            }
            if (left.length() == 0) { cancel(c, a.optInt("id")); continue; }
            if (left.length() != items.length()) {
                try { a.put("items", left); } catch (JSONException ignored) { }
                set(c, a);
            }
            keep.put(a);
        }
        AlarmStore.saveSchedule(c, keep);
    }

    private static boolean contains(JSONArray list, JSONObject it) {
        for (int k = 0; k < list.length(); k++) {
            JSONObject d = list.optJSONObject(k);
            if (d != null && d.optString("reminderId").equals(it.optString("reminderId"))
                && d.optString("time").equals(it.optString("time")) && d.optString("date").equals(it.optString("date"))) return true;
        }
        return false;
    }
}
