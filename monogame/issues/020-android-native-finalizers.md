# 020. Android graphics-resource finalization

Part: framework (`GraphicsResource`, `GraphicsDevice.Native`). No native ABI change.

## Need

A graphics resource's CLR finalizer can execute on a different thread from the Vulkan
render loop. Native texture, buffer and state destruction modifies runtime containers;
shader destruction can destroy descriptor pools. The existing native calls do not
provide a general cross-thread destruction contract. A queue-idle wait on the render
thread is not a lock against CLR finalizers.

## Change

The Android host selects deferred finalization before creating the device. Finalizers
queue resource objects instead of entering native code; the graphics-device owner
processes a snapshot after serial `Present` and during device disposal. Weak resource
references track resurrection in this mode so explicit device disposal can find objects
awaiting finalization. An already disposed object is skipped; finalizers arriving after
queue closure cannot enter the destroyed native device. Native release does not run under
the producer lock. Explicit disposal retains its current contract and must still obey
render-thread ownership. Desktop behavior and the Android queue-idle workaround are
unchanged. Do not remove the serial wait on the strength of this change alone.

## Verification

The regression runner's `native finalizer queue` cases exercise concurrent producers,
release order, draining outside the producer lock, counters and closure. The Android
performance log reports cumulative queued, drained and rejected finalizer requests.
Verify repeated launches, login, map streaming, rotation, pause/resume and orderly exit
on the affected device. This correction removes a confirmed thread-ownership defect;
it does not establish the root cause of a specific Mali driver backtrace. Swapchain
recreation, native resource retirement and explicit worker-thread disposal remain
separate audit areas.
