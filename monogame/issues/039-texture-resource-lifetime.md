# Texture and depth attachment resource lifetime

Owned render targets keep their private depth attachments until the owner's retirement. The texture queue waits for
both the disposal frame and the latest tracked use to age past the existing frames-in-flight budget. Drawing into a
render target stamps its lifetime frame, in addition to its write frame. Future native sampled and storage-image
bindings are cleared on disposal without rewriting recorded descriptors. Native target references are cleared before
freeing their wrappers. Cached framebuffers are destroyed before slice views; the private depth image and view are
released only after the owner's target caches have been removed.

Wrapped external images retain their existing synchronous ownership contract, with a checked idle wait and ordered
attachment destruction. This change does not make it valid to release an external image while commands referencing it
are still being recorded. Explicit disposal must occur on the graphics thread; GC disposal uses the device's queue.
No additional wait is introduced in the ordinary frame path. Transfer completion errors and shader/descriptor pool
retirement are separate issues.

Verification: StateCacheRepro records a depth-backed target, samples it into a persistent target, retires the temporary
target before Present, then reads and compares the output on the next frame. Run the 2,000-frame stress test and
DepthSurfaceRepro with validation enabled as well as the standard regression suite. Pixel tests alone cannot establish
that every native lifetime rule is satisfied.
