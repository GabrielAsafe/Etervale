# Vulkan 1.0 extended feature queries

The runtime requests Vulkan 1.0 at instance creation. Custom border color discovery uses
`vkGetPhysicalDeviceFeatures2KHR` through the enabled `VK_KHR_get_physical_device_properties2` instance extension,
not the Vulkan 1.1 core entry point. The extension entry point is resolved for that instance. If it is unavailable,
or either custom border color feature is missing, the sampler uses its existing non-custom fallback. Both feature
bits are necessary because custom border color samplers in the runtime use `VK_FORMAT_UNDEFINED`.

The instance API version, synchronization, rendering quality and ABI are unchanged. With the packaged validation
layer enabled, verify that the feature query reports `Features2KHR available=1` on the target device and that
`UNASSIGNED-API-Version-Violation` no longer occurs for this call. Verify startup, gameplay and rotation. The
composite alpha selection must remain valid. This does not by itself establish the cause of the Mali crashes.
