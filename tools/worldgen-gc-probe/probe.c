/* Optional Windows/Mono diagnostic, never a gameplay dependency.
 * Observer ABI: Unity-Technologies/mono, mono/metadata/profiler.h and
 * profiler-events.h (profiler API 3). No runtime internals are patched.
 * Callback must NEVER enter managed code, allocate, take locks, or write files.
 */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>

#define EVENT_CAPACITY 131072
typedef struct { int64_t qpc; int32_t event; uint32_t generation, thread; int32_t serial; } ProbeEvent;
typedef void (__cdecl *GcCallback)(void *, int, uint32_t, int);
typedef void *(__cdecl *CreateProfiler)(void *);
typedef void (__cdecl *SetCallback)(void *, GcCallback);
static ProbeEvent events[EVENT_CAPACITY];
static volatile LONG published[EVENT_CAPACITY];
static volatile LONG next_event, active;
static void *profiler;
static SetCallback set_callback;

__declspec(dllexport) int __cdecl WorldGenGcEventSize(void) { return sizeof(ProbeEvent); }

static void __cdecl observe_gc(void *unused, int event, uint32_t generation, int serial)
{
    (void)unused;
    if (!InterlockedCompareExchange(&active, 0, 0)) return;
    LONG index = InterlockedIncrement(&next_event) - 1;
    if (index < 0 || index >= EVENT_CAPACITY) return;
    LARGE_INTEGER now;
    QueryPerformanceCounter(&now);
    events[index].qpc = now.QuadPart;
    events[index].event = event;
    events[index].generation = generation;
    events[index].thread = GetCurrentThreadId();
    events[index].serial = serial;
    InterlockedExchange(&published[index], 1);
}

__declspec(dllexport) int __cdecl WorldGenGcStart(void)
{
    if (profiler) return -3; /* One bounded capture per process; never reset a live buffer. */
    HMODULE mono = GetModuleHandleW(L"mono-2.0-bdwgc.dll");
    if (!mono) return -1;
    CreateProfiler create = (CreateProfiler)GetProcAddress(mono, "mono_profiler_create");
    set_callback = (SetCallback)GetProcAddress(mono, "mono_profiler_set_gc_event_callback");
    if (!create || !set_callback) return -2;
    profiler = create(events);
    if (!profiler) return -4;
    InterlockedExchange(&active, 1);
    set_callback(profiler, observe_gc);
    return 1;
}

__declspec(dllexport) int __cdecl WorldGenGcStop(void)
{
    InterlockedExchange(&active, 0);
    if (profiler) set_callback(profiler, NULL);
    return InterlockedCompareExchange(&next_event, 0, 0);
}

__declspec(dllexport) int __cdecl WorldGenGcRead(int index, ProbeEvent *result)
{
    if (!result || index < 0 || index >= EVENT_CAPACITY) return 0;
    if (!InterlockedCompareExchange(&published[index], 0, 0)) return 0;
    *result = events[index];
    return 1;
}

/* The module stays loaded until process exit: Mono permits an in-flight
 * callback after callback removal. Storage and callback addresses remain valid.
 * Neither the buffer nor the Mono-owned profiler descriptor is manually freed.
 */
