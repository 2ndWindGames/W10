using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenedNote;
using UnityEngine;

public static class OpenedChecks
{
    sealed class MemoryDisk : IOpenedDisk
    {
        public Dictionary<string,string> files = new Dictionary<string,string>();
        public bool fail;
        public bool Exists(string suffix) => files.ContainsKey(suffix);
        public string Read(string suffix) => files[suffix];
        public void Write(string json) { if(fail)throw new IOException("Injected full disk");if(files.ContainsKey("")) files[".bak"]=files[""];files[""]=json; }
        public void RemoveRecoveryCopies() { files.Remove(".bak"); files.Remove(".tmp"); }
    }
    public static void Run()
    {
        var log = new StringBuilder(); int total=0;
        void Check(bool value,string name) { if(!value)throw new Exception("FAIL "+name); log.AppendLine("PASS "+name);total++; }
        void Reject(Action action,string name) { try { action(); } catch(ArgumentException) { Check(true,name);return; } throw new Exception("Not rejected: "+name); }
        var now=new DateTime(2026,9,11,10,0,0);var disk=new MemoryDisk();var store=new OpenedStore(disk,()=>now);
        OpenedItem Item(string name="우유") => new OpenedItem { name=name,openedLocalDate="2026-09-09",reminderLocalDate="2026-09-11",reminderTime="09:00" };
        Check(store.State.items.Count==0,"First launch is empty, no fabricated user data");
        var milk=Item();store.SaveItem(milk,true);Check(store.Elapsed(store.Find(milk.id))==2,"Elapsed is local calendar days");
        Check(store.DueLabel(milk)=="오늘 확인","Past time today is displayed as today");
        Check(store.ReminderAt(milk)<store.Now,"A past time is identifiable and never moved to now");
        store.SaveItem(Item(),true);Check(store.State.items.Count==2,"Duplicate names retain distinct records");
        Reject(()=>store.SaveItem(milk,true),"Duplicate save ID rejected");
        Check(store.State.items.Count==2,"Duplicate save cannot duplicate item");
        var wrong=Item();wrong.name="  ";Reject(()=>store.SaveItem(wrong,true),"Blank title blocked");
        wrong=Item(new string('가',41));Reject(()=>store.SaveItem(wrong,true),"41 characters blocked");
        Check(OpenedStore.TextLength("😀")==1,"Supplementary Unicode character counts once");
        wrong=Item(new string('가',40));store.SaveItem(wrong,true);Check(store.State.items.Count==3,"40 Korean characters accepted");
        wrong=Item();wrong.openedLocalDate="2026-09-12";Reject(()=>store.SaveItem(wrong,true),"Future opening blocked");
        wrong=Item();wrong.reminderLocalDate="2026-09-08";Reject(()=>store.SaveItem(wrong,true),"Reminder before opening blocked");
        wrong=Item();wrong.openedLocalDate="2026-02-29";Reject(()=>store.SaveItem(wrong,true),"Invalid non-leap day blocked");
        wrong=Item();wrong.reminderTime="24:00";Reject(()=>store.SaveItem(wrong,true),"Invalid 24-hour time blocked");
        wrong=Item();wrong.photoFile="../secret.jpg";Reject(()=>store.SaveItem(wrong,true),"Private photo traversal blocked");
        wrong=Item();wrong.note=new string('메',201);Reject(()=>store.SaveItem(wrong,true),"Oversize note blocked");
        wrong=Item();wrong.reminderLocalDate="";store.SaveItem(wrong,true);Check(!store.ReminderAt(wrong).HasValue,"No-reminder record allowed");
        wrong=Item();wrong.openedLocalDate="2024-02-29";store.SaveItem(wrong,true);Check(store.Find(wrong.id)!=null,"Leap day accepted");
        var copy=OpenedStore.Clone(store.Find(milk.id));copy.name="저지방 우유";store.SaveItem(copy,false);Check(store.Find(milk.id).name=="저지방 우유","Edit updates same product");
        Check(store.Find(milk.id).reminderVersion==1,"Text edit keeps one-shot identity");
        copy=OpenedStore.Clone(store.Find(milk.id));copy.reminderLocalDate="2026-09-15";store.SaveItem(copy,false);Check(store.Find(milk.id).reminderVersion==2,"Reminder change invalidates stale request");
        store.Finish(milk.id,true);Check(store.Find(milk.id).status=="finished" && store.Find(milk.id).finishedAt.Length>0,"Finish archives with timestamp");
        Check(!store.Sorted(false).Any(x=>x.id==milk.id),"Finished excluded from active list");
        Check(store.Sorted(true).Any(x=>x.id==milk.id),"Finished visible in archive");
        store.Finish(milk.id,false);Check(store.Find(milk.id).reminderVersion==2 && store.Find(milk.id).finishedAt=="","Restore retains reminder identity");
        now=new DateTime(2026,9,12,0,0,1);Check(store.Elapsed(store.Find(milk.id))==3,"Midnight recalculates elapsed");
        Check(store.DueLabel(Item())=="알림일 1일 지남","Overdue date remains a record");
        now=new DateTime(2026,10,1);var edge=Item();edge.openedLocalDate="2026-09-30";Check(store.Elapsed(edge)==1,"Month boundary");
        now=new DateTime(2024,3,1);edge.openedLocalDate="2024-02-28";Check(store.Elapsed(edge)==2,"Leap-year boundary");
        now=new DateTime(2026,9,11,10,0,0);
        var restored=new OpenedStore(disk,()=>now);Check(restored.State.items.Count==store.State.items.Count,"Restart restores all committed data");
        Check(restored.State.items.Count(x=>x.status=="finished")==0,"No phantom archive after JSON round-trip");
        var before=JsonUtility.ToJson(store.State);disk.fail=true;
        try {store.SaveItem(Item("실패"),true);throw new Exception("Expected I/O failure");}catch(IOException){}
        Check(JsonUtility.ToJson(store.State)==before,"Failed write leaves memory unchanged");disk.fail=false;
        Check(!new OpenedStore(disk,()=>now).State.items.Any(x=>x.name=="실패"),"Failed save absent after restart");
        disk.files[""]="broken";var recovered=new OpenedStore(disk,()=>now);Check(recovered.Recovered && !recovered.ReadOnly,"Corrupt primary recovers valid backup");
        recovered.SaveItem(Item("복구 후"),true);Check(new OpenedStore(disk,()=>now).State.items.Any(x=>x.name=="복구 후"),"Recovery can commit again");
        var future=new MemoryDisk();future.files[""]=JsonUtility.ToJson(new OpenedState()).Replace("\"schemaVersion\":1","\"schemaVersion\":2");future.files[".bak"]=JsonUtility.ToJson(new OpenedState());
        Check(new OpenedStore(future,()=>now).ReadOnly,"New schema never downgraded through backup");
        var broken=new MemoryDisk();broken.files[""]="{}";Check(new OpenedStore(broken).ReadOnly,"Incomplete JSON protected");
        var interrupted=new MemoryDisk();interrupted.files[".tmp"]=JsonUtility.ToJson(new OpenedState());Check(new OpenedStore(interrupted).ReadOnly,"Uncommitted first write protected");
        store=new OpenedStore(new MemoryDisk(),()=>now);
        for(int i=0;i<15;i++)store.SaveItem(Item("제품 "+i),true);
        Reject(()=>store.SaveItem(Item("16번째"),true),"Active free limit enforced");
        var archived=store.State.items[0].id;store.Finish(archived,true);store.SaveItem(Item("새 제품"),true);
        Check(store.State.items.Count==16,"Archive does not consume active allowance");
        Reject(()=>store.Finish(archived,false),"Restore respects active limit without deleting archive");
        Check(store.Find(archived).status=="finished","Failed restore keeps archived product");
        store.Delete(store.State.items.Last().id);store.Finish(archived,false);Check(store.State.items.Count==15,"Delete releases slot for restoration");
        var ordering=new OpenedStore(new MemoryDisk(),()=>now);
        var past=Item("지난 알림");past.reminderLocalDate="2026-09-10";var upcoming=Item("다가올 알림");upcoming.reminderLocalDate="2026-09-13";var none=Item("알림 없음");none.reminderLocalDate="";
        ordering.SaveItem(none,true);ordering.SaveItem(upcoming,true);ordering.SaveItem(past,true);
        Check(ordering.Sorted(false).First().id==past.id,"Default ordering puts overdue first");
        Check(ordering.Sorted(false).Last().id==none.id,"No-reminder records sort last");
        ordering.Change(s=>s.sortMode="near");Check(ordering.Sorted(false).First().id==upcoming.id,"Upcoming sort prioritizes future dates");
        ordering.Finish(past.id,true);Check(ordering.Sorted(true,"all","지난").Single().id==past.id,"Archive search matches name");
        ordering.Delete(past.id);Check(ordering.Find(past.id)==null,"Permanent delete removes product");
        ordering.Reset();Check(new OpenedStore(new MemoryDisk()).State.items.Count==0 && ordering.State.items.Count==0,"Reset returns to empty state");
        // Real disk atomic-write and tombstone verification.
        string dir=Path.GetFullPath("BuildArtifacts/QA/storage");Directory.CreateDirectory(dir);string file=Path.Combine(dir,"test.json");
        var physical=new OpenedStore(file,()=>now);physical.Reset();physical.SaveItem(Item(),true);
        Check(File.Exists(file) && File.Exists(file+".bak") && !File.Exists(file+".tmp"),"Disk writes atomic primary and recovery copy");
        physical.Reset();Check(!File.Exists(file+".bak") && new OpenedStore(file).State.items.Count==0,"Reset removes recovery copies and survives restart");
        Directory.CreateDirectory("BuildArtifacts/QA");File.WriteAllText("BuildArtifacts/QA/data-checks.txt",log+"PASS ALL: "+total+" checks\n");Debug.Log("OpenedNote: "+total+" data checks passed.");
    }
}
