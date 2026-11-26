# Vulkan transfer submission and completion checks

The 14E.1 Android diagnostic session reported fence destruction and command-buffer release while pending, then a
SIGSEGV inside the validation layer on a .NET worker thread. This does not identify the failing call site or establish
that the earlier Mali crashes share the same cause. Several transfer paths discarded submission and wait results;
nonblocking retirement treated every status except NOT_READY as completion.

Check allocation, recording, submission and completion results in the touched transfer paths. Retire pending uploads
only after a zero-timeout fence wait returns SUCCESS; TIMEOUT retains the submission, staging buffer and fence.
Unexpected results fail fast rather than freeing potentially pending resources. This is not device-loss recovery.
The validation callback identifies the current transfer path when an error is reported synchronously on that thread.
No additional per-frame queue-wide wait is introduced. Existing frame-slot fences and Android presentation policy are
unchanged. Other device-loss, lifetime, and lock-order paths require separate review.

Re-test startup, field streaming, movement and graphics-setting changes with synchronization validation enabled.
If a successful wait still produces an in-use error, collect the path and investigate driver/layer tracking separately.
The earlier image-layout error remains a separate investigation.

See [fence timeout handling](031-vulkan-fence-timeout-retry.md) for the 14E.3 follow-up.
