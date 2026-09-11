package com.secondwindgames.openednote;
import android.content.*;
public final class RestoreReceiver extends BroadcastReceiver {
    @Override public void onReceive(Context c, Intent i) {
        if (i == null) return;
        String a = i.getAction();
        if (!Intent.ACTION_BOOT_COMPLETED.equals(a) && !Intent.ACTION_MY_PACKAGE_REPLACED.equals(a) && !Intent.ACTION_TIME_CHANGED.equals(a) && !Intent.ACTION_TIMEZONE_CHANGED.equals(a)) return;
        try { Reminders.sync(c); }
        catch (Exception e) { android.util.Log.w("OpenedNote", "Reminder recovery deferred: " + e.getClass().getSimpleName()); }
    }
}
