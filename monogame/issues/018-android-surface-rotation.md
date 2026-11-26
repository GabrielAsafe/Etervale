# Android surface rotation

## Contract

The Android activity leaves device orientation to the system. SDL forwards drawable dimensions to the managed window.
The renderer uses logical window coordinates for both the scene and the UI. It does not pre-rotate the complete image
into the panel's natural orientation. When supported, the Android Vulkan swapchain therefore requests the identity
transform and leaves rotation to the presentation engine. Other platforms retain their existing transform policy.
If identity is unavailable, the existing current-transform choice is retained; that fallback needs device validation.

## Verification

`ETERVALE_SDL_RESIZE` records SDL event dimensions, drawable dimensions and the dimensions forwarded to MonoGame.
`ETERVALE_VK_ROTATION` records requested and actual swapchain image extents and surface transform flags at creation.
Neither log is emitted on ordinary frames. The runtime interface is unchanged.

On Android, compare portrait, landscape and reverse landscape at startup and during play, checking that the scene and
HUD are upright, correctly scaled, and aligned with touch. Confirm that returning to the initial orientation works.
Repeat with the device orientation locked. Compare the logs if the rendered dimensions or orientation are incorrect.
On desktop, rebuild the runtime and run the existing regression suite to check that the unchanged policy still works.

See https://docs.vulkan.org/samples/latest/samples/performance/surface_rotation/README.html for the two rotation modes.
