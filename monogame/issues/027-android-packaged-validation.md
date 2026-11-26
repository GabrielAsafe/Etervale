# Android packaged Vulkan validation diagnostics

The optional Android diagnostic build packages the ARM64 Khronos validation layer and enables it explicitly during
instance creation. It does not depend on privileged Android GPU debug settings. A debug-utils messenger writes warnings
and errors to `ETERVALE_VK_VALIDATION` in logcat; successful activation is logged after messenger creation.
The build fails to create a graphics instance if the requested layer or messenger is unavailable. The render-fence
presentation policy is unchanged. The native ABI and the default non-diagnostic build are unchanged.
The diagnostic instance also requests synchronization validation with `VK_EXT_layer_settings` (`validate_sync`),
falling back to `VK_EXT_validation_features` when the layer or headers lack layer settings support. Both extension
availability and successful instance creation are checked. Core validation remains enabled. The extra render fence
remains in place to isolate this diagnostic from presentation-policy changes. Collect `SYNC-HAZARD` messages from
startup, rotation, world streaming, gameplay and normal shutdown before testing asynchronous presentation.

The asynchronous diagnostic skips the additional post-present fence wait only when `ETERVALE_VK_VALIDATION` is
compiled in on Android. Ordinary builds retain the render-fence baseline. Frame-slot fences, image-acquisition and
presentation semaphores, resource retirement and swapchain recreation synchronization are unchanged. The validation
callback reports synchronization hazards and core errors. A driver crash without a validation error does not prove
that the runtime is correct; validation coverage is incomplete and instrumentation changes timing. Restore the fence
configuration by reversing this isolated diagnostic patch if the asynchronous experiment fails.

Enable the native CMake option `ETERVALE_VK_VALIDATION` and pass the absolute ARM64 library path as the
`EtervaleValidationLayer` MSBuild property in a Debug APK build. Only use this APK for diagnostics, not performance
measurement or distribution. Verify the APK version, both native libraries and the activation log before testing.
Disable the CMake option and omit the MSBuild property for ordinary builds. Restore the manifest version after the test.

See [transfer completion checks](030-vulkan-transfer-completion.md) for the 14E.2 follow-up.
