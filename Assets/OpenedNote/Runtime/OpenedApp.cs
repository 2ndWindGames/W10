using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace OpenedNote
{
    public sealed class OpenedApp : MonoBehaviour
    {
        public static string DataPathOverride;
        public OpenedStore Store { get; private set; }
        public OpenedState State => Store.State;
        public VisualElement Root => root;
        public string CurrentPage { get; private set; } = "home";
        public string DataPath { get; private set; }
        PanelSettings panel;
        UnityEngine.TextCore.Text.FontAsset font;
        VisualElement root, safe, body, footer, overlay;
        ScrollView scroll;
        readonly List<Texture2D> photos = new List<Texture2D>();
        OpenedItem draft;
        bool draftNew;
        string selectedId, filter = "all", search = "", notificationError, photoCleanupError;
        int archivePage;
        Rect lastSafe;
        int lastWidth, lastHeight;
        DateTime displayedDate;
        float nextPoll;
        Action modalBack;
        static readonly Color Orange = new Color32(232, 92, 38, 255), Muted = new Color32(119, 116, 111, 255);

        void Start()
        {
            Application.targetFrameRate = 60;
#if UNITY_EDITOR
            if (File.Exists("BuildArtifacts/qa-mode.flag")) DataPathOverride = Path.GetFullPath("BuildArtifacts/QA/data/opened-note.json");
#endif
            DataPath = DataPathOverride ?? Path.Combine(OpenedPlatform.DataDirectory, "opened-note.json");
            Store = new OpenedStore(DataPath);
            panel = ScriptableObject.CreateInstance<PanelSettings>(); panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(390, 844); panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; panel.match = 0;
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("OpenedNote/OpenedTheme");
            var document = gameObject.AddComponent<UIDocument>(); document.panelSettings = panel;
            root = document.rootVisualElement; root.AddToClassList("app"); root.styleSheets.Add(Resources.Load<StyleSheet>("OpenedNote/Opened"));
            font = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(Resources.Load<Font>("OpenedNote/NotoSansKR"), 64, 8, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048, UnityEngine.TextCore.Text.AtlasPopulationMode.Dynamic, true);
            root.style.unityFontDefinition = FontDefinition.FromSDFFont(font);
            root.RegisterCallback<GeometryChangedEvent>(_ => SafeArea());
            displayedDate = Store.Today;
            CleanupPhotos(); Navigate("home"); SyncReminders(); ConsumeNotification();
            if (Store.Recovered) Toast("이전 저장본에서 복구했어요. 최근 기록을 확인해 주세요.");
        }
        void Update()
        {
            if (root == null) return;
            if (lastSafe != Screen.safeArea || lastWidth != Screen.width || lastHeight != Screen.height) SafeArea();
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
#endif
            if (Time.unscaledTime < nextPoll) return; nextPoll = Time.unscaledTime + .4f;
            var photo = OpenedPlatform.PhotoResult();
            if (photo.StartsWith("ok:"))
            {
                if (draft != null && CurrentPage == "edit") { draft.photoFile = photo.Substring(3); Editor(); Toast("사진을 추가했어요. 저장을 누르면 기록에 반영돼요."); }
                else CleanupPhotos();
            }
            else if (photo.StartsWith("error:")) Toast(photo.Substring(6), true);
            if (displayedDate != Store.Today)
            {
                displayedDate = Store.Today; SyncReminders();
                if (CurrentPage != "edit" && overlay == null) Navigate(CurrentPage);
            }
        }
        void OnApplicationFocus(bool focus)
        {
            if (!focus || Store == null || root == null) return;
            SyncReminders(); ConsumeNotification();
            if (CurrentPage == "home" || CurrentPage == "settings") Navigate(CurrentPage);
        }
        void OnDestroy()
        {
            ClearTextures(); if (font) Destroy(font); if (panel) Destroy(panel);
#if UNITY_EDITOR
            DataPathOverride = null;
#endif
        }
        void SafeArea()
        {
            if (safe == null || Screen.width == 0) return;
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafe = Screen.safeArea;
            int logicalWidth = Screen.width > Screen.height ? 780 : 390;
            panel.referenceResolution = new Vector2Int(logicalWidth, 844);
            float ratio = logicalWidth / (float)Screen.width;
            safe.style.paddingTop = Mathf.Max(8, (Screen.height - lastSafe.yMax) * ratio);
            safe.style.paddingBottom = Mathf.Max(6, lastSafe.yMin * ratio);
            safe.style.paddingLeft = Mathf.Max(0, lastSafe.xMin * ratio); safe.style.paddingRight = Mathf.Max(0, (Screen.width - lastSafe.xMax) * ratio);
            root.EnableInClassList("compact", Screen.height / (float)Screen.width < 1.9f);
            root.EnableInClassList("large", State.largeText);
        }
        public bool Save(Action action)
        {
            try { action(); SyncReminders(); return true; }
            catch (Exception e)
            {
                Toast(e is ArgumentException ? e.Message : "저장하지 못했어요. 저장 공간을 확인하고 다시 시도해 주세요.", true);
                Debug.LogWarning("OpenedNote save failed: " + e.GetType().Name); return false;
            }
        }
        void SyncReminders()
        {
            if (Store.ReadOnly) return;
            try { OpenedPlatform.Sync(); notificationError = null; }
            catch { notificationError = "기록은 저장됐지만 알림 예약을 확인하지 못했어요. 설정에서 다시 시도해 주세요."; }
        }
        void ConsumeNotification()
        {
            try { var id = OpenedPlatform.ConsumeItem(); if (!string.IsNullOrEmpty(id) && Store.Find(id) != null && CurrentPage != "edit") OpenItem(id); }
            catch { /* Opening the regular home remains available. */ }
        }
        static void Classes(VisualElement e, string classes) { foreach (var c in classes.Split(' ')) if (c.Length > 0) e.AddToClassList(c); }
        static VisualElement El(VisualElement parent, string classes, string name = null) { var e = new VisualElement { name = name }; Classes(e, classes); parent.Add(e); return e; }
        static Label Text(VisualElement parent, string value, string classes = "", string name = null) { var e = new Label(value) { name = name }; Classes(e, classes); parent.Add(e); return e; }
        static Button Btn(VisualElement parent, string label, Action action, string classes = "", string name = null) { var e = new Button(action) { text = label, name = name }; Classes(e, classes); parent.Add(e); return e; }
        static Button IconButton(VisualElement parent, string icon, string label, Action action, string name = null)
        { var b = Btn(parent, "", action, "icon-button", name); b.tooltip = label; b.Add(new OpenedIcon(icon, 23)); return b; }
        public void Navigate(string page)
        {
            CloseModal(); CurrentPage = page;
            if (page == "detail") Detail();
            else if (page == "edit" && draft != null) Editor();
            else if (page == "archive") Listing(true);
            else if (page == "settings") Settings();
            else if (page == "privacy") Privacy();
            else { CurrentPage = "home"; Listing(false); }
        }
        public void OpenItem(string id) { selectedId = id; Navigate("detail"); }
        public void Back()
        {
            if (overlay != null) { var back = modalBack; CloseModal(); back?.Invoke(); return; }
            if (CurrentPage == "edit") { Confirm("편집을 그만둘까요?", "저장하지 않은 변경 내용은 사라져요.", "편집 나가기", () => { draft = null; CleanupPhotos(); Navigate(draftNew ? "home" : "detail"); }); return; }
            if (CurrentPage == "privacy") Navigate("settings");
            else if (CurrentPage != "home") { filter = "all"; search = ""; Navigate("home"); }
            else Confirm("개봉노트를 닫을까요?", "저장한 기록은 다음에도 그대로 남아요.", "앱 닫기", Application.Quit);
        }
        void Shell(string title, bool home = false, bool navigation = true)
        {
            ClearTextures(); root.Clear(); overlay = null; modalBack = null;
            safe = El(root, "safe"); SafeArea();
            var header = El(safe, "header");
            if (home)
            {
                var brand = El(header, "brand"); brand.Add(new OpenedIcon("jar", 20) { Ink = Orange });
                Text(header, "OPENED NOTE", "eyebrow grow");
                if (State.items.Any(x => x.status == "active" && !string.IsNullOrEmpty(x.reminderLocalDate)) && (!State.notificationsEnabled || !OpenedPlatform.Allowed()))
                    Btn(header, "알림 꺼짐 · 설정", () => Navigate("settings"), "text-button", "notification-status");
                else IconButton(header, "plus", "새 제품 추가", AddItem, "add-top");
            }
            else { IconButton(header, "back", "뒤로", Back, "back"); Text(header, title, "header-title grow"); El(header, "header-spacer"); }
            scroll = new ScrollView(ScrollViewMode.Vertical) { name = "page-scroll", horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("page-scroll"); scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            safe.Add(scroll); body = scroll.contentContainer; body.AddToClassList("body");
            footer = El(safe, "footer");
            if (navigation)
            {
                var nav = El(safe, "nav"); Nav(nav, "home", "개봉노트"); Nav(nav, "archive", "보관함"); Nav(nav, "settings", "설정");
            }
        }
        void Nav(VisualElement parent, string page, string label)
        {
            var b = Btn(parent, "", () => { filter = "all"; search = ""; archivePage = 0; Navigate(page); }, "nav-item" + (CurrentPage == page ? " selected" : ""), "nav-" + page);
            b.Add(new OpenedIcon(page, 23) { Ink = CurrentPage == page ? Orange : Muted }); Text(b, label);
        }
        void Listing(bool archive)
        {
            Shell(archive ? "보관함" : "", !archive);
            if (!archive)
            {
                Text(body, "개봉노트", "title"); Text(body, "오늘도, 필요한 만큼만.", "subtitle");
            }
            else { Text(body, "잘 썼어요", "title"); Text(body, "다 쓴 제품의 개봉 기록을 모아 두었어요.", "subtitle"); }
            if (Store.ReadOnly) { Text(body, Store.LoadMessage, "notice danger"); Btn(body, "자료 관리 열기", () => Navigate("settings"), "secondary"); return; }
            if (notificationError != null) Text(body, notificationError, "notice danger");
            var filters = El(body, "filters row");
            foreach (var value in new[] { "all", "food", "care", "other" })
            {
                var category = value; int count = State.items.Count(x => (x.status == "finished") == archive && (value == "all" || x.category == value));
                Btn(filters, Category(value) + " " + count, () => { filter = category; archivePage = 0; Navigate(archive ? "archive" : "home"); }, "chip grow" + (filter == value ? " active" : ""), "filter-" + value);
            }
            var tools = El(body, "row list-tools");
            Text(tools, archive ? "보관한 제품 " + State.items.Count(x => x.status == "finished") + "개" : "나의 제품  " + State.items.Count(x => x.status == "active") + " / 15", "section-title grow");
            if (!archive) Btn(tools, SortName(State.sortMode) + "  ↓", SortDialog, "text-button", "sort");
            VisualElement cards = null;
            if (archive)
            {
                var field = new TextField { value = search, name = "archive-search" }; field.AddToClassList("search"); field.textEdition.placeholder = "이름이나 메모로 찾기"; body.Add(field);
                field.RegisterValueChangedCallback(e => { search = e.newValue; archivePage = 0; RenderCards(cards, true); });
            }
            cards = El(body, "cards", "cards"); RenderCards(cards, archive);
            if (!archive) Btn(footer, "＋  새 제품 기록하기", AddItem, "primary", "add-item");
            Text(body, "개봉일과 알림일은 직접 남기는 기록이에요.\n섭취·사용 안전 여부를 판단하지 않아요.", "footnote");
        }
        void RenderCards(VisualElement cards, bool archive)
        {
            // Release decoded photos before search or page changes, including repeated keystrokes.
            ClearTextures(); cards.Clear(); var items = Store.Sorted(archive, filter, search).ToList();
            if (items.Count == 0)
            {
                var empty = El(cards, "empty"); empty.Add(new OpenedIcon(archive ? "archive" : "jar", 66));
                Text(empty, archive ? (search.Length > 0 ? "찾는 제품이 없어요" : "보관함이 비어 있어요") : "처음 연 날을 기록해요", "section-title center");
                Text(empty, archive ? "다 쓴 제품에서 ‘다 썼어요’를 누르면\n이곳에 기록이 남아요." : "우유, 소스, 화장품까지\n개봉일을 기억하고 싶은 제품을 추가해 보세요.", "muted center"); return;
            }
            int pages = Math.Max(1, (items.Count + 19) / 20); archivePage = Mathf.Clamp(archivePage, 0, pages - 1);
            foreach (var item in archive ? items.Skip(archivePage * 20).Take(20) : items)
            {
                var value = item;
                bool due = !archive && Store.ReminderAt(item).HasValue && Store.ReminderAt(item).Value.Date <= Store.Today;
                var card = Btn(cards, "", () => OpenItem(value.id), "product-card" + (due ? " due-card" : ""), "item-" + item.id);
                ProductImage(card, item, false);
                var info = El(card, "product-copy grow"); Text(info, item.name, "product-title");
                Text(info, "개봉 후 " + Store.Elapsed(item) + "일 경과", "elapsed");
                var dates = El(info, "row card-dates");
                Text(dates, Store.DueLabel(item), "badge" + (due ? " due-badge" : ""));
                Text(dates, OpenedStore.ParseDate(item.openedLocalDate).ToString("yy. M. d.") + " 개봉", "tiny muted grow");
                card.Add(new OpenedIcon("next", 17) { Ink = Muted });
            }
            if (archive && pages > 1)
            {
                var paging = El(cards, "row gap");
                Btn(paging, "이전", () => { archivePage--; RenderCards(cards, true); scroll.scrollOffset = Vector2.zero; }, "secondary grow", "archive-prev").SetEnabled(archivePage > 0);
                Text(paging, (archivePage + 1) + " / " + pages, "muted center", "archive-page-label");
                Btn(paging, "다음", () => { archivePage++; RenderCards(cards, true); scroll.scrollOffset = Vector2.zero; }, "secondary grow", "archive-next").SetEnabled(archivePage + 1 < pages);
            }
        }
        static string Category(string value) => value == "food" ? "식품" : value == "care" ? "생활용품" : value == "other" ? "기타" : "전체";
        static string SortName(string value) => value == "recent" ? "최근 개봉순" : value == "near" ? "다가오는 순" : "알림 지난 순";
        static string ShortDate(string value) => OpenedStore.ParseDate(value).ToString("yyyy. M. d.");
        void SortDialog()
        {
            var modal = Modal("어떤 순서로 볼까요?");
            foreach (var mode in new[] { "due", "near", "recent" })
            { var sort = mode; Btn(modal, SortName(mode), () => { if (Save(() => Store.Change(s => s.sortMode = sort))) Navigate("home"); }, "secondary"); }
        }
        void ProductImage(VisualElement parent, OpenedItem item, bool hero)
        {
            var box = El(parent, "product-image " + (hero ? "hero-image" : "thumbnail") + " category-" + item.category);
            if (!string.IsNullOrEmpty(item.photoFile))
            {
                try
                {
                    var path = PhotoPath(item.photoFile);
                    if (File.Exists(path))
                    {
                        var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                        if (texture.LoadImage(File.ReadAllBytes(path), true))
                        { photos.Add(texture); var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit }; image.AddToClassList("photo"); box.Add(image); return; }
                        Destroy(texture);
                    }
                }
                catch { /* Text stays available when a private photo is missing. */ }
                Text(box, "사진을\n다시 선택", "tiny center"); return;
            }
            string icon = item.category == "care" ? "tube" : item.category == "other" ? "box" : item.name.Contains("우유") ? "milk" : "jar";
            box.Add(new OpenedIcon(icon, hero ? 122 : 58));
        }
        public void AddItem()
        {
            if (Store.ReadOnly) { Navigate("settings"); return; }
            if (State.items.Count(x => x.status == "active") >= OpenedStore.ActiveLimit) { Toast("사용 중인 제품은 15개까지예요. 다 쓴 제품을 보관한 뒤 추가해 주세요."); return; }
            draft = new OpenedItem { openedLocalDate = OpenedStore.Date(Store.Today), reminderTime = State.defaultReminderTime }; draftNew = true; Navigate("edit");
        }
        void Detail()
        {
            var item = Store.Find(selectedId); if (item == null) { Navigate("home"); return; }
            bool finished = item.status == "finished"; Shell("제품 기록", navigation: false);
            var hero = El(body, "detail-hero"); ProductImage(hero, item, true); Text(hero, Category(item.category), "eyebrow");
            Text(hero, item.name, "detail-title center"); Text(hero, "개봉 후 " + Store.Elapsed(item) + "일 경과", "detail-elapsed");
            var dates = El(body, "date-card row");
            var opened = El(dates, "date-column grow"); Text(opened, "개봉한 날", "muted"); Text(opened, ShortDate(item.openedLocalDate), "date-value");
            var reminder = El(dates, "date-column grow"); Text(reminder, "직접 정한 알림일", "muted"); Text(reminder, string.IsNullOrEmpty(item.reminderLocalDate) ? "설정하지 않음" : ShortDate(item.reminderLocalDate), "date-value");
            if (!string.IsNullOrEmpty(item.reminderLocalDate)) Text(reminder, item.reminderTime, "teal");
            Text(body, finished ? "다 쓴 제품이에요. 예약한 알림은 취소했어요." : Store.DueLabel(item), "notice teal center");
            if (notificationError != null) Text(body, notificationError, "notice danger");
            var memo = El(body, "memo-card"); Text(memo, "나의 메모", "section-title"); Text(memo, string.IsNullOrEmpty(item.note) ? "기억하고 싶은 내용을 남겨 보세요." : item.note, "muted");
            if (finished && DateTime.TryParse(item.finishedAt, out var date)) Text(body, date.ToLocalTime().ToString("yyyy. M. d.") + "에 다 썼어요", "footnote");
            Text(body, "알림은 제품을 확인하기 위한 개인 기록이에요.\n제품 표시사항과 보관 방법을 함께 확인해 주세요.", "footnote");
            var actions = El(footer, "row gap");
            Btn(actions, finished ? "사용 중으로 복구" : "✓  다 썼어요", () =>
            {
                if (Save(() => Store.Finish(item.id, !finished))) { Navigate("detail"); Toast(finished ? "사용 중으로 복구했어요. 지난 알림은 다시 울리지 않아요." : "보관함에 담았어요. 알림도 취소했어요."); }
            }, "primary grow", "finish-item");
            Btn(actions, "기록 수정", () => { draft = OpenedStore.Clone(item); draftNew = false; Navigate("edit"); }, "secondary grow", "edit-item");
            Btn(body, "제품 완전 삭제", () => Confirm("이 제품을 삭제할까요?", "‘" + item.name + "’의 개봉 기록과 사진이 삭제돼요. 되돌릴 수 없어요.", "완전 삭제", () =>
            {
                if (!Save(() => Store.Delete(item.id))) return;
                CleanupPhotos(); Navigate(finished ? "archive" : "home"); Toast(Store.CleanupWarning ?? photoCleanupError ?? "제품 기록을 삭제했어요.", Store.CleanupWarning != null || photoCleanupError != null);
            }, true), "danger-text", "delete-item");
        }
        TextField Field(string title, string value, Action<string> changed, string name, bool multiline = false)
        {
            Text(body, title, "field-label");
            var field = new TextField { value = value ?? "", name = name, multiline = multiline }; field.AddToClassList(multiline ? "input memo-input" : "input");
            field.RegisterValueChangedCallback(e => changed(e.newValue)); body.Add(field);
            field.RegisterCallback<FocusInEvent>(_ => field.schedule.Execute(() => scroll.ScrollTo(field)).ExecuteLater(250)); return field;
        }
        void Editor()
        {
            Shell(draftNew ? "새 제품 기록" : "제품 기록 수정", navigation: false);
            Text(body, "사진 (선택)", "field-label");
            var photo = El(body, "photo-editor row gap"); ProductImage(photo, draft, false);
            var controls = El(photo, "grow");
            Btn(controls, "사진 선택", () => { try { OpenedPlatform.PickPhoto(); } catch (Exception e) { Toast(e.Message, true); } }, "secondary", "pick-photo");
            if (!string.IsNullOrEmpty(draft.photoFile)) Btn(controls, "사진 빼기", () => { draft.photoFile = ""; Editor(); }, "text-button", "remove-photo");
            Text(body, "선택한 한 장만 이 기기에 복사해요.", "tiny muted");
            Field("제품 이름", draft.name, value => draft.name = value, "item-name"); Text(body, "1~40자 · 같은 이름도 별도로 기록할 수 있어요.", "tiny muted");
            Text(body, "분류", "field-label"); var categories = El(body, "row gap");
            foreach (var name in new[] { "food", "care", "other" })
            { var category = name; Btn(categories, Category(name), () => { draft.category = category; Editor(); }, "chip grow" + (draft.category == name ? " active" : ""), "category-" + name); }
            Text(body, "개봉한 날", "field-label");
            Btn(body, ShortDate(draft.openedLocalDate) + "     ▾", () => DatePicker("개봉한 날", draft.openedLocalDate, Store.Today, value => { draft.openedLocalDate = value; Editor(); }), "date-input", "opened-date");
            var enabled = new Toggle("직접 정한 날짜에 한 번 알림") { value = !string.IsNullOrEmpty(draft.reminderLocalDate), name = "reminder-enabled" }; enabled.AddToClassList("setting-toggle"); body.Add(enabled);
            enabled.RegisterValueChangedCallback(e => { draft.reminderLocalDate = e.newValue ? OpenedStore.Date(Store.Today.AddDays(1)) : ""; Editor(); });
            if (!string.IsNullOrEmpty(draft.reminderLocalDate))
            {
                Btn(body, ShortDate(draft.reminderLocalDate) + "     ▾", () => DatePicker("직접 정한 알림일", draft.reminderLocalDate, null, value => { draft.reminderLocalDate = value; Editor(); }), "date-input", "reminder-date");
                Field("알림 시각", draft.reminderTime, value => draft.reminderTime = value, "reminder-time");
                var times = El(body, "row gap"); foreach (var time in new[] { "09:00", "12:00", "18:00" }) { var value = time; Btn(times, time, () => { draft.reminderTime = value; Editor(); }, "chip grow"); }
                Text(body, "기기 절전 설정에 따라 알림이 늦어질 수 있어요.\n지난 시각은 기록만 남기고 새 알림을 보내지 않아요.", "tiny muted");
            }
            Field("메모 (선택)", draft.note, value => draft.note = value, "item-note", true); Text(body, "제품 구분이나 보관 방법 등 · 최대 200자", "tiny muted");
            Text(body, "알림일은 직접 정하는 확인 날짜예요.\n섭취·사용 가능 여부를 판정하지 않아요.", "footnote");
            Btn(footer, "기록 저장", SaveDraft, "primary", "save-item");
        }
        void SaveDraft()
        {
            bool wantsReminder = !string.IsNullOrEmpty(draft.reminderLocalDate);
            if (!string.IsNullOrEmpty(draft.photoFile) && !File.Exists(PhotoPath(draft.photoFile)))
            { Toast("사진 파일을 찾지 못했어요. 사진을 다시 선택하거나 빼고 저장해 주세요.", true); return; }
            if (!Save(() => Store.SaveItem(draft, draftNew))) return;
            selectedId = draft.id; draft = null; CleanupPhotos(); Navigate("detail"); Toast("개봉 기록을 저장했어요.");
            if (wantsReminder && !State.notificationsEnabled) NotificationPrompt();
        }
        void DatePicker(string title, string value, DateTime? maximum, Action<string> changed)
        {
            DateTime selected = OpenedStore.ParseDate(value), month = new DateTime(selected.Year, selected.Month, 1);
            Action render = null;
            render = () =>
            {
                var modal = Modal(title); modal.AddToClassList("calendar-modal");
                var header = El(modal, "row gap");
                var prev = IconButton(header, "back", "이전 달", () => { month = month.AddMonths(-1); render(); }, "month-prev"); prev.SetEnabled(month.Year > 1900 || month.Month > 1);
                Text(header, month.ToString("yyyy년 M월"), "section-title grow center");
                var next = IconButton(header, "next", "다음 달", () => { month = month.AddMonths(1); render(); }, "month-next");
                next.SetEnabled(month.Year < 9999 && (!maximum.HasValue || month.AddMonths(1) <= maximum.Value));
                var week = El(modal, "row calendar-week"); foreach (var day in new[] { "일", "월", "화", "수", "목", "금", "토" }) Text(week, day, "calendar-weekday");
                int offset = (int)month.DayOfWeek, count = DateTime.DaysInMonth(month.Year, month.Month);
                for (int row = 0; row < (offset + count + 6) / 7; row++)
                {
                    var line = El(modal, "row calendar-row");
                    for (int col = 0; col < 7; col++)
                    {
                        int day = row * 7 + col - offset + 1;
                        if (day < 1 || day > count) { El(line, "calendar-blank"); continue; }
                        DateTime date = month.AddDays(day - 1);
                        var button = Btn(line, day.ToString(), () => { CloseModal(); changed(OpenedStore.Date(date)); }, "calendar-day" + (date == selected ? " chosen" : ""), "day-" + day);
                        button.SetEnabled(!maximum.HasValue || date <= maximum.Value);
                    }
                }
                Btn(modal, "오늘 선택", () => { CloseModal(); changed(OpenedStore.Date(Store.Today)); }, "secondary");
                Text(modal, "날짜 직접 입력", "field-label");
                var exact = new TextField { value = OpenedStore.Date(selected), name = "date-exact" }; exact.AddToClassList("input"); modal.Add(exact);
                Btn(modal, "이 날짜로 선택", () =>
                {
                    try { var date = OpenedStore.ParseDate(exact.value.Trim()); if (maximum.HasValue && date > maximum.Value) throw new ArgumentException("미래 날짜는 개봉일로 선택할 수 없어요."); CloseModal(); changed(OpenedStore.Date(date)); }
                    catch (ArgumentException e) { Toast(e.Message, true); }
                }, "primary", "date-apply");
            };
            render();
        }
        void Settings()
        {
            Shell("설정"); Text(body, "내 기록, 내 방식으로", "section-title");
            Text(body, "이 기기에만 저장돼요", "settings-title"); Text(body, "계정이나 서버 없이 제품 이름, 날짜, 사진을 보관해요. 앱 삭제·기기 변경 시 기록을 복구할 수 없어요.", "muted");
            var large = new Toggle("큰 글자") { value = State.largeText, name = "large-text" }; large.AddToClassList("setting-toggle"); body.Add(large);
            large.RegisterValueChangedCallback(e => { if (Save(() => Store.Change(s => s.largeText = e.newValue))) SafeArea(); });
            Text(body, "날짜 알림", "settings-title");
            Text(body, State.notificationsEnabled && OpenedPlatform.Allowed() ? "알림 켜짐 · 정한 날짜에 한 번 알려드려요." : "알림 꺼짐 · 날짜 기록은 계속 사용할 수 있어요.", "notice");
            if (notificationError != null) Text(body, notificationError, "notice danger");
            if (!State.notificationsEnabled) Btn(body, "알림 사용하기", NotificationPrompt, "secondary", "enable-notifications");
            else Btn(body, "앱 알림 끄기", () => { if (Save(() => Store.Change(s => s.notificationsEnabled = false))) Navigate("settings"); }, "secondary", "disable-notifications");
            Btn(body, "Android 알림 설정 열기", () => { OpenedPlatform.Settings();
#if UNITY_EDITOR
                Toast("Android 앱에서 시스템 알림 설정을 열 수 있어요.");
#endif
            }, "text-button", "notification-settings");
            Btn(body, "알림 예약 다시 확인", () => { SyncReminders(); Navigate("settings"); Toast(notificationError ?? "예약을 다시 확인했어요. 지난 알림은 보내지 않아요.", notificationError != null); }, "text-button");
            Text(body, "알림은 지정 시각 이후에 전달되며 절전 상태에 따라 늦어질 수 있어요. Android 설정에서 앱을 강제 종료한 경우 다시 열어야 알림이 예약돼요.", "tiny muted");
            var defaultTime = Field("새 제품의 기본 알림 시각", State.defaultReminderTime, _ => { }, "default-time");
            Btn(body, "기본 시각 저장", () => { if (Save(() => Store.Change(s => s.defaultReminderTime = defaultTime.value.Trim()))) Toast("앞으로 추가할 제품에 적용해요."); }, "secondary");
            Text(body, "앱 안내", "settings-title");
            Btn(body, "개인정보처리방침", () => Navigate("privacy"), "setting-link", "privacy");
            var release = Resources.Load<TextAsset>("OpenedNote/Contact"); var contact = release == null ? "" : release.text.Trim();
            if (!string.IsNullOrEmpty(contact)) Btn(body, "문의하기 · " + contact, () => Application.OpenURL("mailto:" + contact + "?subject=" + Uri.EscapeDataString("개봉노트 문의")), "setting-link");
            else Text(body, "문의: Google Play의 개발자 연락처를 이용해 주세요.", "muted");
            Text(body, "개봉노트 1.0.0  ·  SecondWindGames\n무료 사용 중 15개 · 광고 및 인앱 구매 없음", "footnote");
            if (Store.ReadOnly) { Text(body, Store.LoadMessage, "notice danger"); Btn(body, "저장 자료 다시 읽기", () => { Store = new OpenedStore(DataPath); Navigate("settings"); }, "secondary"); }
            Btn(body, "모든 기록과 사진 삭제", () => Confirm("모든 기록을 삭제할까요?", "사용 중인 제품, 보관함, 사진, 알림과 설정을 모두 삭제해요. 되돌릴 수 없어요.", "모두 삭제", () =>
            {
                if (!Save(Store.Reset)) return;
                try { OpenedPlatform.Clear(); } catch { notificationError = "알림 정리를 마치지 못했어요. 앱을 다시 열어 확인해 주세요."; }
                draft = null; CleanupPhotos(); filter = "all"; search = ""; archivePage = 0; Navigate("home"); Toast(Store.CleanupWarning ?? photoCleanupError ?? notificationError ?? "모든 기록을 삭제했어요.", Store.CleanupWarning != null || photoCleanupError != null || notificationError != null);
            }, true), "danger-text", "reset-all");
        }
        void NotificationPrompt()
        {
            var modal = Modal("정한 날짜에 알려드릴까요?");
            Text(modal, "직접 입력한 날짜에 제품 이름과 함께 한 번 알려드려요. 허용하지 않아도 개봉 기록은 그대로 사용할 수 있어요.", "muted");
            Btn(modal, "알림 허용하기", () =>
            {
                if (!Save(() => Store.Change(s => s.notificationsEnabled = true))) return;
                CloseModal(); OpenedPlatform.RequestNotifications(() =>
                {
                    SyncReminders(); if (CurrentPage == "settings") Navigate("settings");
                    Toast(OpenedPlatform.Allowed() ? "알림을 사용할 수 있어요." : "알림이 꺼져 있어요. 설정에서 허용할 수 있어요.");
                });
            }, "primary", "allow-notifications");
            Btn(modal, "나중에", CloseModal, "secondary", "later-notifications");
        }
        void Privacy()
        {
            Shell("개인정보처리방침", navigation: false); Text(body, "기록은 이 기기에만", "title");
            Text(body, "시행일: 2026년 9월 11일\n운영: SecondWindGames · 개봉노트", "muted");
            foreach (var pair in new[] {
                new[] { "수집 및 공유", "개봉노트는 서버로 개인정보를 수집하거나 제3자에게 공유하지 않습니다. 광고, 분석, 로그인, 결제 SDK를 사용하지 않습니다." },
                new[] { "기기에 보관하는 정보", "제품 이름, 개봉일, 사용자가 정한 알림일과 시각, 메모, 분류, 보관 상태와 선택 사진을 앱 내부 저장소에서만 처리합니다." },
                new[] { "사진과 알림 권한", "사진 선택기를 통해 고른 한 장만 앱에 복사합니다. 전체 사진 라이브러리와 카메라 권한을 요구하지 않습니다. 복사한 사진은 크기를 줄이고 위치 등 원본 메타데이터를 제거합니다. 알림은 허용한 경우에만 사용합니다." },
                new[] { "보관 및 삭제", "개별 제품의 완전 삭제 또는 설정의 모든 기록과 사진 삭제로 지울 수 있습니다. 앱을 제거하면 앱 내부 자료가 삭제됩니다. 자동 클라우드 백업과 기기 간 동기화는 제공하지 않습니다." },
                new[] { "알림 화면", "알림을 켜면 제품 이름이 Android 알림에 표시될 수 있습니다. 잠금 화면의 표시 방식은 기기의 알림 설정에서 변경할 수 있습니다." },
                new[] { "문의 및 변경 안내", "Google Play 스토어 등록정보의 개발자 연락처로 문의해 주세요. 처리 방식이 변경되면 앱과 공개 개인정보처리방침의 시행일 및 내용을 갱신합니다." }
            }) { Text(body, pair[0], "settings-title"); Text(body, pair[1], "muted"); }
        }
        VisualElement Modal(string title)
        {
            CloseModal(); overlay = El(root, "overlay", "modal");
            var frame = El(overlay, "modal-frame");
            var content = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Auto }; content.AddToClassList("modal-scroll"); frame.Add(content);
            var modal = content.contentContainer; modal.AddToClassList("modal-content");
            var header = El(modal, "row"); Text(header, title, "modal-title grow"); IconButton(header, "close", "닫기", () => { var action = modalBack; CloseModal(); action?.Invoke(); }, "close-modal");
            return modal;
        }
        void Confirm(string title, string message, string label, Action confirmed, bool danger = false)
        {
            var modal = Modal(title); Text(modal, message, "muted");
            Btn(modal, label, () => { CloseModal(); confirmed(); }, danger ? "primary destructive" : "primary", "confirm-action");
            Btn(modal, "취소", CloseModal, "secondary", "cancel-action");
        }
        public void CloseModal() { if (overlay != null) overlay.RemoveFromHierarchy(); overlay = null; modalBack = null; }
        void Toast(string message, bool error = false)
        {
            root.Q<Label>("toast")?.RemoveFromHierarchy();
            var toast = Text(root, message, "toast" + (error ? " error-toast" : ""), "toast");
            toast.pickingMode = PickingMode.Ignore; toast.schedule.Execute(() => toast.RemoveFromHierarchy()).ExecuteLater(error ? 6500 : 4200);
        }
        string PhotoPath(string filename) => Path.Combine(Path.GetDirectoryName(DataPath), "photos", filename);
        void ClearTextures() { foreach (var image in photos) if (image) Destroy(image); photos.Clear(); }
        public void CleanupPhotos()
        {
            if (Store.ReadOnly) return;
            photoCleanupError = null;
            try
            {
                var keep = new HashSet<string>(State.items.Select(x => x.photoFile ?? ""));
                if (draft != null) keep.Add(draft.photoFile ?? "");
                if (File.Exists(DataPath + ".bak"))
                {
                    var backup = JsonUtility.FromJson<OpenedState>(File.ReadAllText(DataPath + ".bak"));
                    if (backup?.items != null) foreach (var item in backup.items) if (item != null) keep.Add(item.photoFile ?? "");
                }
                var folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(DataPath), "photos"));
                if (!Directory.Exists(folder)) return;
                foreach (var file in Directory.GetFiles(folder))
                    if (!keep.Contains(Path.GetFileName(file)) && Path.GetDirectoryName(Path.GetFullPath(file)) == folder) File.Delete(file);
            }
            catch { photoCleanupError = "일부 사진 파일을 정리하지 못했어요. 저장 공간을 확인하고 삭제를 다시 실행해 주세요."; }
        }
    }
}
