import com.secondwindgames.openednote.ReminderRules;
import java.time.*;
public final class ReminderRulesTest {
    static int count;
    static void check(boolean ok, String name) { if (!ok) throw new AssertionError(name); count++; System.out.println("PASS " + name); }
    public static void main(String[] args) {
        ZoneId seoul=ZoneId.of("Asia/Seoul"); long at=ReminderRules.at("2026-09-11","09:00",seoul);
        check(at==Instant.parse("2026-09-11T00:00:00Z").toEpochMilli(),"Selected local time maps to correct instant");
        check(ReminderRules.schedule(at,at-1,"v1",""),"Future reminder can be armed");
        check(!ReminderRules.schedule(at,at,"v1",""),"Exactly now never creates catch-up request");
        check(!ReminderRules.schedule(at,at+1,"v1",""),"Past time not newly armed");
        check(!ReminderRules.schedule(at,at-1,"v1","v1"),"Delivered reminder not rearmed after clock rollback");
        check(ReminderRules.deliver("2026-09-11","v1","v1","",at,at,seoul),"Scheduled request may deliver at target");
        check(!ReminderRules.deliver("2026-09-11","v1","v1","",at,at-1,seoul),"Never deliver early");
        check(ReminderRules.deliver("2026-09-11","v1","v1","",at,at+3600000,seoul),"Same-day inexact delay accepted");
        check(!ReminderRules.deliver("2026-09-11","v2","v1","",at,at,seoul),"Stale edited alarm suppressed");
        check(!ReminderRules.deliver("2026-09-11","v1","v1","v1",at,at,seoul),"Once-only delivery survives restore");
        check(!ReminderRules.deliver("2026-09-11","v1","v1","",at,at+15*3600000L,seoul),"Midnight ends delayed delivery window");
        check(ReminderRules.at("2024-03-01","09:00",seoul)-ReminderRules.at("2024-02-28","09:00",seoul)==2*86400000L,"Leap day calendar interval");
        ZoneId ny=ZoneId.of("America/New_York");
        check(ReminderRules.at("2026-03-08","02:30",ny)==ZonedDateTime.of(2026,3,8,3,30,0,0,ny).toInstant().toEpochMilli(),"DST nonexistent time moves through gap");
        check(ReminderRules.at("2026-11-01","01:30",ny)==Instant.parse("2026-11-01T05:30:00Z").toEpochMilli(),"DST overlap uses first occurrence");
        check(ReminderRules.at("2026-09-11","09:00",ZoneId.of("UTC"))-at==9*3600000L,"Timezone change changes future local-date instant");
        System.out.println("PASS ALL: "+count+" native reminder checks");
    }
}
