package com.secondwindgames.openednote;
import android.content.*;
public final class ReminderReceiver extends BroadcastReceiver {
    @Override public void onReceive(Context c, Intent i) {
        if (i == null) return;
        try { Reminders.deliver(c, i.getStringExtra("id"), i.getStringExtra("token")); }
        catch (Exception e) { android.util.Log.w("OpenedNote", "Reminder suppressed until data recovery: " + e.getClass().getSimpleName()); }
    }
}
