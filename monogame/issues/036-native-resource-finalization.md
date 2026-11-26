# Native graphics resource finalization

A `GraphicsResource` associated with a Native graphics device publishes a managed reference to that device's
finalization queue from its GC finalizer. The GC thread does not invoke the resource's native disposal path.
The graphics thread drains a bounded snapshot of the queue after `Present`, preserving `Dispose(false)` semantics
(no `Disposing` event). Explicit `Dispose()` continues to run synchronously on the caller's thread.

Device shutdown closes the queue and drains it before the resource registry is swept and the native device destroyed.
The Native resource registry uses resurrection-tracking weak references so that a pending finalizer can still be
seen by the shutdown sweep. The resource lock serializes the queue close with enqueues. The graphics device must
be disposed explicitly on its owning thread, as before; concurrent device disposal and drawing are not supported.
The change does not make arbitrary explicit disposal from worker threads safe, nor replace native fences or
retirement of resources used by in-flight GPU commands. Resources that deliberately skip native cleanup in
`Dispose(false)` retain their existing policy.

Verification: `dotnet run --project monogame/runtime/repro/DepthSurfaceRepro -c Release` forces GC of an unbound
texture, verifies that the object is retained until `Present` and disposed afterwards, and checks synchronous
explicit disposal. Run with Vulkan validation and exercise scene streaming and shutdown separately.
