#include <Windows.h>
#include <algorithm>
#include <d3d11.h>
#include <d3dcompiler.h>
#include <dxgi.h>
#include <wrl/client.h>
#include <atomic>
#include <chrono>
#include <cstdint>
#include <mutex>
#include <string>
#include <thread>

using Microsoft::WRL::ComPtr;

namespace
{
    struct NativeGpuMetrics
    {
        double elapsedSeconds;
        double framesPerSecond;
        double frameTimeMs;
        double progressPercent;
        std::uint64_t allocatedVramBytes;
        int errors;
        int isRunning;
    };

    std::atomic<bool> running{ false };
    std::atomic<bool> stopRequested{ false };
    std::thread worker;
    std::mutex metricsMutex;
    std::mutex errorMutex;
    NativeGpuMetrics metrics{};
    std::wstring lastError;

    void SetError(const std::wstring& value)
    {
        std::scoped_lock lock(errorMutex);
        lastError = value;
    }

    std::wstring HResultMessage(const wchar_t* operation, HRESULT result)
    {
        wchar_t buffer[160]{};
        swprintf_s(buffer, L"%s falhou (HRESULT 0x%08X).", operation, static_cast<unsigned int>(result));
        return buffer;
    }

    constexpr char shaderSource[] = R"(
RWStructuredBuffer<float4> Data : register(u0);
cbuffer StressConstants : register(b0) { uint ElementCount; uint ElementOffset; uint Seed; uint Iterations; };
[numthreads(256, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    uint index = ElementOffset + id.x;
    if (index >= ElementCount) return;
    float4 value = Data[index] + float4(index * 0.000001f, Seed * 0.00001f, 0.37f, 1.0f);
    [loop] for (uint i = 0; i < Iterations; ++i)
    {
        value = sin(value * 1.00013f + 0.17f) * cos(value.yzwx * 0.99987f + 0.31f) + 1.001f;
        value = mad(value, value.wxyz + 0.001f, 0.0001f);
    }
    Data[index] = value;
})";

    void RunStress(double durationSeconds, int targetFps, double vramLimitPercent, int qualityLevel)
    {
        (void)qualityLevel;
        CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        {   // All local variables at top so no initialization is skipped
            ComPtr<ID3D11Device> device;
            ComPtr<ID3D11DeviceContext> context;
            ComPtr<IDXGIDevice> dxgiDevice;
            ComPtr<IDXGIAdapter> adapter;
            ComPtr<ID3D11Buffer> buffer;
            ComPtr<ID3D11UnorderedAccessView> view;
            ComPtr<ID3DBlob> shaderBlob, compilationErrors;
            ComPtr<ID3D11ComputeShader> shader;
            ComPtr<ID3D11Buffer> constantsBuffer;
            struct Constants { UINT count; UINT offset; UINT seed; UINT iterations; };
            DXGI_ADAPTER_DESC adapterDescription{};
            D3D_FEATURE_LEVEL level{};
            D3D11_BUFFER_DESC bufferDescription{};
            D3D11_UNORDERED_ACCESS_VIEW_DESC viewDescription{};
            D3D11_BUFFER_DESC constantsDescription{};
            UINT elementCount = 0, activeElements = 0;
            HRESULT result = S_OK;
            bool initialized = false;
        const D3D_FEATURE_LEVEL levels[] = { D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL_11_0 };
        result = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, levels, ARRAYSIZE(levels), D3D11_SDK_VERSION, &device, &level, &context);
        if (FAILED(result))
        {
            SetError(HResultMessage(L"D3D11CreateDevice", result));
            std::scoped_lock lock(metricsMutex); metrics.errors++; metrics.isRunning = 0; running = false;
        }
        else
        {
            initialized = true;
            device.As(&dxgiDevice);
            if (dxgiDevice && SUCCEEDED(dxgiDevice->GetAdapter(&adapter)))
                adapter->GetDesc(&adapterDescription);

            const auto dedicatedBytes = adapterDescription.DedicatedVideoMemory > 0 ? adapterDescription.DedicatedVideoMemory : 256ull * 1024ull * 1024ull;
            const auto requestedBytes = static_cast<std::uint64_t>(dedicatedBytes * (std::clamp(vramLimitPercent, 1.0, 50.0) / 100.0));
            const auto allocationBytes = static_cast<UINT>(std::clamp<std::uint64_t>(requestedBytes, 64ull * 1024ull * 1024ull, 256ull * 1024ull * 1024ull));
            elementCount = allocationBytes / 16;

            bufferDescription.ByteWidth = allocationBytes;
            bufferDescription.Usage = D3D11_USAGE_DEFAULT;
            bufferDescription.BindFlags = D3D11_BIND_UNORDERED_ACCESS;
            bufferDescription.MiscFlags = D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;
            bufferDescription.StructureByteStride = 16;
            result = device->CreateBuffer(&bufferDescription, nullptr, &buffer);

            if (SUCCEEDED(result))
            {
                viewDescription.ViewDimension = D3D11_UAV_DIMENSION_BUFFER;
                viewDescription.Format = DXGI_FORMAT_UNKNOWN;
                viewDescription.Buffer.NumElements = elementCount;
                result = device->CreateUnorderedAccessView(buffer.Get(), &viewDescription, &view);
            }

            if (SUCCEEDED(result)) result = D3DCompile(shaderSource, sizeof(shaderSource), nullptr, nullptr, nullptr, "main", "cs_5_0", D3DCOMPILE_OPTIMIZATION_LEVEL3, 0, &shaderBlob, &compilationErrors);
            if (SUCCEEDED(result)) result = device->CreateComputeShader(shaderBlob->GetBufferPointer(), shaderBlob->GetBufferSize(), nullptr, &shader);

            if (SUCCEEDED(result))
            {
                constantsDescription.ByteWidth = sizeof(Constants);
                constantsDescription.Usage = D3D11_USAGE_DYNAMIC;
                constantsDescription.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
                constantsDescription.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
                result = device->CreateBuffer(&constantsDescription, nullptr, &constantsBuffer);
            }

            if (FAILED(result))
            {
                SetError(HResultMessage(L"Falha na inicialização do stress test", result));
                std::scoped_lock lock(metricsMutex); metrics.errors++; metrics.isRunning = 0; running = false;
                initialized = false;
            }
        }

        if (initialized)
        {
            context->CSSetShader(shader.Get(), nullptr, 0);
            ID3D11UnorderedAccessView* views[] = { view.Get() };
            context->CSSetUnorderedAccessViews(0, 1, views, nullptr);
            ID3D11Buffer* constantBuffers[] = { constantsBuffer.Get() };
            context->CSSetConstantBuffers(0, 1, constantBuffers);

            const auto started = std::chrono::steady_clock::now();
            auto sampleStarted = started;
            std::uint64_t totalDispatches = 0;
            std::uint64_t sampleDispatches = 0;
            // Dispatch em lotes pequenos (64K elementos) para que cada Dispatch
            // complete rápido e o loop possa responder imediatamente ao Cancel.
            // O buffer total (256 MB) permanece alocado na VRAM.
            constexpr UINT batchSize = 64u * 1024u;
            UINT elementOffset = 0;
        while (!stopRequested.load(std::memory_order_relaxed))
        {
            const auto frameStarted = std::chrono::steady_clock::now();
            activeElements = (std::min)(batchSize, elementCount - elementOffset);
            {
                D3D11_MAPPED_SUBRESOURCE mapped{};
                result = context->Map(constantsBuffer.Get(), 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped);
                if (FAILED(result)) break;
                *static_cast<Constants*>(mapped.pData) = { elementCount, elementOffset, static_cast<UINT>(totalDispatches), 128 };
                context->Unmap(constantsBuffer.Get(), 0);
            }
            context->Dispatch((activeElements + 255) / 256, 1, 1);
            {
                ID3D11UnorderedAccessView* nullUAV[] = { nullptr };
                context->CSSetUnorderedAccessViews(0, 1, nullUAV, nullptr);
            }
            elementOffset = elementOffset + activeElements >= elementCount ? 0 : elementOffset + activeElements;
            ++totalDispatches;
            ++sampleDispatches;

            const auto now = std::chrono::steady_clock::now();
            const double elapsed = std::chrono::duration<double>(now - started).count();
            const double sampleElapsed = std::chrono::duration<double>(now - sampleStarted).count();
            if (sampleElapsed >= 0.25)
            {
                const double fps = sampleDispatches / sampleElapsed;
                std::scoped_lock lock(metricsMutex);
                metrics.elapsedSeconds = elapsed;
                metrics.framesPerSecond = fps;
                metrics.frameTimeMs = fps > 0 ? 1000.0 / fps : 0;
                metrics.progressPercent = durationSeconds > 0 ? std::clamp(elapsed / durationSeconds * 100.0, 0.0, 100.0) : 0;
                metrics.allocatedVramBytes = static_cast<std::uint64_t>(elementCount * 16);
                metrics.isRunning = 1;
                sampleStarted = now;
                sampleDispatches = 0;
            }
            if (durationSeconds > 0 && elapsed >= durationSeconds) break;
            if (targetFps > 0)
            {
                const auto targetDuration = std::chrono::duration<double>(1.0 / targetFps);
                const auto spent = now - frameStarted;
                if (spent < targetDuration) std::this_thread::sleep_for(targetDuration - spent);
            }
            result = device->GetDeviceRemovedReason();
            if (FAILED(result)) break;
        }

        if (FAILED(result))
        {
            SetError(HResultMessage(L"O driver removeu o dispositivo gráfico", result));
            std::scoped_lock lock(metricsMutex); metrics.errors++;
        }
        } // if initialized

        if (context) { context->ClearState(); context->Flush(); }
        running = false;
        {   std::scoped_lock lock(metricsMutex);
            metrics.progressPercent = stopRequested ? metrics.progressPercent : 100.0;
            metrics.isRunning = 0;
        }
        } // D3D ComPtrs destroyed here
        CoUninitialize();
    }
}

extern "C" __declspec(dllexport) int __cdecl EmeGpu_IsAvailable()
{
    ComPtr<ID3D11Device> device; ComPtr<ID3D11DeviceContext> context; D3D_FEATURE_LEVEL level{};
    return SUCCEEDED(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION, &device, &level, &context));
}

extern "C" __declspec(dllexport) int __cdecl EmeGpu_Start(double durationSeconds, int width, int height, int targetFps, double vramLimitPercent, int qualityLevel)
{
    (void)width; (void)height; (void)qualityLevel;
    if (running.exchange(true)) {
        // Worker anterior ainda finalizando — aguarda até 500ms
        for (int retry = 0; retry < 50; ++retry) {
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
            if (!running.exchange(true)) goto acquired;
        }
        running = false;
        return 0;
    }
acquired:
    if (worker.joinable()) worker.join();
    stopRequested = false;
    { std::scoped_lock lock(metricsMutex); metrics = {}; metrics.isRunning = 1; }
    SetError(L"");
    worker = std::thread(RunStress, durationSeconds, targetFps, vramLimitPercent, qualityLevel);
    return 1;
}

extern "C" __declspec(dllexport) void __cdecl EmeGpu_Stop()
{
    stopRequested = true;
}

extern "C" __declspec(dllexport) void __cdecl EmeGpu_GetMetrics(NativeGpuMetrics* output)
{
    if (!output) return;
    std::scoped_lock lock(metricsMutex); *output = metrics;
}

extern "C" __declspec(dllexport) int __cdecl EmeGpu_GetLastError(wchar_t* buffer, int capacity)
{
    if (!buffer || capacity <= 0) return 0;
    std::scoped_lock lock(errorMutex);
    wcsncpy_s(buffer, capacity, lastError.c_str(), _TRUNCATE);
    return static_cast<int>(lastError.size());
}

extern "C" __declspec(dllexport) void __cdecl EmeGpu_Shutdown()
{
    stopRequested = true;
    if (worker.joinable()) worker.join();
}

// ── VRAM Test ──────────────────────────────────────────────────────────────

struct NativeVramMetrics
{
    double elapsedSeconds;
    double progressPercent;
    std::uint64_t bytesTested;
    std::uint64_t totalBytes;
    int errors;
    int isRunning;
};

static std::atomic<bool> vramRunning{ false };
static std::atomic<bool> vramStopRequested{ false };
static std::thread vramWorker;
static std::mutex vramMetricsMutex;
static NativeVramMetrics vramMetrics{};

static void FillPattern(std::uint32_t* data, UINT elementCount, std::uint32_t pattern)
{
    for (UINT i = 0; i < elementCount; ++i)
    {
        data[i * 4 + 0] = i ^ pattern;
        data[i * 4 + 1] = i + pattern;
        data[i * 4 + 2] = i * pattern;
        data[i * 4 + 3] = i | pattern;
    }
}

static bool VerifyPattern(const std::uint32_t* data, UINT elementCount, std::uint32_t pattern, UINT first, UINT count, int& errors)
{
    errors = 0;
    for (UINT i = first; i < first + count && i < elementCount; ++i)
    {
        if (data[i * 4 + 0] != (i ^ pattern) ||
            data[i * 4 + 1] != (i + pattern) ||
            data[i * 4 + 2] != (i * pattern) ||
            data[i * 4 + 3] != (i | pattern))
        {
            ++errors;
            if (errors >= 100) return false;
        }
    }
    return errors == 0;
}

static void RunVramTest()
{
    CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    HRESULT result = S_OK;
    {   ComPtr<ID3D11Device> device;
        ComPtr<ID3D11DeviceContext> context;
        ComPtr<IDXGIDevice> dxgiDevice;
        ComPtr<IDXGIAdapter> adapter;
        ComPtr<ID3D11Buffer> vramBuffer;
        ComPtr<ID3D11Buffer> stagingBuffer;
        bool initialized = false;
        DXGI_ADAPTER_DESC adapterDesc{};
        D3D_FEATURE_LEVEL level{};
        UINT elementCount = 0;
        std::uint64_t dedicatedBytes = 0;
        UINT allocationBytes = 0;
        D3D11_BUFFER_DESC bufDesc{};
        std::uint64_t totalErrors = 0;
        std::uint64_t cumulativeTested = 0;
        std::uint64_t totalBytes = 0;
        std::unique_ptr<std::uint32_t[]> cpuBuf;

    { constexpr D3D_FEATURE_LEVEL levels[] = { D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL_11_0 };
      result = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, levels, ARRAYSIZE(levels), D3D11_SDK_VERSION, &device, &level, &context); }
    if (FAILED(result)) goto vramCleanup;
    initialized = true;
    device.As(&dxgiDevice);
    if (dxgiDevice && SUCCEEDED(dxgiDevice->GetAdapter(&adapter)))
        adapter->GetDesc(&adapterDesc);

    dedicatedBytes = adapterDesc.DedicatedVideoMemory > 0 ? adapterDesc.DedicatedVideoMemory : 2048ull * 1024ull * 1024ull;
    allocationBytes = static_cast<UINT>(std::clamp<std::uint64_t>(dedicatedBytes * 90 / 100, 256ull * 1024ull * 1024ull, 2048ull * 1024ull * 1024ull));
    elementCount = allocationBytes / 16;

    // VRAM-side buffer (DEFAULT = GPU memory)
    bufDesc = {};
    bufDesc.ByteWidth = allocationBytes;
    bufDesc.Usage = D3D11_USAGE_DEFAULT;
    bufDesc.BindFlags = 0;
    bufDesc.MiscFlags = 0;
    result = device->CreateBuffer(&bufDesc, nullptr, &vramBuffer);
    if (FAILED(result)) goto vramCleanup;

    // Staging buffer for CPU read/write
    bufDesc.Usage = D3D11_USAGE_STAGING;
    bufDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ | D3D11_CPU_ACCESS_WRITE;
    result = device->CreateBuffer(&bufDesc, nullptr, &stagingBuffer);
    if (FAILED(result)) goto vramCleanup;

    // CPU-side buffer for pattern generation
    cpuBuf = std::make_unique<std::uint32_t[]>(elementCount * 4);

    totalBytes = static_cast<std::uint64_t>(allocationBytes) * 5;
    { std::scoped_lock lock(vramMetricsMutex);
      vramMetrics.totalBytes = totalBytes;
      vramMetrics.isRunning = 1; }

    { constexpr std::uint32_t patterns[] = { 0xAAAAAAAA, 0x55555555, 0xFFFFFFFF, 0x00000000, 0x6996A5A5 };
      constexpr int patternCount = 5;
    for (int run = 0; !vramStopRequested.load(std::memory_order_relaxed); ++run)
    {
        for (int p = 0; p < patternCount && !vramStopRequested.load(std::memory_order_relaxed); ++p)
        {
            const std::uint32_t seed = patterns[p];

            // 1. Fill CPU buffer with pattern
            FillPattern(cpuBuf.get(), elementCount, seed);

            // 2. Upload CPU → staging → VRAM
            D3D11_MAPPED_SUBRESOURCE mapped{};
            result = context->Map(stagingBuffer.Get(), 0, D3D11_MAP_WRITE, 0, &mapped);
            if (FAILED(result)) { totalErrors++; break; }
            memcpy(mapped.pData, cpuBuf.get(), allocationBytes);
            context->Unmap(stagingBuffer.Get(), 0);
            context->CopyResource(vramBuffer.Get(), stagingBuffer.Get());

            // 3. Read back VRAM → staging → CPU
            context->CopyResource(stagingBuffer.Get(), vramBuffer.Get());
            result = context->Map(stagingBuffer.Get(), 0, D3D11_MAP_READ, 0, &mapped);
            if (FAILED(result)) { totalErrors++; break; }
            memcpy(cpuBuf.get(), mapped.pData, allocationBytes);
            context->Unmap(stagingBuffer.Get(), 0);

            // 4. Verify in batches, report progress
            constexpr UINT verifyBatch = 64u * 1024u;
            int patternErrors = 0;
            for (UINT i = 0; i < elementCount && !vramStopRequested.load(std::memory_order_relaxed); i += verifyBatch)
            {
                UINT end = (std::min)(i + verifyBatch, elementCount);
                int batchErrors = 0;
                if (!VerifyPattern(cpuBuf.get(), elementCount, seed, i, end - i, batchErrors) || batchErrors > 0)
                    patternErrors += batchErrors;
                cumulativeTested += (end - i) * 16;
                { std::scoped_lock lock(vramMetricsMutex);
                  vramMetrics.bytesTested = cumulativeTested;
                  vramMetrics.errors = static_cast<int>(totalErrors + patternErrors);
                  vramMetrics.progressPercent = std::clamp(cumulativeTested * 100.0 / totalBytes, 0.0, 100.0); }
            }
            totalErrors += patternErrors;
        }
    } }

    { std::scoped_lock lock(vramMetricsMutex);
      vramMetrics.progressPercent = 100.0; }

vramCleanup:
    { std::scoped_lock lock(vramMetricsMutex);
      vramMetrics.isRunning = 0;
      if (totalErrors > 0) vramMetrics.errors = static_cast<int>(totalErrors);
      else if (FAILED(result)) vramMetrics.errors++; }
    if (context) { context->ClearState(); context->Flush(); }
    vramRunning = false;
    } // D3D ComPtrs + unique_ptr destroyed here
    CoUninitialize();
}

extern "C" __declspec(dllexport) int __cdecl EmeGpu_VramTest_Start()
{
    if (vramRunning.exchange(true))
    {
        for (int retry = 0; retry < 50; ++retry)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
            if (!vramRunning.exchange(true)) goto vramAcquired;
        }
        vramRunning = false;
        return 0;
    }
vramAcquired:
    if (vramWorker.joinable()) vramWorker.join();
    vramStopRequested = false;
    { std::scoped_lock lock(vramMetricsMutex); vramMetrics = {}; vramMetrics.isRunning = 1; }
    vramWorker = std::thread(RunVramTest);
    return 1;
}

extern "C" __declspec(dllexport) void __cdecl EmeGpu_VramTest_Stop()
{
    vramStopRequested = true;
}

extern "C" __declspec(dllexport) void __cdecl EmeGpu_VramTest_GetMetrics(NativeVramMetrics* output)
{
    if (!output) return;
    std::scoped_lock lock(vramMetricsMutex); *output = vramMetrics;
}

BOOL APIENTRY DllMain(HMODULE, DWORD, LPVOID) { return TRUE; }
