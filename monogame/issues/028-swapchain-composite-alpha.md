# Swapchain composite alpha selection

Swapchain creation selects the first supported mode from opaque, inherit, pre-multiplied and post-multiplied alpha.
Opaque remains preferred when supported. Android surfaces that advertise only inherit use that mode instead of an
invalid opaque request (VUID-VkSwapchainCreateInfoKHR-compositeAlpha-01280). The selected mode and supported mask
are logged under `ETERVALE_VK_ROTATION` at swapchain creation. If no known mode is supported, creation stops instead
of submitting an invalid create info.

The surface transform, present mode, render fence, resource lifetimes and native ABI are unchanged. The diagnostic
APK retains the packaged validation layer. Verify startup and rotation with the layer active: the composite-alpha
VUID must be absent and the chosen mode must be a bit in the supported mask. Check opacity and visual alignment in
portrait and landscape. The separate Vulkan API version violation is not addressed by this change.
