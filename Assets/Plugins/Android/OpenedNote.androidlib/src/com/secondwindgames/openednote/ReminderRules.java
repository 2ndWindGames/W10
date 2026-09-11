package com.secondwindgames.openednote;
import java.time.*;

/** Pure rules shared by Android scheduling and host-side boundary tests. */
public final class ReminderRules {
    public static long at(String date, String time, ZoneId zone) {
        return LocalDate.parse(date).atTime(LocalTime.parse(time)).atZone(zone).toInstant().toEpochMilli();
    }
    public static boolean schedule(long at, long now, String token, String sent) {
        return at > now && !token.equals(sent);
    }
    public static boolean deliver(String date, String token, String expected, String sent, long at, long now, ZoneId zone) {
        return token.equals(expected) && !token.equals(sent) && at <= now &&
            LocalDate.parse(date).equals(Instant.ofEpochMilli(now).atZone(zone).toLocalDate());
    }
}
