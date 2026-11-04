using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TrophyPop;

/// <summary>
/// Capturas no estilo PS5:
/// - Troféus comuns (Bronze, Prata e Ouro): salva apenas um print (.jpg) leve da tela no auge do pop-up.
/// - Troféu de Platina: grava um clipe de vídeo (.mp4 H.264) de alguns segundos em processo separado.
/// </summary>
public static class MediaCapture
{
    const string StopEventName = @"Local\TrophyPop.StopClip";

    public static string Dir => Path.Combine(AppConfig.Dir, "capturas");

    public static string GameDir(int appId) =>
        Path.Combine(Dir, appId > 0 ? appId.ToString() : "teste");

    public static string ScreenshotPath(int appId, string key) =>
        Path.Combine(GameDir(appId), Sanitize(key) + ".jpg");

    public static string PlatinumClipPath(int appId) =>
        Path.Combine(GameDir(appId), "Platina.mp4");

    public static bool HasScreenshot(int appId, string key) =>
        !string.IsNullOrWhiteSpace(key) && File.Exists(ScreenshotPath(appId, key));

    public static bool HasPlatinumClip(int appId) =>
        File.Exists(PlatinumClipPath(appId));

    static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "trofeu";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    public static void OpenFolder(int? appId = null)
    {
        try
        {
            var folder = appId is > 0 && Directory.Exists(GameDir(appId.Value)) ? GameDir(appId.Value) : Dir;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch { }
    }

    public static void RevealFile(string path)
    {
        try
        {
            if (File.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            else
                OpenFolder();
        }
        catch { }
    }

    // -------------------------------------------------------------------------
    // Print (.jpg) para Bronze, Prata e Ouro
    // -------------------------------------------------------------------------

    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    const int DESKTOPHORZRES = 118, DESKTOPVERTZRES = 117;

    public static (int Width, int Height) GetPhysicalScreenSize()
    {
        IntPtr hdc = GetDC(IntPtr.Zero);
        try
        {
            int w = GetDeviceCaps(hdc, DESKTOPHORZRES);
            int h = GetDeviceCaps(hdc, DESKTOPVERTZRES);
            if (w > 0 && h > 0) return (w, h);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc);
        }
        return (1920, 1080);
    }

    public static void CaptureScreenJpg(string outputPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var (w, h) = GetPhysicalScreenSize();
            using var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(0, 0, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);

            var codec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/jpeg");
            var temp = outputPath + ".tmp";
            if (codec != null)
            {
                using var ep = new EncoderParameters(1);
                ep.Param[0] = new EncoderParameter(Encoder.Quality, 88L);
                bmp.Save(temp, codec, ep);
            }
            else
            {
                bmp.Save(temp, ImageFormat.Jpeg);
            }
            File.Move(temp, outputPath, overwrite: true);
        }
        catch { }
    }

    // -------------------------------------------------------------------------
    // Clipe (.mp4 H.264) para a Platina, em processo separado (--gravar-clipe)
    // -------------------------------------------------------------------------

    public static Process? StartPlatinumRecorder(int appId, int maxSeconds)
    {
        try
        {
            var outPath = PlatinumClipPath(appId);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

            // Limpa eventual sinal anterior antes de iniciar o processo gravador
            using (var ev = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName))
                ev.Reset();

            var psi = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--gravar-clipe");
            psi.ArgumentList.Add(outPath);
            psi.ArgumentList.Add(Math.Clamp(maxSeconds, 4, 30).ToString());
            return Process.Start(psi);
        }
        catch
        {
            return null;
        }
    }

    public static void StopPlatinumRecorder()
    {
        try
        {
            using var ev = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName);
            ev.Set();
        }
        catch { }
    }

    /// <summary>
    /// Roda dentro do subprocesso "--gravar-clipe": grava a tela em H.264 (.mp4) via MediaFoundation
    /// até receber o sinal de parada (quando o pop-up da Platina fecha) ou atingir o tempo máximo.
    /// </summary>
    public static void RunClipRecorderProcess(string outputPath, int maxSeconds)
    {
        Native.SetBackgroundMode(true);
        using var stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName);
        RecordScreenMp4(outputPath, Math.Clamp(maxSeconds, 3, 30), stopEvent);
    }

    public static void RecordScreenMp4(string outputPath, int maxSeconds, WaitHandle? stopSignal = null)
    {
        const int Fps = 24;
        const int Bitrate = 2_800_000;
        const long FrameDuration100Ns = 10_000_000L / Fps;

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tempPath = outputPath + ".tmp.mp4";
        try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }

        var (srcW, srcH) = GetPhysicalScreenSize();
        double scale = Math.Min(1.0, Math.Min(1280.0 / srcW, 720.0 / srcH));
        int outW = Math.Max(320, ((int)Math.Round(srcW * scale)) & ~1);
        int outH = Math.Max(240, ((int)Math.Round(srcH * scale)) & ~1);

        int hr = MFStartup(MF_VERSION, 0);
        if (hr < 0) return;

        IMFSinkWriter? writer = null;
        try
        {
            Marshal.ThrowExceptionForHR(MFCreateSinkWriterFromURL(tempPath, IntPtr.Zero, IntPtr.Zero, out writer));

            // 1. Formato de saída: H.264 MP4
            Marshal.ThrowExceptionForHR(MFCreateMediaType(out var mediaOut));
            try
            {
                var attrOut = (IMFAttributes)Marshal.GetObjectForIUnknown(mediaOut);
                Marshal.ThrowExceptionForHR(attrOut.SetGUID(in MF_MT_MAJOR_TYPE, in MFMediaType_Video));
                Marshal.ThrowExceptionForHR(attrOut.SetGUID(in MF_MT_SUBTYPE, in MFVideoFormat_H264));
                Marshal.ThrowExceptionForHR(attrOut.SetUINT32(in MF_MT_AVG_BITRATE, Bitrate));
                Marshal.ThrowExceptionForHR(attrOut.SetUINT32(in MF_MT_INTERLACE_MODE, 2)); // Progressive
                Marshal.ThrowExceptionForHR(attrOut.SetUINT64(in MF_MT_FRAME_SIZE, Pack64((uint)outW, (uint)outH)));
                Marshal.ThrowExceptionForHR(attrOut.SetUINT64(in MF_MT_FRAME_RATE, Pack64(Fps, 1)));
                Marshal.ThrowExceptionForHR(attrOut.SetUINT64(in MF_MT_PIXEL_ASPECT_RATIO, Pack64(1, 1)));
                Marshal.ThrowExceptionForHR(writer.AddStream(mediaOut, out uint streamIndex));

                // 2. Formato de entrada: RGB32
                Marshal.ThrowExceptionForHR(MFCreateMediaType(out var mediaIn));
                try
                {
                    var attrIn = (IMFAttributes)Marshal.GetObjectForIUnknown(mediaIn);
                    Marshal.ThrowExceptionForHR(attrIn.SetGUID(in MF_MT_MAJOR_TYPE, in MFMediaType_Video));
                    Marshal.ThrowExceptionForHR(attrIn.SetGUID(in MF_MT_SUBTYPE, in MFVideoFormat_RGB32));
                    Marshal.ThrowExceptionForHR(attrIn.SetUINT32(in MF_MT_INTERLACE_MODE, 2));
                    Marshal.ThrowExceptionForHR(attrIn.SetUINT64(in MF_MT_FRAME_SIZE, Pack64((uint)outW, (uint)outH)));
                    Marshal.ThrowExceptionForHR(attrIn.SetUINT64(in MF_MT_FRAME_RATE, Pack64(Fps, 1)));
                    Marshal.ThrowExceptionForHR(attrIn.SetUINT64(in MF_MT_PIXEL_ASPECT_RATIO, Pack64(1, 1)));
                    Marshal.ThrowExceptionForHR(writer.SetInputMediaType(streamIndex, mediaIn, IntPtr.Zero));
                }
                finally
                {
                    Marshal.Release(mediaIn);
                }

                Marshal.ThrowExceptionForHR(writer.BeginWriting());

                using var screenBmp = new Bitmap(srcW, srcH, PixelFormat.Format32bppRgb);
                using var gScreen = Graphics.FromImage(screenBmp);
                using var frameBmp = new Bitmap(outW, outH, PixelFormat.Format32bppRgb);
                using var gFrame = Graphics.FromImage(frameBmp);
                gFrame.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;

                int rowBytes = outW * 4;
                uint bufferSize = (uint)(rowBytes * outH);
                int maxFrames = Math.Max(Fps * 2, maxSeconds * Fps);
                var sw = Stopwatch.StartNew();
                long timestamp100Ns = 0;

                for (int frame = 0; frame < maxFrames; frame++)
                {
                    if (stopSignal != null && frame >= Fps * 2 && stopSignal.WaitOne(0))
                        break;

                    long nextFrameMs = (frame * 1000L) / Fps;
                    long waitMs = nextFrameMs - sw.ElapsedMilliseconds;
                    if (waitMs > 1)
                    {
                        if (stopSignal != null && frame >= Fps * 2 && stopSignal.WaitOne((int)waitMs))
                            break;
                        else if (stopSignal == null)
                            Thread.Sleep((int)waitMs);
                    }

                    try
                    {
                        gScreen.CopyFromScreen(0, 0, 0, 0, new Size(srcW, srcH), CopyPixelOperation.SourceCopy);
                    }
                    catch
                    {
                        // Se o UAC ou troca de tela bloquear um quadro isolado, mantém o quadro anterior
                    }
                    gFrame.DrawImage(screenBmp, 0, 0, outW, outH);

                    Marshal.ThrowExceptionForHR(MFCreateMemoryBuffer(bufferSize, out var mediaBuffer));
                    try
                    {
                        Marshal.ThrowExceptionForHR(mediaBuffer.Lock(out IntPtr dstPtr, out _, out _));
                        var bits = frameBmp.LockBits(new Rectangle(0, 0, outW, outH), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
                        try
                        {
                            // O encoder RGB32 do MediaFoundation espera as linhas de baixo para cima (bottom-up DIB)
                            unsafe
                            {
                                for (int y = 0; y < outH; y++)
                                {
                                    void* srcRow = (byte*)bits.Scan0 + (outH - 1 - y) * bits.Stride;
                                    void* dstRow = (byte*)dstPtr + y * rowBytes;
                                    Buffer.MemoryCopy(srcRow, dstRow, rowBytes, rowBytes);
                                }
                            }
                        }
                        finally
                        {
                            frameBmp.UnlockBits(bits);
                            mediaBuffer.Unlock();
                        }
                        Marshal.ThrowExceptionForHR(mediaBuffer.SetCurrentLength(bufferSize));

                        Marshal.ThrowExceptionForHR(MFCreateSample(out var sample));
                        try
                        {
                            Marshal.ThrowExceptionForHR(sample.AddBuffer(mediaBuffer));
                            Marshal.ThrowExceptionForHR(sample.SetSampleTime(timestamp100Ns));
                            Marshal.ThrowExceptionForHR(sample.SetSampleDuration(FrameDuration100Ns));
                            Marshal.ThrowExceptionForHR(writer.WriteSample(streamIndex, sample));
                            timestamp100Ns += FrameDuration100Ns;
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(sample);
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(mediaBuffer);
                    }
                }

                Marshal.ThrowExceptionForHR(writer.Finalize_());
            }
            finally
            {
                Marshal.Release(mediaOut);
            }

            if (File.Exists(tempPath))
                File.Move(tempPath, outputPath, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
        finally
        {
            if (writer != null) Marshal.ReleaseComObject(writer);
            MFShutdown();
        }
    }

    static ulong Pack64(uint hi, uint lo) => ((ulong)hi << 32) | lo;

    // -------------------------------------------------------------------------
    // Interop nativo com o Windows MediaFoundation (mfplat.dll / mfreadwrite.dll)
    // -------------------------------------------------------------------------

    const int MF_VERSION = 0x00020070;

    static readonly Guid MF_MT_MAJOR_TYPE = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    static readonly Guid MF_MT_SUBTYPE = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    static readonly Guid MF_MT_AVG_BITRATE = new("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
    static readonly Guid MF_MT_INTERLACE_MODE = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    static readonly Guid MF_MT_FRAME_SIZE = new("1652c33d-d6b2-4012-b834-72030849a37d");
    static readonly Guid MF_MT_FRAME_RATE = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    static readonly Guid MF_MT_PIXEL_ASPECT_RATIO = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");

    static readonly Guid MFMediaType_Video = new("73646976-0000-0010-8000-00aa00389b71");
    static readonly Guid MFVideoFormat_H264 = new("34363248-0000-0010-8000-00aa00389b71");
    static readonly Guid MFVideoFormat_RGB32 = new("00000016-0000-0010-8000-00aa00389b71");

    [DllImport("mfplat.dll")] static extern int MFStartup(int version, int dwFlags);
    [DllImport("mfplat.dll")] static extern int MFShutdown();
    [DllImport("mfplat.dll")] static extern int MFCreateMediaType(out IntPtr ppMFType);
    [DllImport("mfplat.dll")] static extern int MFCreateMemoryBuffer(uint cbMaxLength, out IMFMediaBuffer ppBuffer);
    [DllImport("mfplat.dll")] static extern int MFCreateSample(out IMFSample ppIMFSample);

    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode)]
    static extern int MFCreateSinkWriterFromURL(string pwszOutputURL, IntPtr pByteStream, IntPtr pAttributes, out IMFSinkWriter ppSinkWriter);

    [ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFAttributes
    {
        [PreserveSig] int GetItem(in Guid guidKey, IntPtr pValue);
        [PreserveSig] int GetItemType(in Guid guidKey, out int pType);
        [PreserveSig] int CompareItem(in Guid guidKey, IntPtr value, out int pbResult);
        [PreserveSig] int Compare(IntPtr pTheirs, int matchType, out int pbResult);
        [PreserveSig] int GetUINT32(in Guid guidKey, out uint punValue);
        [PreserveSig] int GetUINT64(in Guid guidKey, out ulong punValue);
        [PreserveSig] int GetDouble(in Guid guidKey, out double pfValue);
        [PreserveSig] int GetGUID(in Guid guidKey, out Guid pguidValue);
        [PreserveSig] int GetStringLength(in Guid guidKey, out uint pcchLength);
        [PreserveSig] int GetString(in Guid guidKey, IntPtr pwszValue, uint cchBufSize, IntPtr pcchLength);
        [PreserveSig] int GetAllocatedString(in Guid guidKey, out IntPtr ppwszValue, out uint pcchLength);
        [PreserveSig] int GetBlobSize(in Guid guidKey, out uint pcbBlobSize);
        [PreserveSig] int GetBlob(in Guid guidKey, IntPtr pBuf, uint cbBufSize, IntPtr pcbBlobSize);
        [PreserveSig] int GetAllocatedBlob(in Guid guidKey, out IntPtr ip, out uint pcbSize);
        [PreserveSig] int GetUnknown(in Guid guidKey, in Guid riid, out IntPtr ppv);
        [PreserveSig] int SetItem(in Guid guidKey, IntPtr value);
        [PreserveSig] int DeleteItem(in Guid guidKey);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32(in Guid guidKey, uint unValue);
        [PreserveSig] int SetUINT64(in Guid guidKey, ulong unValue);
        [PreserveSig] int SetDouble(in Guid guidKey, double fValue);
        [PreserveSig] int SetGUID(in Guid guidKey, in Guid guidValue);
        [PreserveSig] int SetString(in Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        [PreserveSig] int SetBlob(in Guid guidKey, IntPtr pBuf, uint cbBufSize);
        [PreserveSig] int SetUnknown(in Guid guidKey, IntPtr pUnknown);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint pcItems);
        [PreserveSig] int GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
        [PreserveSig] int CopyAllItems(IntPtr pDest);
    }

    [ComImport, Guid("045FA593-8799-42b8-BC8D-8968C6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFMediaBuffer
    {
        [PreserveSig] int Lock(out IntPtr ppbBuffer, out uint pcbMaxLength, out uint pcbCurrentLength);
        [PreserveSig] int Unlock();
        [PreserveSig] int GetCurrentLength(out uint pcbCurrentLength);
        [PreserveSig] int SetCurrentLength(uint cbCurrentLength);
        [PreserveSig] int GetMaxLength(out uint pcbMaxLength);
    }

    [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFSample
    {
        // IMFAttributes (30 métodos base)
        [PreserveSig] int GetItem(in Guid guidKey, IntPtr pValue);
        [PreserveSig] int GetItemType(in Guid guidKey, out int pType);
        [PreserveSig] int CompareItem(in Guid guidKey, IntPtr value, out int pbResult);
        [PreserveSig] int Compare(IntPtr pTheirs, int matchType, out int pbResult);
        [PreserveSig] int GetUINT32(in Guid guidKey, out uint punValue);
        [PreserveSig] int GetUINT64(in Guid guidKey, out ulong punValue);
        [PreserveSig] int GetDouble(in Guid guidKey, out double pfValue);
        [PreserveSig] int GetGUID(in Guid guidKey, out Guid pguidValue);
        [PreserveSig] int GetStringLength(in Guid guidKey, out uint pcchLength);
        [PreserveSig] int GetString(in Guid guidKey, IntPtr pwszValue, uint cchBufSize, IntPtr pcchLength);
        [PreserveSig] int GetAllocatedString(in Guid guidKey, out IntPtr ppwszValue, out uint pcchLength);
        [PreserveSig] int GetBlobSize(in Guid guidKey, out uint pcbBlobSize);
        [PreserveSig] int GetBlob(in Guid guidKey, IntPtr pBuf, uint cbBufSize, IntPtr pcbBlobSize);
        [PreserveSig] int GetAllocatedBlob(in Guid guidKey, out IntPtr ip, out uint pcbSize);
        [PreserveSig] int GetUnknown(in Guid guidKey, in Guid riid, out IntPtr ppv);
        [PreserveSig] int SetItem(in Guid guidKey, IntPtr value);
        [PreserveSig] int DeleteItem(in Guid guidKey);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32(in Guid guidKey, uint unValue);
        [PreserveSig] int SetUINT64(in Guid guidKey, ulong unValue);
        [PreserveSig] int SetDouble(in Guid guidKey, double fValue);
        [PreserveSig] int SetGUID(in Guid guidKey, in Guid guidValue);
        [PreserveSig] int SetString(in Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        [PreserveSig] int SetBlob(in Guid guidKey, IntPtr pBuf, uint cbBufSize);
        [PreserveSig] int SetUnknown(in Guid guidKey, IntPtr pUnknown);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out uint pcItems);
        [PreserveSig] int GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
        [PreserveSig] int CopyAllItems(IntPtr pDest);

        // IMFSample
        [PreserveSig] int GetSampleFlags(out uint pdwSampleFlags);
        [PreserveSig] int SetSampleFlags(uint dwSampleFlags);
        [PreserveSig] int GetSampleTime(out long phnsSampleTime);
        [PreserveSig] int SetSampleTime(long hnsSampleTime);
        [PreserveSig] int GetSampleDuration(out long phnsSampleDuration);
        [PreserveSig] int SetSampleDuration(long hnsSampleDuration);
        [PreserveSig] int GetBufferCount(out uint pdwBufferCount);
        [PreserveSig] int GetBufferByIndex(uint dwIndex, out IMFMediaBuffer ppBuffer);
        [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
        [PreserveSig] int AddBuffer(IMFMediaBuffer pBuffer);
        [PreserveSig] int RemoveBufferByIndex(uint dwIndex);
        [PreserveSig] int RemoveAllBuffers();
        [PreserveSig] int GetTotalLength(out uint pcbTotalLength);
        [PreserveSig] int CopyToBuffer(IMFMediaBuffer pBuffer);
    }

    [ComImport, Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMFSinkWriter
    {
        [PreserveSig] int AddStream(IntPtr pTargetMediaType, out uint pdwStreamIndex);
        [PreserveSig] int SetInputMediaType(uint dwStreamIndex, IntPtr pInputMediaType, IntPtr pEncodingParameters);
        [PreserveSig] int BeginWriting();
        [PreserveSig] int WriteSample(uint dwStreamIndex, IMFSample pSample);
        [PreserveSig] int SendStreamTick(uint dwStreamIndex, long llTimestamp);
        [PreserveSig] int PlaceMarker(uint dwStreamIndex, IntPtr pvContext);
        [PreserveSig] int NotifyEndOfSegment(uint dwStreamIndex);
        [PreserveSig] int Flush(uint dwStreamIndex);
        [PreserveSig] int Finalize_();
        [PreserveSig] int GetServiceForStream(uint dwStreamIndex, in Guid guidService, in Guid riid, out IntPtr ppvObject);
        [PreserveSig] int GetStatistics(uint dwStreamIndex, IntPtr pStats);
    }
}
