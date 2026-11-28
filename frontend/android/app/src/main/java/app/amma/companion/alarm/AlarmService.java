package app.amma.companion.alarm;

import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ServiceInfo;
import android.content.res.AssetFileDescriptor;
import android.media.AudioAttributes;
import android.media.MediaPlayer;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.os.PowerManager;
import android.os.VibrationEffect;
import android.os.Vibrator;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import app.amma.companion.R;

/**
 * Rings a reminder: plays the chime on the ALARM audio stream (so it sounds even in silent / vibrate mode),
 * vibrates, and shows the full-screen alarm. Also handles the Done / Snooze / Stop buttons.
 */
public class AlarmService extends Service {
    public static final String ACTION_FIRE = "app.amma.alarm.RING";
    public static final String ACTION_DONE = "app.amma.alarm.DONE";
    public static final String ACTION_SNOOZE = "app.amma.alarm.SNOOZE";
    public static final String ACTION_STOP = "app.amma.alarm.STOP";
    /** Broadcast so an open alarm screen closes when the alarm is handled elsewhere. */
    public static final String ACTION_HANDLED = "app.amma.alarm.HANDLED";
    private static final long RING_MS = 45_000;
    private static final long SNOOZE_MS = 10 * 60_000;

    private final Handler handler = new Handler(Looper.getMainLooper());
    private MediaPlayer player;
    private Vibrator vibrator;
    private PowerManager.WakeLock wakeLock;
    private JSONObject ringing;
    private final Runnable timeout = this::ringTimeout;

    @Override
    public IBinder onBind(Intent intent) { return null; }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        String action = intent != null ? intent.getAction() : null;
        JSONObject a = parse(intent != null ? intent.getStringExtra("alarm") : null);
        if (action == null || a == null) { stopIfIdle(); return START_NOT_STICKY; }

        if (ACTION_FIRE.equals(action)) {
            if (ringing != null) { // a new alarm while another rings: leave the older one as a silent notification
                silence();
                AlarmNotifications.notifySafe(this, ringing.optInt("id"), AlarmNotifications.build(this, ringing, false));
            }
            ringing = a;
            int id = a.optInt("id");
            try {
                if (Build.VERSION.SDK_INT >= 29) startForeground(id, AlarmNotifications.build(this, a, true), ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PLAYBACK);
                else startForeground(id, AlarmNotifications.build(this, a, true));
            } catch (Exception e) {
                AlarmNotifications.postFallback(this, a.toString());
                stopSelf();
                return START_NOT_STICKY;
            }
            ring();
            return START_NOT_STICKY;
        }

        int id = a.optInt("id");
        boolean isCurrent = ringing != null && ringing.optInt("id") == id;
        if (isCurrent) { silence(); ringing = null; stopForegroundCompat(); }
        sendBroadcast(new Intent(ACTION_HANDLED).setPackage(getPackageName()).putExtra("id", id));

        if (ACTION_DONE.equals(action)) {
            JSONArray items = a.optJSONArray("items");
            if (items != null) {
                for (int i = 0; i < items.length(); i++) {
                    JSONObject it = items.optJSONObject(i);
                    if (it == null) continue;
                    try {
                        AlarmStore.addAction(this, new JSONObject()
                            .put("reminderId", it.optString("reminderId")).put("time", it.optString("time"))
                            .put("date", it.optString("date")).put("status", "done").put("at", System.currentTimeMillis()));
                    } catch (JSONException ignored) { }
                }
                AlarmScheduler.removeFollowUps(this, items);
            }
            AlarmNotifications.cancel(this, id);
        } else if (ACTION_SNOOZE.equals(action)) {
            try {
                JSONObject s = new JSONObject(a.toString());
                s.put("id", 1_500_000_000 + (int) ((System.currentTimeMillis() / 1000) % 600_000_000L));
                s.put("at", System.currentTimeMillis() + SNOOZE_MS);
                String t = a.optString("title");
                if (!t.startsWith("⏰ Snoozed")) s.put("title", "⏰ Snoozed · " + t);
                AlarmScheduler.add(this, s);
            } catch (JSONException ignored) { }
            AlarmNotifications.cancel(this, id);
        } else if (ACTION_STOP.equals(action)) {
            AlarmNotifications.notifySafe(this, id, AlarmNotifications.build(this, a, false));
        } else if ((ACTION_STOP + ".SILENT").equals(action)) {
            AlarmNotifications.cancel(this, id);
        }
        stopIfIdle();
        return START_NOT_STICKY;
    }

    private void ring() {
        handler.removeCallbacks(timeout);
        try {
            PowerManager pm = (PowerManager) getSystemService(Context.POWER_SERVICE);
            if (pm != null) {
                wakeLock = pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "amma:alarm");
                wakeLock.acquire(RING_MS + 5_000);
            }
        } catch (Exception ignored) { }
        try {
            player = new MediaPlayer();
            player.setAudioAttributes(new AudioAttributes.Builder()
                .setUsage(AudioAttributes.USAGE_ALARM)
                .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                .build());
            AssetFileDescriptor afd = getResources().openRawResourceFd(R.raw.amma_chime);
            player.setDataSource(afd.getFileDescriptor(), afd.getStartOffset(), afd.getLength());
            afd.close();
            player.setLooping(true);
            player.prepare();
            player.start();
        } catch (Exception ignored) { }
        try {
            vibrator = (Vibrator) getSystemService(Context.VIBRATOR_SERVICE);
            long[] pattern = {0, 600, 400, 600, 1400};
            if (vibrator != null) {
                if (Build.VERSION.SDK_INT >= 26) {
                    vibrator.vibrate(VibrationEffect.createWaveform(pattern, 0),
                        new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_ALARM).build());
                } else {
                    vibrator.vibrate(pattern, 0);
                }
            }
        } catch (Exception ignored) { }
        handler.postDelayed(timeout, RING_MS);
    }

    /** After 45 s the sound stops; the reminder stays in the notification shade with its buttons. */
    private void ringTimeout() {
        if (ringing == null) return;
        JSONObject a = ringing;
        silence();
        ringing = null;
        stopForegroundCompat();
        AlarmNotifications.notifySafe(this, a.optInt("id"), AlarmNotifications.build(this, a, false));
        sendBroadcast(new Intent(ACTION_HANDLED).setPackage(getPackageName()).putExtra("id", a.optInt("id")));
        stopIfIdle();
    }

    private void silence() {
        handler.removeCallbacks(timeout);
        if (player != null) { try { player.stop(); } catch (Exception ignored) { } player.release(); player = null; }
        if (vibrator != null) { try { vibrator.cancel(); } catch (Exception ignored) { } vibrator = null; }
        if (wakeLock != null && wakeLock.isHeld()) { try { wakeLock.release(); } catch (Exception ignored) { } }
        wakeLock = null;
    }

    private void stopForegroundCompat() {
        if (Build.VERSION.SDK_INT >= 24) stopForeground(Service.STOP_FOREGROUND_REMOVE);
        else stopForeground(true);
    }

    private void stopIfIdle() {
        if (ringing == null) stopSelf();
    }

    @Override
    public void onDestroy() {
        silence();
        super.onDestroy();
    }

    private static JSONObject parse(String json) {
        if (json == null) return null;
        try { return new JSONObject(json); } catch (JSONException e) { return null; }
    }
}
