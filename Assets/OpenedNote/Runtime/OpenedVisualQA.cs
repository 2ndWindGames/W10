#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenedNote
{
    // Editor-only UI evidence. Never seeds production storage or ships in Android.
    public sealed class OpenedVisualQA : MonoBehaviour
    {
        IEnumerator Start()
        {
            var app=GetComponent<OpenedApp>();
            if(OpenedApp.DataPathOverride==null || !app.DataPath.Contains("BuildArtifacts"))throw new Exception("QA requires isolated data");
            var folder=Path.GetFullPath("BuildArtifacts/QA");
            var panel=GetComponent<UIDocument>().panelSettings;
            var target=new RenderTexture(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32);target.Create();panel.targetTexture=target;
            var log=new StringBuilder();
            try
            {
                app.Store.Reset();app.Navigate("home");yield return Pause();yield return Capture("empty");Visible("add-item");
                Click("add-item");yield return Pause();Visible("save-item");
                Click("save-item");yield return Pause();Check(app.CurrentPage=="edit" && app.State.items.Count==0,"Blank name rejected by real save button");
                app.Root.Q<TextField>("item-name").value="우유";
                app.Root.Q<TextField>("item-note").value="냉장고 문 안쪽 · 파란 뚜껑";
                Click("opened-date");yield return Pause();
                app.Root.Q<TextField>("date-exact").value=OpenedStore.Date(app.Store.Today.AddDays(-2));Click("date-apply");yield return Pause();
                Check(app.CurrentPage=="edit","Calendar input returns to editor");
                app.Root.Q<Toggle>("reminder-enabled").value=true;yield return Pause();Click("reminder-date");yield return Pause();
                app.Root.Q<TextField>("date-exact").value=OpenedStore.Date(app.Store.Today);Click("date-apply");yield return Pause();
                app.Root.Q<TextField>("reminder-time").value="09:00";Click("save-item");yield return Pause();
                Check(app.State.items.Count==1 && app.Store.Elapsed(app.State.items[0])==2,"Editor saves local dates and memo");
                Click("later-notifications");yield return Pause();
                Check(!app.State.notificationsEnabled,"Permission explanation can be declined");
                var milk=app.State.items[0].id;
                foreach(var value in new[] {
                    new OpenedItem{name="토마토 소스",category="food",openedLocalDate=OpenedStore.Date(app.Store.Today.AddDays(-5)),reminderLocalDate=OpenedStore.Date(app.Store.Today.AddDays(3)),note="냉장 보관 · 파스타 만들 때"},
                    new OpenedItem{name="선크림",category="care",openedLocalDate=OpenedStore.Date(app.Store.Today.AddDays(-30)),reminderLocalDate=OpenedStore.Date(app.Store.Today.AddDays(14)),note="현관 선반"}
                })app.Store.SaveItem(value,true);
                app.Store.Change(s=>s.notificationsEnabled=true);app.Navigate("home");yield return Pause();yield return Capture("home");
                Visible("add-item");Visible("nav-archive");Click("item-"+milk);yield return Pause();yield return Capture("detail");Visible("finish-item");Visible("edit-item");
                Click("edit-item");yield return Pause();yield return Capture("editor");Visible("save-item");
                Click("opened-date");yield return Pause();yield return Capture("calendar");
                app.Root.Q<TextField>("date-exact").value=OpenedStore.Date(app.Store.Today.AddDays(1));Click("date-apply");yield return Pause();
                Check(app.Root.Q("modal")!=null && app.Root.Q<Label>("toast")!=null,"Calendar rejects future opening without dismissing");
                app.CloseModal();Click("save-item");yield return Pause();
                Check(app.State.items.Count==3,"Editing preserves count");
                Click("finish-item");yield return Pause();
                Check(app.Store.Find(milk).status=="finished","Finish button archives");
                app.Navigate("archive");yield return Pause();yield return Capture("archive");
                app.Root.Q<TextField>("archive-search").value="없는 제품";yield return Pause();Check(app.Root.Q<Button>("item-"+milk)==null,"Archive search filters results");
                app.Root.Q<TextField>("archive-search").value="우유";yield return Pause();Click("item-"+milk);yield return Pause();Click("finish-item");yield return Pause();
                Check(app.Store.Find(milk).status=="active","Restore button returns to active");
                app.Navigate("settings");yield return Pause();yield return Capture("settings");
                app.Root.Q<Toggle>("large-text").value=true;app.OpenItem(milk);yield return Pause();yield return Capture("large-detail");Visible("finish-item");Visible("edit-item");
                Click("edit-item");yield return Pause();yield return Capture("large-editor");Visible("save-item");
                app.Back();yield return Pause();Click("cancel-action");yield return Pause();Check(app.CurrentPage=="edit","Cancel discard keeps draft");
                app.Back();yield return Pause();Click("confirm-action");yield return Pause();Check(app.CurrentPage=="detail","Confirm discard leaves editor");
                Click("delete-item");yield return Pause();Click("cancel-action");yield return Pause();Check(app.Store.Find(milk)!=null,"Cancelled deletion keeps item");
                Click("delete-item");yield return Pause();Click("confirm-action");yield return Pause();Check(app.Store.Find(milk)==null,"Confirmed deletion removes item");
                app.Navigate("settings");yield return Pause();Click("privacy");yield return Pause();yield return Capture("privacy");
                Check(app.CurrentPage=="privacy","Privacy is accessible in app");
                for(int i=0;i<25;i++) { var item=new OpenedItem{name="보관 제품 "+i,openedLocalDate=OpenedStore.Date(app.Store.Today)};app.Store.SaveItem(item,true);app.Store.Finish(item.id,true); }
                app.Navigate("archive");yield return Pause();app.Root.Q<TextField>("archive-search").value="";yield return Pause();
                Check(app.Root.Q<Label>("archive-page-label").text=="1 / 2","Large archive is paged to bound photo memory");
                Click("archive-next");yield return Pause();Check(app.Root.Q<Label>("archive-page-label").text=="2 / 2","Archive next page shows remaining records");
                Click("archive-prev");yield return Pause();Check(app.Root.Q<Label>("archive-page-label").text=="1 / 2","Archive previous page remains available");
                app.Navigate("settings");yield return Pause();Click("reset-all");yield return Pause();Click("confirm-action");yield return Pause();
                Check(app.State.items.Count==0 && app.CurrentPage=="home","Whole-data reset returns to empty home");
                File.WriteAllText(Path.Combine(folder,"visual-"+Screen.width+"x"+Screen.height+".txt"),log+"PASS ALL\n");
            }
            finally { panel.targetTexture=null;target.Release();Destroy(target); }
            Destroy(this);
            void Check(bool value,string name){if(!value)throw new Exception("UI QA: "+name);log.AppendLine("PASS "+name);}
            void Click(string name)
            {
                var button=app.Root.Q<Button>(name);if(button==null || !button.enabledInHierarchy)throw new Exception("UI QA missing button: "+name);
                typeof(Clickable).GetMethod("SimulateSingleClick",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(button.clickable,new object[]{null,0});
            }
            void Visible(string name)
            {
                var b=app.Root.Q<Button>(name);Check(b!=null && b.worldBound.yMin>=0 && b.worldBound.yMax<=app.Root.worldBound.yMax+1 && b.worldBound.height>=40,name+" visible, touch height >=40");
            }
            IEnumerator Pause(){yield return new WaitForSecondsRealtime(.23f);}
            IEnumerator Capture(string name)
            {
                yield return new WaitForSecondsRealtime(.15f);
                app.Root.panel.GetType().GetMethod("Repaint",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Invoke(app.Root.panel,new object[]{new Event{type=EventType.Repaint}});
                var old=RenderTexture.active;RenderTexture.active=target;
                var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();RenderTexture.active=old;
                File.WriteAllBytes(Path.Combine(folder,name+"-"+Screen.width+"x"+Screen.height+".png"),texture.EncodeToPNG());Destroy(texture);
            }
        }
    }
}
#endif
