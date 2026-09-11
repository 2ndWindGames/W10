using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace OpenedNote
{
    [Serializable] public sealed class OpenedItem
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "";
        public string category = "food";
        public string openedLocalDate = "";
        public string reminderLocalDate = "";
        public string reminderTime = "09:00";
        public string photoFile = "";
        public string note = "";
        public string status = "active";
        public string finishedAt = "";
        public string createdAt = "";
        public string updatedAt = "";
        // A reminder version prevents a stale OS request from delivering an edited reminder.
        public int reminderVersion = 1;
    }
    [Serializable] public sealed class OpenedState
    {
        public int schemaVersion = 1;
        public bool initialized = true;
        public bool largeText;
        public bool notificationsEnabled;
        public string defaultReminderTime = "09:00";
        public string sortMode = "due";
        public List<OpenedItem> items = new List<OpenedItem>();
    }
    public interface IOpenedDisk
    {
        bool Exists(string suffix);
        string Read(string suffix);
        void Write(string json);
        void RemoveRecoveryCopies();
    }
    public sealed class OpenedDisk : IOpenedDisk
    {
        readonly string path;
        public OpenedDisk(string path) { this.path = path; }
        public bool Exists(string suffix) => File.Exists(path + suffix);
        public string Read(string suffix) => File.ReadAllText(path + suffix, Encoding.UTF8);
        public void Write(string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var bytes = new UTF8Encoding(false).GetBytes(json);
            using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
            else File.Move(path + ".tmp", path);
        }
        public void RemoveRecoveryCopies()
        {
            foreach (var suffix in new[] { ".bak", ".tmp" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }
    public sealed class OpenedStore
    {
        public const int ActiveLimit = 15;
        public OpenedState State { get; private set; } = new OpenedState();
        public bool ReadOnly { get; private set; }
        public bool Recovered { get; private set; }
        public string LoadMessage { get; private set; }
        public string CleanupWarning { get; private set; }
        readonly IOpenedDisk disk;
        readonly Func<DateTime> now;
        public DateTime Now => now();
        public DateTime Today => Now.Date;
        public OpenedStore(string path, Func<DateTime> clock = null) : this(new OpenedDisk(path), clock) { }
        public OpenedStore(IOpenedDisk storage, Func<DateTime> clock = null)
        {
            disk = storage; now = clock ?? (() => DateTime.Now);
            try
            {
                if (!disk.Exists(""))
                {
                    if (disk.Exists(".bak")) { State = Decode(disk.Read(".bak")); Recovered = true; }
                    else if (disk.Exists(".tmp")) throw new InvalidDataException("Interrupted first save");
                    else disk.Write(JsonUtility.ToJson(State, true));
                    return;
                }
                string json = disk.Read("");
                OpenedState envelope = null;
                try { envelope = JsonUtility.FromJson<OpenedState>(json); } catch { /* Decode below can recover a corrupt primary. */ }
                if (envelope != null && envelope.schemaVersion > 1)
                { ReadOnly = true; LoadMessage = "새 버전에서 만든 기록이에요. 앱을 업데이트해 주세요."; return; }
                try { State = Decode(json); }
                catch { if (!disk.Exists(".bak")) throw; State = Decode(disk.Read(".bak")); Recovered = true; }
            }
            catch (Exception)
            { ReadOnly = true; LoadMessage = "기록을 읽지 못해 원본을 보호하고 있어요. 설정에서 다시 읽거나 자료를 삭제할 수 있어요."; }
        }
        public static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static OpenedState Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{") || !json.Contains("\"initialized\"") || !json.Contains("\"items\"")) throw new InvalidDataException("Incomplete data");
            var value = JsonUtility.FromJson<OpenedState>(json);
            Validate(value); return value;
        }
        public void Change(Action<OpenedState> change)
        {
            if (ReadOnly) throw new IOException("기록 보호 중에는 저장할 수 없어요.");
            var next = Clone(State); change(next); Validate(next);
            if (Recovered) { disk.Write(JsonUtility.ToJson(State, true)); Recovered = false; }
            disk.Write(JsonUtility.ToJson(next, true)); State = next;
        }
        public static string Date(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        public static DateTime ParseDate(string date)
        {
            if (!DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) throw new ArgumentException("날짜를 yyyy-MM-dd 형식으로 입력해 주세요.");
            return value.Date;
        }
        public static TimeSpan ParseTime(string time)
        {
            if (!DateTime.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) throw new ArgumentException("알림 시각을 00:00~23:59로 입력해 주세요.");
            return value.TimeOfDay;
        }
        public static int TextLength(string value) => new StringInfo(value ?? "").LengthInTextElements;
        static void Require(bool value, string message) { if (!value) throw new ArgumentException(message); }
        public static void Validate(OpenedState state)
        {
            Require(state != null && state.schemaVersion == 1 && state.initialized, "지원하지 않는 저장 형식이에요.");
            Require(state.items != null, "기록 목록이 손상되었어요.");
            ParseTime(state.defaultReminderTime);
            Require(new[] { "due", "near", "recent" }.Contains(state.sortMode), "정렬 설정을 확인해 주세요.");
            Require(state.items.All(x => x != null && Guid.TryParseExact(x.id, "N", out _)) && state.items.Select(x => x.id).Distinct().Count() == state.items.Count, "기록 ID를 확인해 주세요.");
            Require(state.items.Count(x => x.status == "active") <= ActiveLimit, "사용 중인 제품은 최대 15개예요. 다 쓴 제품을 보관한 뒤 추가해 주세요.");
            foreach (var item in state.items)
            {
                Require(!string.IsNullOrWhiteSpace(item.name) && TextLength(item.name) <= 40, "제품 이름은 1~40자로 입력해 주세요.");
                Require(TextLength(item.note) <= 200, "메모는 200자까지 입력할 수 있어요.");
                Require(new[] { "food", "care", "other" }.Contains(item.category), "분류를 선택해 주세요.");
                Require(item.status == "active" || item.status == "finished", "보관 상태를 확인해 주세요.");
                Require(item.reminderVersion > 0, "알림 정보를 확인해 주세요.");
                ParseDate(item.openedLocalDate); ParseTime(item.reminderTime);
                if (!string.IsNullOrEmpty(item.reminderLocalDate)) Require(ParseDate(item.reminderLocalDate) >= ParseDate(item.openedLocalDate), "알림일은 개봉일과 같거나 이후여야 해요.");
                Require(string.IsNullOrEmpty(item.photoFile) || (item.photoFile.Length == 36 && item.photoFile.EndsWith(".jpg", StringComparison.Ordinal) && Guid.TryParseExact(item.photoFile.Substring(0, 32), "N", out _)), "사진 경로가 올바르지 않아요.");
            }
        }
        public OpenedItem Find(string id) => State.items.FirstOrDefault(x => x.id == id);
        public void SaveItem(OpenedItem item, bool isNew)
        {
            var copy = Clone(item); copy.name = (copy.name ?? "").Trim(); copy.note = (copy.note ?? "").Trim();
            Require(ParseDate(copy.openedLocalDate) <= Today, "개봉일은 오늘 또는 과거 날짜로 선택해 주세요.");
            copy.updatedAt = Now.ToUniversalTime().ToString("o");
            Change(s =>
            {
                var old = s.items.FirstOrDefault(x => x.id == copy.id);
                if (isNew)
                {
                    Require(old == null, "이미 저장한 제품이에요."); copy.createdAt = copy.updatedAt;
                    copy.status = "active"; copy.finishedAt = ""; s.items.Add(copy);
                }
                else
                {
                    Require(old != null, "제품을 찾을 수 없어요.");
                    if (copy.reminderLocalDate != old.reminderLocalDate || copy.reminderTime != old.reminderTime) copy.reminderVersion = checked(old.reminderVersion + 1);
                    copy.status = old.status; copy.finishedAt = old.finishedAt; copy.createdAt = old.createdAt;
                    s.items[s.items.IndexOf(old)] = copy;
                }
            });
        }
        public void Finish(string id, bool finished)
        {
            Change(s =>
            {
                var item = s.items.FirstOrDefault(x => x.id == id); Require(item != null, "제품을 찾을 수 없어요.");
                item.status = finished ? "finished" : "active";
                item.finishedAt = finished ? Now.ToUniversalTime().ToString("o") : "";
                item.updatedAt = Now.ToUniversalTime().ToString("o");
                // Retain reminder identity. A previously delivered reminder cannot replay after restore.
            });
        }
        public void Delete(string id)
        {
            Change(s => { Require(s.items.Any(x => x.id == id), "제품을 찾을 수 없어요."); s.items.RemoveAll(x => x.id == id); });
            PurgeRecovery();
        }
        void PurgeRecovery()
        {
            CleanupWarning = null;
            try { disk.RemoveRecoveryCopies(); }
            catch { CleanupWarning = "기록은 삭제했지만 복구 파일 정리가 남았어요. 저장 공간을 확인하고 전체 삭제를 다시 실행해 주세요."; }
        }
        public void Reset()
        {
            // Commit an empty tombstone first: an interrupted cleanup never revives deleted records.
            var empty = new OpenedState(); disk.Write(JsonUtility.ToJson(empty, true));
            State = empty; ReadOnly = false; Recovered = false; LoadMessage = null; PurgeRecovery();
        }
        public int Elapsed(OpenedItem item) => Math.Max(0, (Today - ParseDate(item.openedLocalDate)).Days);
        public DateTime? ReminderAt(OpenedItem item) => string.IsNullOrEmpty(item.reminderLocalDate) ? (DateTime?)null : ParseDate(item.reminderLocalDate).Add(ParseTime(item.reminderTime));
        public string DueLabel(OpenedItem item)
        {
            if (item.status == "finished") return "다 쓴 제품";
            var at = ReminderAt(item); if (!at.HasValue) return "알림 없이 기록 중";
            int days = (at.Value.Date - Today).Days;
            return days < 0 ? "알림일 " + -days + "일 지남" : days == 0 ? "오늘 확인" : days == 1 ? "내일 알림" : "알림까지 " + days + "일";
        }
        public IEnumerable<OpenedItem> Sorted(bool archive, string category = "all", string search = "")
        {
            var list = State.items.Where(x => (x.status == "finished") == archive && (category == "all" || x.category == category) && (string.IsNullOrWhiteSpace(search) || (x.name + " " + x.note).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0));
            if (archive) return list.OrderByDescending(x => x.finishedAt).ThenBy(x => x.name);
            if (State.sortMode == "recent") return list.OrderByDescending(x => x.openedLocalDate).ThenByDescending(x => x.createdAt);
            if (State.sortMode == "near") return list.OrderBy(x => !ReminderAt(x).HasValue ? 2 : ReminderAt(x).Value.Date < Today ? 1 : 0).ThenBy(x => x.reminderLocalDate).ThenBy(x => x.name);
            return list.OrderBy(x => ReminderAt(x) ?? DateTime.MaxValue).ThenBy(x => x.name);
        }
    }
}
