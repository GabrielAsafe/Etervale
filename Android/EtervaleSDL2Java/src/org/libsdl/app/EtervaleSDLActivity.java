package org.libsdl.app;

import android.content.pm.ActivityInfo;
import android.os.Build;
import android.os.Bundle;
import android.view.View;
import android.view.WindowManager;

/** ETERVALE_ANDROID_V5: JNI SDL2 Activity + landscape immersive SurfaceView. */
public class EtervaleSDLActivity extends SDLActivity {
    // ETERVALE_ANDROID_FIXED_LANDSCAPE_V13
    // SDL2 may request portrait when creating a resizable window; keep the
    // physical SurfaceView in landscape, matching MainActivity and the HUD.
    @Override
    public void setOrientationBis(int width, int height, boolean resizable, String hint) {
        // ETERVALE_SDL_ORIENTATION_DISABLED_V18: ignorar qualquer pedido SDL via JNI.
        // A orientacao passa a ser uma decisao do Android e do utilizador.
        android.util.Log.i("ETERVALE_SDL_ORIENTATION", "ignored SDL orientation request"
            + "; width=" + width + "; height=" + height + "; resizable=" + resizable);
    }

    private void applyGameDisplay() {
        // v18: conservar fullscreen/cutout, sem modificar a orientacao.
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
        if (hasFocus) {
            applyGameDisplay();
            logCalibration("focus");
            // ETERVALE_ANDROID_VIEWPORT_V14: actual Android window and rotation.
            android.util.DisplayMetrics physical = new android.util.DisplayMetrics();
            getWindowManager().getDefaultDisplay().getRealMetrics(physical);
            View decor = getWindow().getDecorView();
            android.util.Log.i("ETERVALE_DISPLAY", "rotation="
                + getWindowManager().getDefaultDisplay().getRotation()
                + "; physical=" + physical.widthPixels + "x" + physical.heightPixels
                + "; decor=" + decor.getWidth() + "x" + decor.getHeight()
                + "; cutout=" + getWindow().getAttributes().layoutInDisplayCutoutMode);
        }
    }

    // ETERVALE_ROTACAO_LIVRE_V16: telemetria de calibracao do sensor.
    private void logCalibration(String source) {
        android.util.DisplayMetrics physical = new android.util.DisplayMetrics();
        getWindowManager().getDefaultDisplay().getRealMetrics(physical);
        View decor = getWindow().getDecorView();
        int surfaceWidth = mSurface == null ? 0 : mSurface.getWidth();
        int surfaceHeight = mSurface == null ? 0 : mSurface.getHeight();
        android.util.Log.i("ETERVALE_ROTATION", "source=" + source
            + "; rotation=" + getWindowManager().getDefaultDisplay().getRotation()
            + "; physical=" + physical.widthPixels + "x" + physical.heightPixels
            + "; decor=" + decor.getWidth() + "x" + decor.getHeight()
            + "; surface=" + surfaceWidth + "x" + surfaceHeight
            + "; requested=" + getRequestedOrientation());
    }

    @Override public void onConfigurationChanged(android.content.res.Configuration config) {
        super.onConfigurationChanged(config);
        getWindow().getDecorView().post(new Runnable() {
            @Override public void run() { logCalibration("configuration"); }
        });
    }

    public static boolean surfaceReady() {
        return mSurface != null && mSurface.getHolder() != null
            && mSurface.getHolder().getSurface() != null
            && mSurface.getHolder().getSurface().isValid();
    }
}
