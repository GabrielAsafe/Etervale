# Vulkan swapchain resource lifetime

Swapchain recreation waits for the existing queue/device before retiring resources. It invalidates the old
backbuffer wrappers in the bound target state, releases framebuffers before their views, and releases the
swapchain image views before the swapchain. Swapchain-owned images have no VMA allocation to free.

When the image count changes, the old command buffers are freed, descriptor-cache entries are returned to
the free lists without an age calculation (their uniform buffers are being replaced), and the frame index
is recomputed from the current frame number and new image count. Present records the old submitted slot
before recreation can invalidate its reference, and checks the queue submission result before presenting.

The Android serial-present workaround remains enabled. This correction does not change the exported
native interface. It does not yet handle every surface-loss/acquisition error or every resource disposal path.

## Regression procedure

On Android, repeatedly launch the game, rotate in both directions, background and resume, and change
maps. Check for correct rendering, SIGSEGV in QueueSubmit, SIGABRT in the Mali backend, and hangs.
On desktop, resize repeatedly and change presentation settings. Exercise both unchanged and changed
swapchain image counts when supported by the driver, and run with Vulkan validation enabled where available.
