# Surface alpha and empty graphics descriptor binds

Swapchain creation selects the first supported composite alpha mode, preferring opaque when available.
The surface capability mask governs this selection, rather than a platform assumption.
Graphics programs without resource bindings skip descriptor-set binding; programs with bindings retain
normal descriptor allocation, binding and dirty-state handling.

Verification: create and resize a window with validation enabled; exercise graphics programs with and
without resource bindings (including shadow passes). Check for swapchain composite-alpha and
`descriptorSetCount` validation errors. Run the existing StateCacheRepro and a render benchmark before
and after the change. The Android checkpoint is not a substitute for desktop validation.
