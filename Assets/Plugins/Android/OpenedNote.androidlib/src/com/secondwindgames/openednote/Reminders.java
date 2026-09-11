package com.secondwindgames.openednote;

import android.app.*;
import android.content.*;
import android.net.Uri;
import android.provider.Settings;
import java.io.File;
import java.nio.file.Files;
import java.nio.charset.StandardCharsets;
import java.time.*;
import java.util.*;
import org.json.*;

/** Local, one-shot, inexact reminders. The committed app file is the authority. */
public final class Reminders {
    static final String CHANNEL = "opened_dates";
    static SharedPreferences prefs(Context c) { return c.getSharedPreferences("opened_note_reminders", Context.MODE_PRIVATE); }
    static NotificationManager manager(Context c) { return (NotificationManager)c.getSystemService(Context.NOTIFICATION_SERVICE); }
    static void channel(Context c) {
        NotificationChannel channel = new NotificationChannel(CHANNEL, "직접 정한 확인 날짜", NotificationManager.IMPORTANCE_DEFAULT);
        channel.setDescription("개봉한 제품을 확인하기로 정한 날짜에 한 번 알려드려요.");
        manager(c).createNotificationChannel(channel);
    }
    public static boolean allowed(Context c) {
        channel(c); return manager(c).areNotificationsEnabled() && manager(c).getNotificationChannel(CHANNEL).getImportance() != NotificationManager.IMPORTANCE_NONE;
    }
    static JSONObject read(Context c) throws Exception {
        File source = new File(c.getFilesDir(), "openednote/opened-note.json");
        JSONObject state = new JSONObject(new String(Files.readAllBytes(source.toPath()), StandardCharsets.UTF_8));
        if (state.getInt("schemaVersion") != 1 || !state.getBoolean("initialized")) throw new IllegalStateException("Unsupported data");
        return state;
    }
    static long time(JSONObject item) throws Exception {
        return ReminderRules.at(item.getString("reminderLocalDate"), item.getString("reminderTime"), ZoneId.systemDefault());
    }
    static String token(JSONObject item) throws Exception { return item.getString("id") + ":" + item.getInt("reminderVersion") + ":" + item.getString("reminderLocalDate") + ":" + item.getString("reminderTime"); }
    static PendingIntent pending(Context c, String id, String token, int flags) {
        Intent intent = new Intent(c, ReminderReceiver.class).setData(Uri.parse("openednote://reminder/" + Uri.encode(id))).putExtra("id", id).putExtra("token", token);
        return PendingIntent.getBroadcast(c, 0, intent, flags | PendingIntent.FLAG_IMMUTABLE);
    }
    static void cancel(Context c, String id, boolean dismiss) {
        PendingIntent intent = pending(c, id, "", PendingIntent.FLAG_NO_CREATE);
        if (intent != null) { ((AlarmManager)c.getSystemService(Context.ALARM_SERVICE)).cancel(intent); intent.cancel(); }
        if (dismiss) manager(c).cancel(id, 1);
    }
    public static synchronized void sync(Context c) throws Exception {
        channel(c);
        Set<String> previous = new HashSet<>(prefs(c).getStringSet("ids", Collections.emptySet()));
        JSONObject state;
        try { state = read(c); } catch (Exception e) {
            for (String id : previous) cancel(c, id, true);
            prefs(c).edit().remove("ids").commit(); throw e;
        }
        Set<String> ids = new HashSet<>();
        JSONArray list = state.getJSONArray("items");
        Map<String, JSONObject> current = new HashMap<>();
        for (int i=0; i<list.length(); i++) { JSONObject item = list.getJSONObject(i); ids.add(item.getString("id")); current.put(item.getString("id"), item); }
        for (String id : previous) {
            JSONObject item = current.get(id);
            boolean dismiss = !state.getBoolean("notificationsEnabled") || item == null ||
                !"active".equals(item.getString("status")) || item.optString("reminderLocalDate").isEmpty() ||
                !token(item).equals(prefs(c).getString("sent:" + id, ""));
            // Keep another product's delivered notification when editing or archiving one product.
            cancel(c, id, dismiss);
        }
        SharedPreferences.Editor edit = prefs(c).edit().putStringSet("ids", ids);
        for (String id : previous) if (!ids.contains(id)) edit.remove("sent:" + id);
        if (!edit.commit()) throw new IllegalStateException("Reminder storage full");
        if (!state.getBoolean("notificationsEnabled") || !allowed(c)) return;
        long now = System.currentTimeMillis();
        for (int i=0; i<list.length(); i++) {
            JSONObject item = list.getJSONObject(i);
            if (!"active".equals(item.getString("status")) || item.optString("reminderLocalDate").isEmpty()) continue;
            String id = item.getString("id"), token = token(item);
            long at = time(item);
            if (!ReminderRules.schedule(at, now, token, prefs(c).getString("sent:" + id, ""))) continue;
            ((AlarmManager)c.getSystemService(Context.ALARM_SERVICE)).setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, pending(c, id, token, PendingIntent.FLAG_UPDATE_CURRENT));
        }
    }
    static synchronized void deliver(Context c, String id, String expectedToken) throws Exception {
        JSONObject state = read(c);
        if (!state.getBoolean("notificationsEnabled") || !allowed(c)) return;
        JSONArray list = state.getJSONArray("items");
        for (int i=0; i<list.length(); i++) {
            JSONObject item = list.getJSONObject(i);
            if (!item.getString("id").equals(id) || !"active".equals(item.getString("status")) || item.optString("reminderLocalDate").isEmpty()) continue;
            String token = token(item);
            long at = time(item), now = System.currentTimeMillis();
            if (at > now) { sync(c); return; }
            // Delayed delivery on the selected local date is allowed; never send a days-old catch-up.
            if (!ReminderRules.deliver(item.getString("reminderLocalDate"), token, expectedToken, prefs(c).getString("sent:" + id, ""), at, now, ZoneId.systemDefault())) return;
            if (!prefs(c).edit().putString("sent:" + id, token).commit()) return;
            Intent launch = c.getPackageManager().getLaunchIntentForPackage(c.getPackageName());
            if (launch == null) return;
            launch.putExtra("openednote_item", id).setData(Uri.parse("openednote://item/" + id));
            PendingIntent open = PendingIntent.getActivity(c, 0, launch, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
            int icon = c.getResources().getIdentifier("ic_openednote", "drawable", c.getPackageName());
            Notification notification = new Notification.Builder(c, CHANNEL).setSmallIcon(icon)
                .setContentTitle(item.getString("name") + " · 오늘 확인")
                .setContentText("직접 정한 알림일이에요. 개봉 기록을 확인해 보세요.")
                .setColor(0xFFED642D).setContentIntent(open).setAutoCancel(true).setOnlyAlertOnce(true)
                .setVisibility(Notification.VISIBILITY_PRIVATE).build();
            manager(c).notify(id, 1, notification); return;
        }
    }
    public static String consumeItem(Activity activity) {
        Intent intent = activity.getIntent(); String id = intent.getStringExtra("openednote_item");
        intent.removeExtra("openednote_item"); return id == null ? "" : id;
    }
    public static synchronized void clear(Context c) {
        for (String id : new HashSet<>(prefs(c).getStringSet("ids", Collections.emptySet()))) cancel(c, id, true);
        if (!prefs(c).edit().clear().commit()) throw new IllegalStateException("Reminder cleanup failed");
        manager(c).cancelAll();
        PhotoActivity.cleanup(c);
    }
    public static void settings(Context c) {
        c.startActivity(new Intent(Settings.ACTION_CHANNEL_NOTIFICATION_SETTINGS).putExtra(Settings.EXTRA_APP_PACKAGE, c.getPackageName()).putExtra(Settings.EXTRA_CHANNEL_ID, CHANNEL).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK));
    }
}
