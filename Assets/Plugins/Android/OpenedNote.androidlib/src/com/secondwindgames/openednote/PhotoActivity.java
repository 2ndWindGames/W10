package com.secondwindgames.openednote;

import android.app.Activity;
import android.content.*;
import android.graphics.*;
import android.media.ExifInterface;
import android.net.Uri;
import android.os.*;
import java.io.*;
import java.util.UUID;

/** System single-photo picker; no camera, storage, or media-library permission. */
public final class PhotoActivity extends Activity {
    static String result = "";
    static synchronized void publish(String value) { result = value; }
    public static void pick(Activity activity) { publish(""); activity.startActivity(new Intent(activity, PhotoActivity.class)); }
    public static synchronized String takeResult() { String value = result; result = ""; return value; }
    public static void cleanup(Context c) {
        publish("");
        File[] files = c.getCacheDir().listFiles((dir, name) -> name.startsWith("import-") && name.endsWith(".tmp"));
        if (files != null) for (File file : files) if (!file.delete() && file.exists()) throw new IllegalStateException("Photo cache cleanup failed");
    }
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        android.widget.TextView notice = new android.widget.TextView(this);
        notice.setText("선택한 사진 한 장만 개봉노트에 저장해요.\n\n사진을 준비하고 있어요…");
        notice.setTextSize(18); notice.setGravity(android.view.Gravity.CENTER); setContentView(notice);
        if (state != null) return;
        Intent pick = new Intent(Build.VERSION.SDK_INT >= 33 ? "android.provider.action.PICK_IMAGES" : Intent.ACTION_OPEN_DOCUMENT);
        pick.setType("image/*");
        if (Build.VERSION.SDK_INT < 33) pick.addCategory(Intent.CATEGORY_OPENABLE);
        try { startActivityForResult(pick, 100); }
        catch (ActivityNotFoundException e) {
            try { startActivityForResult(new Intent(Intent.ACTION_OPEN_DOCUMENT).setType("image/*").addCategory(Intent.CATEGORY_OPENABLE), 100); }
            catch (Exception error) { publish("error:사진 선택기를 열지 못했어요. 사진 없이도 저장할 수 있어요."); finish(); }
        }
    }
    @Override protected void onActivityResult(int request, int code, Intent data) {
        super.onActivityResult(request, code, data);
        if (request != 100) return;
        if (code != RESULT_OK || data == null || data.getData() == null) { publish("cancel"); finish(); return; }
        Uri uri = data.getData();
        new Thread(() -> {
            File raw = null, out = null; Bitmap bitmap = null;
            try {
                File photos = new File(getFilesDir(), "openednote/photos");
                if (!photos.isDirectory() && !photos.mkdirs()) throw new IOException("No storage");
                raw = File.createTempFile("import-", ".tmp", getCacheDir());
                try (InputStream input = getContentResolver().openInputStream(uri); FileOutputStream stream = new FileOutputStream(raw)) {
                    if (input == null) throw new IOException("Missing image");
                    byte[] buffer = new byte[16384]; long size = 0; int n;
                    while ((n = input.read(buffer)) != -1) { size += n; if (size > 25L*1024*1024) throw new IOException("Image too large"); stream.write(buffer, 0, n); }
                }
                BitmapFactory.Options bounds = new BitmapFactory.Options(); bounds.inJustDecodeBounds = true;
                BitmapFactory.decodeFile(raw.getPath(), bounds);
                if (bounds.outWidth <= 0 || bounds.outHeight <= 0 || (long)bounds.outWidth * bounds.outHeight > 100000000L) throw new IOException("Unsupported image");
                if (Build.VERSION.SDK_INT >= 28) {
                    bitmap = ImageDecoder.decodeBitmap(ImageDecoder.createSource(raw), (decoder, info, source) -> {
                        decoder.setAllocator(ImageDecoder.ALLOCATOR_SOFTWARE);
                        float scale = Math.min(1f, 1600f / Math.max(info.getSize().getWidth(), info.getSize().getHeight()));
                        decoder.setTargetSize(Math.max(1, Math.round(info.getSize().getWidth()*scale)), Math.max(1, Math.round(info.getSize().getHeight()*scale)));
                    });
                } else {
                    BitmapFactory.Options options = new BitmapFactory.Options(); options.inSampleSize = 1;
                    while (Math.max(bounds.outWidth, bounds.outHeight) / options.inSampleSize > 1600) options.inSampleSize *= 2;
                    bitmap = BitmapFactory.decodeFile(raw.getPath(), options);
                    if (bitmap == null) throw new IOException("Unsupported image");
                    int orientation = new ExifInterface(raw.getPath()).getAttributeInt(ExifInterface.TAG_ORIENTATION, 1);
                    Matrix matrix = new Matrix();
                    switch (orientation) {
                        case 2: matrix.setScale(-1, 1); break;
                        case 3: matrix.setRotate(180); break;
                        case 4: matrix.setScale(1, -1); break;
                        case 5: matrix.setRotate(90); matrix.postScale(-1, 1); break;
                        case 6: matrix.setRotate(90); break;
                        case 7: matrix.setRotate(-90); matrix.postScale(-1, 1); break;
                        case 8: matrix.setRotate(-90); break;
                    }
                    if (!matrix.isIdentity()) { Bitmap rotated = Bitmap.createBitmap(bitmap, 0, 0, bitmap.getWidth(), bitmap.getHeight(), matrix, true); if (rotated != bitmap) bitmap.recycle(); bitmap = rotated; }
                }
                String filename = UUID.randomUUID().toString().replace("-", "") + ".jpg"; out = new File(photos, filename);
                // Re-encoding strips original location and camera metadata.
                try (FileOutputStream stream = new FileOutputStream(out)) {
                    if (!bitmap.compress(Bitmap.CompressFormat.JPEG, 88, stream)) throw new IOException("Encode failed");
                    stream.getFD().sync();
                }
                publish("ok:" + filename);
            } catch (Exception | OutOfMemoryError e) {
                if (out != null) out.delete();
                publish("error:사진을 불러오지 못했어요. 25MB 이하의 다른 사진을 선택해 주세요.");
            } finally {
                if (raw != null) raw.delete(); if (bitmap != null) bitmap.recycle(); runOnUiThread(this::finish);
            }
        }, "OpenedNotePhoto").start();
    }
}
