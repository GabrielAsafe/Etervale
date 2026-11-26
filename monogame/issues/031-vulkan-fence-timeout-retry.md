# Android Vulkan fence timeout handling

The APK 43 diagnostic caught VK_TIMEOUT (2) in MGVK_ExecuteAndFreeCommandBuffer while
creating a texture, before the fence or command buffer could be destroyed. It had
requested an effectively infinite wait. Skia documents premature timeout returns on
older Mali drivers when a signal interrupts a fence wait (2026-06-25, commit
f2169b805f6ed719d8b3adcb6a015747f8f66015):
https://skia.googlesource.com/skia/+/f2169b805f6ed719d8b3adcb6a015747f8f66015%5E!/
This is consistent with the observation, but does not establish the precise driver cause.

On Android, retry the same submitted fence using one-second waits, with a short sleep
after a timeout and an elapsed retry budget of 30 seconds. A successful wait permits
reuse or destruction; an unexpected result or exhausted retry budget fails fast,
without releasing the pending resources. This is not device-loss recovery or a hard
watchdog for a driver that never returns. The existing desktop blocking waits retain
their original timeout and now require success. Keep zero-timeout transfer polling
nonblocking. No extra per-frame wait or queue-wide serialization is added: only
existing blocking fence waits change, including frame-slot reuse and the non-diagnostic
post-present fence. The diagnostic build remains asynchronous with validation enabled.

Re-test startup, movement/streaming, and the prior failure window. Look for timeout
and recovery messages and check that no in-use fence or command-buffer VUID returns.
The earlier image-layout VUID is still a separate open issue. Native-resolution
restoration (100% scene scale) remains a required final Android milestone.

See [empty descriptor binding](032-vulkan-empty-descriptor-bind.md) for the 14E.5 follow-up.
