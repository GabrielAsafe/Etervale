# Vulkan empty graphics descriptor binding

APK 44 validation reported VUID-vkCmdBindDescriptorSets-descriptorSetCount-arraylength
when a graphics program with no resource bindings reached MGVK_UpdatePipeline.
The dirty-state path can compute setCount == 0. Skip vkCmdBindDescriptorSets in
that case; retain the existing descriptor construction and dirty-state clearing.
This is valid for all Vulkan platforms and adds no allocations or synchronization.
The Android validation build remains asynchronous and keeps the fence retry fix.

Test by enabling and disabling shadows and cycling graphics options; ensure no
empty descriptor binding VUID appears and verify rendering for both resource-free
and resource-using programs. Unsupported MSAA sample counts and the previously
observed image-layout VUID remain separate issues. Native scene resolution at
100% is still a required final Android milestone.
