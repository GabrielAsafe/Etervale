# Custom border colours on Vulkan 1.0 instances

The runtime requests Vulkan 1.0 and enables `VK_KHR_get_physical_device_properties2` when available.
Custom border colour detection resolves `vkGetPhysicalDeviceFeatures2KHR` from the instance instead of
calling the Vulkan 1.1 core entry point. The device extension is enabled only if the query is available
and reports both `customBorderColors` and `customBorderColorWithoutFormat`.

Samplers specify `VK_FORMAT_UNDEFINED` for custom colours. When either feature is absent, the existing
standard border colour fallback is used. Standard transparent black, opaque black and opaque white
continue to use their existing paths. No new extension or minimum Vulkan version is required.

Verification: initialize a device with validation, draw with standard and custom border colours, and
check the fallback on an implementation without both custom border features. Run the existing runtime
repro programs and renderer benchmark; verify the absence of feature-query and sampler VUIDs.
