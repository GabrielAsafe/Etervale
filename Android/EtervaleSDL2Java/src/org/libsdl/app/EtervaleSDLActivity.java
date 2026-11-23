package org.libsdl.app;

import android.os.Build;
import android.os.Bundle;
import android.view.View;
import android.view.WindowManager;

/** ETERVALE_ANDROID_V5: JNI SDL2 Activity + landscape immersive SurfaceView. */
public class EtervaleSDLActivity extends SDLActivity {
    private void applyGameDisplay() {
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN);
        if (Build.VERSION.SDK_INT >= 28) {
            WindowManager.LayoutParams p = getWindow().getAttributes();
            p.layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
            getWindow().setAttributes(p);
        }
        getWindow().getDecorView().setSystemUiVisibility(
            View.SYSTEM_UI_FLAG_FULLSCREEN
            | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
            | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
            | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
            | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
            | View.SYSTEM_UI_FLAG_LAYOUT_STABLE);
    }

    @Override protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        applyGameDisplay();
    }

    @Override protected void onResume() {
        super.onResume();
        applyGameDisplay();
    }

    @Override public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) applyGameDisplay();
    }

    public static boolean surfaceReady() {
        return mSurface != null && mSurface.getHolder() != null
            && mSurface.getHolder().getSurface() != null
            && mSurface.getHolder().getSurface().isValid();
    }
}
