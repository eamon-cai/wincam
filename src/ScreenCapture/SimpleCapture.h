#pragma once
#include "Timer.h"
#include <mutex>

class SimpleCapture
{
public:
    SimpleCapture();
    ~SimpleCapture() { Close(); }

    void StartCapture(
        winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice const& device,
        winrt::Windows::Graphics::Capture::GraphicsCaptureItem const& item,
        winrt::Windows::Graphics::DirectX::DirectXPixelFormat pixelFormat,
        RECT bounds,
        bool captureCursor);

    bool WaitForFirstFrame(int timeout);

    // When copying GPU to CPU data the row pitch can be 64 or 128 bit aligned, which can mean
    // you need to pass in a slightly bigger buffer. This ensures ReadNextFrame can do a single
    // optimized memcpy operation but it also means it is up to you to crop the final image
    // to remove that extra data on the right side of each row.
    RECT GetCaptureBounds() { return m_captureBounds; }
    void Close();

    unsigned long long ReadNextFrame(char* buffer, unsigned int size);

private:
    void OnFrameArrived(
        winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool const& sender,
        winrt::Windows::Foundation::IInspectable const& args);

private:
    void ReadPixels(ID3D11Texture2D* buffer);

    winrt::Windows::Graphics::Capture::GraphicsCaptureItem m_item{ nullptr };
    winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool m_framePool{ nullptr };
    winrt::Windows::Graphics::Capture::GraphicsCaptureSession m_session{ nullptr };
    winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice m_device{ nullptr };
    winrt::com_ptr<ID3D11Device> m_d3dDevice{ nullptr };
    winrt::com_ptr<ID3D11DeviceContext> m_d3dContext{ nullptr };
    winrt::Windows::Graphics::DirectX::DirectXPixelFormat m_pixelFormat = winrt::Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized;

    bool m_closed = false;
    winrt::event_token m_frameArrivedToken;
    util::Timer m_timer;
    RECT m_bounds = { 0 };
    RECT m_croppedBounds = { 0 };;
    RECT m_captureBounds = { 0 };
    unsigned long long m_frameId = 0;
    unsigned long long m_frameTime = 0;
    char* m_buffer = nullptr;
    unsigned int m_size = 0;
    HANDLE m_event = NULL;
    bool m_saveBitmap = false;
};