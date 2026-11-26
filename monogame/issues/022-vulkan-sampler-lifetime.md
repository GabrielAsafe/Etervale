# Vulkan sampler retirement

`MGG_SamplerState_Destroy` queued native samplers, but the next frame-resource cleanup destroyed
all of them without checking the age of GPU work or the native binding slots. A descriptor set
can still reference a sampler used in a frame in flight. A native binding slot can also retain
its pointer until a later state application replaces it.

The destruction request now stamps the retirement frame. Each cleanup visits the pending queue
once, retains samplers that are still bound (refreshing their retirement frame), and waits the
existing `freeFrames` interval for unbound samplers. Other retired samplers are not blocked by
one still-bound sampler. Swapchain recreation keeps bound samplers alive; final device destruction,
after the existing device-idle wait, clears the binding slots and drains the retirement queue.
This is a conservative lifetime correction using the existing frame/fence scheme, not proof that
all resource lifetimes are safe. It adds no queue-idle wait and changes no exported ABI.
The Android serial Present workaround remains enabled.

## Regression procedure

- Dispose a sampler after binding it, submit several frames, replace that binding, and submit
  at least `freeFrames` more frames. Verify that no submitted or bound sampler is destroyed early.
- Retire a second unbound sampler while the first is still bound. Verify that the second is freed
  after its retirement interval rather than being blocked by the first.
- Recreate the swapchain while a retired sampler is still bound; it must survive until replacement.
- Close the graphics device with pending retirements; the device-idle cleanup must drain them.
- Run the Android rotation, gameplay, and repeated launch/exit tests with serial Present enabled;
  run Vulkan validation on a supported development device. Shader, image and explicit cross-thread
  disposal paths require separate review.
