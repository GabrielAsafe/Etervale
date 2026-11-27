# GPU timestamp capabilities and elapsed intervals

The selected graphics queue family determines whether timestamps are supported (`timestampValidBits`), not just
whether Vulkan can allocate a query pool. Unsupported queues return no pool and leave the CPU profiler usable.
The elapsed query API reads both consecutive timestamps in one non-blocking operation and subtracts raw ticks
modulo the number of valid bits before converting to nanoseconds. The interval must be shorter than a full wrap.
The previous absolute timestamp API remains available; the engine profiler uses the elapsed API. No new frame wait
or per-frame allocation is introduced. The existing four-frame query-slot policy is unchanged: slot generation and
repeated sections require separate review. The runtime interface is version 10.

Run `dotnet run --project monogame/runtime/repro/GpuTimestampRepro -c Release` on a timestamp-capable GPU. The
repro records 16 unique pairs, polls without waiting, and checks that all become available with plausible durations.
It exits 2 on unsupported hardware, not a successful GPU test. This does not replace the render benchmark or
validation on the actual GPU. The test intentionally does not exercise counter wrap on hardware; the native
modular arithmetic can be tested with synthetic tick pairs.
