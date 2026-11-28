package app.amma.companion.alarm;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Build;
import android.os.Bundle;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;

import androidx.appcompat.app.AppCompatActivity;
import androidx.core.content.ContextCompat;

import org.json.JSONObject;

import java.text.DateFormat;
import java.util.Date;

/** Full-screen alarm shown over the lock screen, with big Done / Snooze / Stop buttons. */
public class AlarmActivity extends AppCompatActivity {
    private String json;
    private int alarmId;

    private final BroadcastReceiver handled = new BroadcastReceiver() {
        @Override public void onReceive(Context c, Intent i) { if (i.getIntExtra("id", -1) == alarmId) finish(); }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (Build.VERSION.SDK_INT >= 27) { setShowWhenLocked(true); setTurnScreenOn(true); }
        else getWindow().addFlags(WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED | WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);

        json = getIntent().getStringExtra("alarm");
        JSONObject a;
        try { a = new JSONObject(json == null ? "{}" : json); } catch (Exception e) { finish(); return; }
        alarmId = a.optInt("id");

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setGravity(Gravity.CENTER);
        int pad = dp(28);
        root.setPadding(pad, pad, pad, pad);
        root.setBackground(new GradientDrawable(GradientDrawable.Orientation.TL_BR, new int[]{0xFF5E1F47, 0xFFA93D6C, 0xFFE2849A}));

        TextView time = text(DateFormat.getTimeInstance(DateFormat.SHORT).format(new Date()), 54, true);
        TextView title = text(a.optString("title", "Amma reminder"), 26, true);
        title.setPadding(0, dp(18), 0, dp(8));
        TextView body = text(a.optString("body", ""), 17, false);
        body.setAlpha(0.92f);
        root.addView(time);
        root.addView(title);
        root.addView(body);

        LinearLayout buttons = new LinearLayout(this);
        buttons.setOrientation(LinearLayout.VERTICAL);
        buttons.setPadding(0, dp(36), 0, 0);
        if (AlarmNotifications.hasItems(a)) {
            buttons.addView(button("✓  Done", 0xFFFFFFFF, 0xFFA93D6C, AlarmService.ACTION_DONE));
            buttons.addView(button("Snooze 10 minutes", 0x33FFFFFF, Color.WHITE, AlarmService.ACTION_SNOOZE));
        }
        buttons.addView(button("Stop sound", 0x22FFFFFF, Color.WHITE, AlarmService.ACTION_STOP));
        root.addView(buttons, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
        setContentView(root);

        ContextCompat.registerReceiver(this, handled, new IntentFilter(AlarmService.ACTION_HANDLED), ContextCompat.RECEIVER_NOT_EXPORTED);
    }

    @Override
    protected void onDestroy() {
        try { unregisterReceiver(handled); } catch (Exception ignored) { }
        super.onDestroy();
    }

    private TextView text(String s, int sp, boolean bold) {
        TextView t = new TextView(this);
        t.setText(s);
        t.setTextColor(Color.WHITE);
        t.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        t.setGravity(Gravity.CENTER);
        if (bold) t.setTypeface(Typeface.DEFAULT_BOLD);
        return t;
    }

    private Button button(String label, int bg, int fg, String action) {
        Button b = new Button(this);
        b.setText(label);
        b.setAllCaps(false);
        b.setTextSize(TypedValue.COMPLEX_UNIT_SP, 18);
        b.setTextColor(fg);
        b.setTypeface(Typeface.DEFAULT_BOLD);
        GradientDrawable d = new GradientDrawable();
        d.setColor(bg);
        d.setCornerRadius(dp(20));
        b.setBackground(d);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, dp(60));
        lp.topMargin = dp(12);
        b.setLayoutParams(lp);
        b.setOnClickListener(v -> {
            startService(new Intent(this, AlarmService.class).setAction(action).putExtra("alarm", json));
            finish();
        });
        return b;
    }

    private int dp(int v) {
        return (int) TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, v, getResources().getDisplayMetrics());
    }
}
