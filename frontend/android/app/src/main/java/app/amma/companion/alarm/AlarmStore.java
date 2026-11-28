package app.amma.companion.alarm;

import android.content.Context;
import android.content.SharedPreferences;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

/** Keeps the scheduled alarms (to restore them after a reboot) and the "Done" taps made while the app was closed. */
public final class AlarmStore {
    private static final String PREFS = "amma_alarms";

    private AlarmStore() {}

    private static SharedPreferences prefs(Context c) {
        return c.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    public static synchronized JSONArray schedule(Context c) {
        try { return new JSONArray(prefs(c).getString("schedule", "[]")); } catch (JSONException e) { return new JSONArray(); }
    }

    public static synchronized void saveSchedule(Context c, JSONArray a) {
        prefs(c).edit().putString("schedule", a.toString()).apply();
    }

    public static synchronized void addAction(Context c, JSONObject action) {
        try {
            JSONArray a = new JSONArray(prefs(c).getString("actions", "[]"));
            a.put(action);
            prefs(c).edit().putString("actions", a.toString()).apply();
        } catch (JSONException ignored) { }
    }

    /** Returns and clears the actions recorded by the native alarm screen / notification buttons. */
    public static synchronized JSONArray takeActions(Context c) {
        JSONArray a;
        try { a = new JSONArray(prefs(c).getString("actions", "[]")); } catch (JSONException e) { a = new JSONArray(); }
        prefs(c).edit().putString("actions", "[]").apply();
        return a;
    }
}
