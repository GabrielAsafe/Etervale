# Swapchain resource lifetime

The queue must complete before swapchain resources are destroyed. The cleanup path clears native target bindings
that refer to the old swapchain, destroys cached framebuffers before attachment views, and destroys the swapchain
only after its views. Swapchain images are owned by Vulkan and are not freed through VMA.

Present records frame completion before out-of-date handling can replace the frame vector. When the image count
changes, idle descriptor sets are recycled explicitly, capture slots are collected, old command buffers are freed,
and the frame index is calculated from the new count. The normal frame path has no additional idle waits.

Verification: run `dotnet run --project monogame/runtime/repro/StateCacheRepro -c Release -- 2000` with a visible
window. Its first eight stress frames alternate requested window sizes while the original pixel checks continue.
Use Vulkan validation for a separate run, and manually resize, minimize, restore and toggle fullscreen in the client.
The window manager can constrain size requests; this test does not guarantee a change in swapchain image count.
Acquisition failure recovery and synchronization of unrelated resource destruction remain separate concerns.
