# HDR render target multisample capabilities

The Vulkan runtime queries the sample-count mask of the actual multisampled colour attachment and the
separate depth attachment, intersecting both with framebuffer limits. It selects the largest supported
count no greater than the normalized request; zero means single-sampled. The query is for a colour
2D target with one mip and one layer. It does not change the existing backbuffer or generic texture API.

`PristonSceneTargets` performs the query only while recreating scene targets, passing the negotiated count
into `RenderTarget2D` and retaining the original request in its allocation cache. The MSAA mode still
excludes the depth pre-pass and temporal resolve, even if the device must fall back to one sample.
The runtime and framework interface version is 8; rebuild native libraries before running the client.

Verification: `dotnet run --project monogame/runtime/repro/DepthSurfaceRepro -c Release` exercises HDR
colour and depth rendering at requested sample counts 0, 2 and 4, checking the negotiated count and
resolved pixel. Run under Vulkan validation on desktop and repeat on devices with sparse sample masks.
