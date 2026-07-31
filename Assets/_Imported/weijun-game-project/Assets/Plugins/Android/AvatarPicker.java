package com.weijun.avatar;

import android.app.Activity;
import android.app.Fragment;
import android.content.ContentResolver;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.os.Bundle;

import com.unity3d.player.UnityPlayer;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.util.UUID;

public final class AvatarPicker {
    private static final String FragmentTag = "WeijunAvatarPicker";

    private AvatarPicker() {
    }

    public static void chooseImage(final String receiverName) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            send(receiverName, "OnAvatarSelectionFailed", "无法打开手机相册");
            return;
        }

        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (activity.getFragmentManager().findFragmentByTag(FragmentTag) != null) {
                    send(receiverName, "OnAvatarSelectionFailed", "头像选择窗口已打开");
                    return;
                }

                PickerFragment fragment = new PickerFragment();
                Bundle arguments = new Bundle();
                arguments.putString("receiverName", receiverName);
                fragment.setArguments(arguments);
                activity.getFragmentManager()
                    .beginTransaction()
                    .add(fragment, FragmentTag)
                    .commitAllowingStateLoss();
                activity.getFragmentManager().executePendingTransactions();
                fragment.openGallery();
            }
        });
    }

    private static void send(final String receiverName, final String method, final String message) {
        final Activity activity = UnityPlayer.currentActivity;
        Runnable callback = new Runnable() {
            @Override
            public void run() {
                UnityPlayer.UnitySendMessage(receiverName, method, message == null ? "" : message);
            }
        };
        if (activity == null) {
            callback.run();
        } else {
            activity.runOnUiThread(callback);
        }
    }

    public static final class PickerFragment extends Fragment {
        private static final int RequestImage = 9182;
        private String receiverName;

        @Override
        public void onCreate(Bundle savedInstanceState) {
            super.onCreate(savedInstanceState);
            receiverName = getArguments() == null ? "" : getArguments().getString("receiverName", "");
        }

        public void openGallery() {
            try {
                Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
                intent.addCategory(Intent.CATEGORY_OPENABLE);
                intent.setType("image/*");
                startActivityForResult(intent, RequestImage);
            } catch (Exception exception) {
                fail("无法打开手机相册：" + exception.getMessage());
            }
        }

        @Override
        public void onActivityResult(int requestCode, int resultCode, Intent data) {
            super.onActivityResult(requestCode, resultCode, data);
            if (requestCode != RequestImage) {
                return;
            }
            if (resultCode != Activity.RESULT_OK || data == null || data.getData() == null) {
                fail("头像选择已取消");
                return;
            }

            final Uri imageUri = data.getData();
            new Thread(new Runnable() {
                @Override
                public void run() {
                    createTemporaryJpeg(imageUri);
                }
            }, "WeijunAvatarPicker").start();
        }

        private void createTemporaryJpeg(Uri imageUri) {
            Activity activity = getActivity();
            if (activity == null) {
                fail("头像选择窗口已关闭");
                return;
            }

            try {
                byte[] jpegBytes = decodeAndCompress(activity.getContentResolver(), imageUri);
                if (jpegBytes == null || jpegBytes.length == 0) {
                    fail("所选图片无法读取");
                    return;
                }

                File outputFile = new File(
                    activity.getCacheDir(),
                    "weijun-avatar-" + UUID.randomUUID().toString() + ".jpg");
                FileOutputStream outputStream = new FileOutputStream(outputFile);
                outputStream.write(jpegBytes);
                outputStream.close();

                JSONObject payload = new JSONObject();
                payload.put("filePath", outputFile.getAbsolutePath());
                payload.put("fileName", "avatar.jpg");
                payload.put("mimeType", "image/jpeg");
                payload.put("temporaryFile", true);
                send(receiverName, "OnAvatarSelected", payload.toString());
                removeSelf();
            } catch (Exception exception) {
                fail("头像图片处理失败：" + exception.getMessage());
            }
        }

        private byte[] decodeAndCompress(ContentResolver resolver, Uri imageUri) throws Exception {
            BitmapFactory.Options bounds = new BitmapFactory.Options();
            bounds.inJustDecodeBounds = true;
            InputStream boundsStream = resolver.openInputStream(imageUri);
            BitmapFactory.decodeStream(boundsStream, null, bounds);
            if (boundsStream != null) {
                boundsStream.close();
            }

            int sampleSize = 1;
            while (bounds.outWidth / sampleSize > 1600 || bounds.outHeight / sampleSize > 1600) {
                sampleSize *= 2;
            }

            BitmapFactory.Options decodeOptions = new BitmapFactory.Options();
            decodeOptions.inSampleSize = sampleSize;
            decodeOptions.inPreferredConfig = Bitmap.Config.ARGB_8888;
            InputStream imageStream = resolver.openInputStream(imageUri);
            Bitmap decoded = BitmapFactory.decodeStream(imageStream, null, decodeOptions);
            if (imageStream != null) {
                imageStream.close();
            }
            if (decoded == null) {
                return null;
            }

            Bitmap prepared = decoded;
            int largestSide = Math.max(decoded.getWidth(), decoded.getHeight());
            if (largestSide > 1600) {
                float scale = 1600f / largestSide;
                int width = Math.max(1, Math.round(decoded.getWidth() * scale));
                int height = Math.max(1, Math.round(decoded.getHeight() * scale));
                prepared = Bitmap.createScaledBitmap(decoded, width, height, true);
            }

            ByteArrayOutputStream output = new ByteArrayOutputStream();
            int quality = 90;
            do {
                output.reset();
                prepared.compress(Bitmap.CompressFormat.JPEG, quality, output);
                quality -= 10;
            } while (output.size() > 1900 * 1024 && quality >= 50);

            byte[] result = output.toByteArray();
            output.close();
            if (prepared != decoded) {
                prepared.recycle();
            }
            decoded.recycle();
            return result;
        }

        private void fail(String message) {
            send(receiverName, "OnAvatarSelectionFailed", message);
            removeSelf();
        }

        private void removeSelf() {
            final Activity activity = getActivity();
            if (activity == null) {
                return;
            }
            activity.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    if (isAdded()) {
                        activity.getFragmentManager()
                            .beginTransaction()
                            .remove(PickerFragment.this)
                            .commitAllowingStateLoss();
                    }
                }
            });
        }
    }
}
