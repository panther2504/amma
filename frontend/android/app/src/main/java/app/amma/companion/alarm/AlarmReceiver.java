package app.amma.companion.alarm;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

import androidx.core.content.ContextCompat;

import org.json.JSONObject;

/** Fired by AlarmManager at the reminder time. Starts the ringing service (or posts a quiet notice). */
public class AlarmReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context c, Intent intent) {
        String json = intent.getStringExtra("alarm");
        if (json == null) return;
        boolean quiet = false;
        try { quiet = new JSONObject(json).optBoolean("quiet"); } catch (Exception ignored) { }
        if (quiet) {
            AlarmNotifications.postQuiet(c, json);
            return;
        }
        Intent s = new Intent(c, AlarmService.class).setAction(AlarmService.ACTION_FIRE).putExtra("alarm", json);
        try {
            ContextCompat.startForegroundService(c, s);
        } catch (Exception e) {
            // If the system refuses to start the ringing service, still show the reminder.
            AlarmNotifications.postFallback(c, json);
        }
    }
}
