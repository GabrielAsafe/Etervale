# Vulkan transfer and fence completion

The Vulkan runtime requires `VK_SUCCESS` from blocking fence waits and from the checked command-buffer and fence
operations before continuing. An unexpected status (including `VK_TIMEOUT` from an infinite wait) or an error
terminates with a diagnostic in Release as well as Debug; resources are not released based on a failed wait.
Pending transfer cleanup polls the same fence with a zero timeout. `VK_TIMEOUT` retains the original transfer,
while only `VK_SUCCESS` permits freeing the command buffer, staging allocation and fence. The transfer pool reset
is checked as well. The normal frame path does not add any blocking wait or queue-idle call.

This desktop change deliberately does not include the Android-specific bounded retry for interrupted fence waits.
Device-loss recovery is not implemented; fail-fast is safer than assuming work finished. It also does not change
queue locking or semaphore ownership. Verify with StateCacheRepro, DepthSurfaceRepro, ComputeRepro (including the
worker-thread 16 MB upload), and TextureLoadRepro, plus the normal tests. Vulkan validation on the actual GPU remains
necessary; successful pixel checks alone cannot demonstrate every synchronization property.
