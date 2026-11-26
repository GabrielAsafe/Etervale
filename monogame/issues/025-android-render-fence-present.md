# Android render-fence presentation experiment

The Android Vulkan presentation path waits for the submitted frame's completion fence
instead of the entire graphics queue after `vkQueuePresentKHR`. Rendering remains
serialized at the frame boundary, while presentation may still be pending. The fence
is the existing fence passed to `vkQueueSubmit`; its result is checked and it is not
reset here. The existing acquire semaphore, swapchain-image render-complete semaphore,
frame-slot recycling and swapchain recreation waits are unchanged. The graphics fence
alone does not establish that the presentation operation consumed its semaphore.
No other synchronization or resource-lifetime behavior is changed. Desktop behavior
and the native ABI are unchanged.

This is a diagnostic intermediate configuration, not proof that asynchronous
rendering is safe. Compare with the serial-queue baseline on the same device and
scene, with identical orientation and graphics settings. Repeat launches, resize,
background/resume, map transitions, combat and normal shutdown. Check for new native
crashes, visual corruption, hangs, and timestamp errors. Restore the serial-queue
configuration if any regression occurs. Validate on-device before removing the
render-fence wait in a separate experiment.
