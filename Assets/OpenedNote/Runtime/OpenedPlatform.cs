using System;
using System.IO;
using UnityEngine;

namespace OpenedNote
{
    public static class OpenedPlatform
    {
        public static string DataDirectory
        {
            get {
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var activity = Activity()) using (var files = activity.Call<AndroidJavaObject>("getFilesDir"))
                    return Path.Combine(files.Call<string>("getAbsolutePath"), "openednote");
#else
                return Path.Combine(Application.persistentDataPath, "openednote");
#endif
            }
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject Activity() { using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) return player.GetStatic<AndroidJavaObject>("currentActivity"); }
        static void Call(string method) { using (var a = Activity()) using (var bridge = new AndroidJavaClass("com.secondwindgames.openednote.Reminders")) bridge.CallStatic(method, a); }
#endif
        public static bool Allowed()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var a = Activity()) using (var bridge = new AndroidJavaClass("com.secondwindgames.openednote.Reminders")) return bridge.CallStatic<bool>("allowed", a); } catch { return false; }
#else
            return false;
#endif
        }
        public static void Sync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Call("sync");
#endif
        }
        public static void Clear()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Call("clear");
#endif
        }
        public static void Settings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Call("settings");
#endif
        }
        public static void RequestNotifications(Action completed)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                if (version.GetStatic<int>("SDK_INT") >= 33 && !UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
                {
                    var callbacks = new UnityEngine.Android.PermissionCallbacks();
                    callbacks.PermissionGranted += _ => completed(); callbacks.PermissionDenied += _ => completed();
                    UnityEngine.Android.Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS", callbacks); return;
                }
            }
#endif
            completed();
        }
        public static void PickPhoto()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var a = Activity()) using (var bridge = new AndroidJavaClass("com.secondwindgames.openednote.PhotoActivity")) bridge.CallStatic("pick", a);
#else
            throw new InvalidOperationException("사진 선택은 Android 앱에서 사용할 수 있어요.");
#endif
        }
        public static string PhotoResult()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var bridge = new AndroidJavaClass("com.secondwindgames.openednote.PhotoActivity")) return bridge.CallStatic<string>("takeResult");
#else
            return "";
#endif
        }
        public static string ConsumeItem()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var a = Activity()) using (var bridge = new AndroidJavaClass("com.secondwindgames.openednote.Reminders")) return bridge.CallStatic<string>("consumeItem", a);
#else
            return "";
#endif
        }
    }
}
