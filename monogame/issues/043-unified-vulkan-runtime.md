# 043. Unified Vulkan runtime interface and lifetime policy

Part: runtime and framework.

## Contract

The common library exports both render-target sample queries. The dimension-aware query checks the attachment image
properties and framebuffer limits; the format-only query uses the same helper for a single-level, single-layer image.
The framework requires ABI 10. GPU timestamp pairs use the selected graphics queue's valid bits and subtract before
converting to nanoseconds. A pending render-target switch can end the old pass before resetting queries.

Android preserves identity presentation transforms where supported, packaged validation, and bounded retry of the
same fence after VK_TIMEOUT. Other platforms use a checked infinite wait. Transfer polling remains nonblocking.
Idle, command recording and submission failures are fatal; no extra queue-wide wait is added to ordinary Present.

The common retirement path keeps bound samplers, textures and shaders alive until they are no longer bound and the
retirement interval expires. Owned depth attachments retire after their owner's framebuffers. The shader request
queue is protected by a mutex. This conservative policy supersedes the desktop-only immediate unbinding strategy
in issues 038–040. Descriptor caches are recycled before old frame uniforms are replaced during swapchain resize.

## Verification

Build the desktop and Android libraries from the same sources. Run the timestamp, state-cache, depth-surface and
compute repros on desktop. On Android run packaged validation and a non-validation build with rotation, rendering
settings and lifecycle regressions; verify the APK contains the ABI 10 library. Benchmark both platforms before release.
