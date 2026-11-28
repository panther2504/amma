package app.amma.companion;

import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;

import androidx.core.content.FileProvider;

import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;

/**
 * Downloads a new Amma APK and hands it to Android's package installer.
 * Android always shows its own "Update" confirmation for apps installed outside the Play Store.
 */
@CapacitorPlugin(name = "AppUpdater")
public class AppUpdaterPlugin extends Plugin {

    /** True when Android allows this app to install updates (the "Install unknown apps" setting). */
    @PluginMethod
    public void canInstall(PluginCall call) {
        JSObject ret = new JSObject();
        ret.put("allowed", canRequestInstalls());
        call.resolve(ret);
    }

    /** Opens the system setting that lets Amma install its own updates. */
    @PluginMethod
    public void openInstallSettings(PluginCall call) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            Intent i = new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES, Uri.parse("package:" + getContext().getPackageName()));
            i.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            getContext().startActivity(i);
        }
        call.resolve();
    }

    /** Downloads the APK from {@code url} (following redirects) and opens the installer. Emits "progress" events. */
    @PluginMethod
    public void downloadAndInstall(final PluginCall call) {
        final String url = call.getString("url");
        if (url == null || !url.startsWith("https://")) {
            call.reject("A secure download link is required.");
            return;
        }
        if (!canRequestInstalls()) {
            call.reject("INSTALL_NOT_ALLOWED");
            return;
        }
        new Thread(() -> {
            HttpURLConnection conn = null;
            try {
                File dir = new File(getContext().getCacheDir(), "updates");
                if (!dir.exists() && !dir.mkdirs()) throw new Exception("Could not prepare the download folder.");
                File apk = new File(dir, "Amma-update.apk");
                if (apk.exists()) apk.delete();

                URL current = new URL(url);
                for (int redirects = 0; ; redirects++) {
                    conn = (HttpURLConnection) current.openConnection();
                    conn.setInstanceFollowRedirects(false);
                    conn.setConnectTimeout(30000);
                    conn.setReadTimeout(60000);
                    int code = conn.getResponseCode();
                    if (code >= 300 && code < 400 && redirects < 6) {
                        String next = conn.getHeaderField("Location");
                        conn.disconnect();
                        URL target = new URL(current, next);
                        if (!"https".equals(target.getProtocol())) throw new Exception("Refusing an insecure download link.");
                        current = target;
                        continue;
                    }
                    if (code != 200) throw new Exception("Download failed (HTTP " + code + ").");
                    break;
                }

                long total = conn.getContentLengthLong();
                long done = 0;
                int lastPct = -1;
                try (InputStream in = conn.getInputStream(); OutputStream out = new FileOutputStream(apk)) {
                    byte[] buf = new byte[64 * 1024];
                    int n;
                    while ((n = in.read(buf)) != -1) {
                        out.write(buf, 0, n);
                        done += n;
                        int pct = total > 0 ? (int) (done * 100 / total) : -1;
                        if (pct != lastPct) {
                            lastPct = pct;
                            JSObject p = new JSObject();
                            p.put("percent", pct);
                            notifyListeners("progress", p);
                        }
                    }
                }

                Uri uri = FileProvider.getUriForFile(getContext(), getContext().getPackageName() + ".fileprovider", apk);
                Intent install = new Intent(Intent.ACTION_VIEW);
                install.setDataAndType(uri, "application/vnd.android.package-archive");
                install.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
                getContext().startActivity(install);
                call.resolve();
            } catch (Exception e) {
                call.reject(e.getMessage() != null ? e.getMessage() : "Download failed.");
            } finally {
                if (conn != null) conn.disconnect();
            }
        }).start();
    }

    private boolean canRequestInstalls() {
        return Build.VERSION.SDK_INT < Build.VERSION_CODES.O || getContext().getPackageManager().canRequestPackageInstalls();
    }
}
