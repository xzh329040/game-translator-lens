using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using GameTranslatorLens.Core;
using Rect = System.Windows.Rect;

namespace GameTranslatorLens.Ocr;

public sealed partial class OneOcrEngine : IOcrEngine, IDisposable
{
    private OcrNativeHandle? _context;
    private OcrNativeHandle? _pipeline;
    private OcrNativeHandle? _processOptions;
    private bool _ready;
    private bool _nativePathConfigured;

    public string Name => "OneOCR";
    public bool IsReady => _ready;
    public string? InitError { get; private set; }

    /// <summary>
    /// Detailed diagnostic log produced during EnsureInitializedAsync.
    /// Available even when initialization fails.
    /// </summary>
    public string? InitDiagnostics { get; private set; }

    public async Task<IReadOnlyList<OcrTextLine>> RecognizeAsync(Bitmap bitmap, string languageCode, CancellationToken cancellationToken)
    {
        using Bitmap prepared = OcrImagePreprocessor.Prepare(bitmap);
        return await RecognizeWithPrepare(bitmap, languageCode, prepared, cancellationToken);
    }

    public async Task<IReadOnlyList<OcrTextLine>> RecognizeAsync(
        Bitmap bitmap,
        string languageCode,
        CancellationToken cancellationToken,
        Func<Bitmap, Bitmap>? customPrepare)
    {
        using Bitmap prepared = customPrepare is not null ? customPrepare(bitmap) : OcrImagePreprocessor.Prepare(bitmap);
        return await RecognizeWithPrepare(bitmap, languageCode, prepared, cancellationToken);
    }

    private async Task<IReadOnlyList<OcrTextLine>> RecognizeWithPrepare(
        Bitmap bitmap,
        string languageCode,
        Bitmap prepared,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await EnsureInitializedAsync();
        if (!_ready)
        {
            return Array.Empty<OcrTextLine>();
        }
        BitmapData data = prepared.LockBits(new Rectangle(0, 0, prepared.Width, prepared.Height), ImageLockMode.ReadOnly, prepared.PixelFormat);
        long instance = 0;
        try
        {
            Img img = new()
            {
                t = 3,
                col = prepared.Width,
                row = prepared.Height,
                _unk = 0,
                step = Image.GetPixelFormatSize(prepared.PixelFormat) / 8 * prepared.Width,
                data_ptr = data.Scan0
            };

            long res = OneOcrNative.RunOcrPipeline(_pipeline!.Value, ref img, _processOptions!.Value, out instance);
            if (res != 0)
            {
                return Array.Empty<OcrTextLine>();
            }

            res = OneOcrNative.GetOcrLineCount(instance, out long lineCount);
            if (res != 0)
            {
                return Array.Empty<OcrTextLine>();
            }

            List<OcrTextLine> lines = [];
            for (long i = 0; i < lineCount; i++)
            {
                if (OneOcrNative.GetOcrLine(instance, i, out long line) != 0 || line == 0)
                {
                    continue;
                }

                if (OneOcrNative.GetOcrLineContent(line, out IntPtr contentPtr) != 0)
                {
                    continue;
                }

                string text = PtrToUtf8(contentPtr).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (OneOcrNative.GetOcrLineBoundingBox(line, out IntPtr boxPtr) == 0)
                {
                    BoundingBox box = Marshal.PtrToStructure<BoundingBox>(boxPtr);
                    double left = Math.Min(Math.Min(box.x1, box.x2), Math.Min(box.x3, box.x4));
                    double top = Math.Min(Math.Min(box.y1, box.y2), Math.Min(box.y3, box.y4));
                    double right = Math.Max(Math.Max(box.x1, box.x2), Math.Max(box.x3, box.x4));
                    double bottom = Math.Max(Math.Max(box.y1, box.y2), Math.Max(box.y3, box.y4));
                    Rect bounds = OcrImagePreprocessor.ScaleBoundsBack(new Rect(left, top, right - left, bottom - top));
                    lines.Add(new OcrTextLine(text, bounds));
                }
            }

            return lines;
        }
        finally
        {
            if (instance != 0)
            {
                _ = OneOcrNative.ReleaseOcrResult(instance);
            }

            prepared.UnlockBits(data);
        }
    }

    private Task EnsureInitializedAsync()
    {
        if (_ready)
        {
            return Task.CompletedTask;
        }

        StringBuilder log = new();
        log.AppendLine("[OCR] Start initialization...");

        ConfigureNativePath();
        try
        {
            // ── Step 1: check source files ──
            string oneOcrDir = Path.Combine(AppContext.BaseDirectory, "OneOcr");
            string dllPath = Path.Combine(oneOcrDir, "oneocr.dll");
            string onnxPath = Path.Combine(oneOcrDir, "onnxruntime.dll");
            string sourceModelPath = Path.Combine(oneOcrDir, "oneocr.onemodel");

            log.AppendLine($"[OCR] AppContext.BaseDirectory = {AppContext.BaseDirectory}");
            log.AppendLine($"[OCR] oneocr.dll   exists={File.Exists(dllPath)} size={SafeFileSize(dllPath)}");
            log.AppendLine($"[OCR] onnxruntime.dll exists={File.Exists(onnxPath)} size={SafeFileSize(onnxPath)}");
            log.AppendLine($"[OCR] oneocr.onemodel (source) exists={File.Exists(sourceModelPath)} size={SafeFileSize(sourceModelPath)}");

            if (!File.Exists(dllPath))
            {
                InitError = $"DLL 不存在: {dllPath}";
                log.AppendLine($"[OCR] FAILED: {InitError}");
                InitDiagnostics = log.ToString();
                return Task.CompletedTask;
            }

            // ── Step 2: resolve ASCII-safe path ──
            // CRITICAL: oneocr.dll's native CreateOcrPipeline cannot open files
            // whose path contains non-ASCII characters (e.g. Chinese usernames or
            // directory names).  We copy the model + DLLs to a directory guaranteed
            // to have a pure-ASCII path.
            string asciiDir = ResolveAsciiOcrDirectory();
            log.AppendLine($"[OCR] ASCII-safe directory = {asciiDir}");

            string modelPath = Path.Combine(asciiDir, "oneocr.onemodel");
            string asciiDll = Path.Combine(asciiDir, "oneocr.dll");
            string asciiOnnx = Path.Combine(asciiDir, "onnxruntime.dll");

            // Copy if any file is missing
            if (!File.Exists(asciiDll) || !File.Exists(asciiOnnx) || !File.Exists(modelPath))
            {
                log.AppendLine("[OCR] Copying files to ASCII-safe directory...");
                try
                {
                    Directory.CreateDirectory(asciiDir);
                    File.Copy(dllPath, asciiDll, overwrite: true);
                    File.Copy(onnxPath, asciiOnnx, overwrite: true);
                    File.Copy(sourceModelPath, modelPath, overwrite: true);
                    log.AppendLine("[OCR] Copy succeeded.");
                }
                catch (Exception ex)
                {
                    InitError = $"复制 OCR 文件到 {asciiDir} 失败: {ex.Message}";
                    log.AppendLine($"[OCR] FAILED: {InitError}");
                    InitDiagnostics = log.ToString();
                    return Task.CompletedTask;
                }
            }
            else
            {
                log.AppendLine("[OCR] Files already present in ASCII directory.");
            }

            log.AppendLine($"[OCR] Model path for pipeline = {modelPath}");
            log.AppendLine($"[OCR] Model exists={File.Exists(modelPath)} size={SafeFileSize(modelPath)}");

            // ── Step 3: set DLL search path to ASCII directory ──
            bool setDirOk = NativeMethods.SetDllDirectory(asciiDir);
            log.AppendLine($"[OCR] SetDllDirectory({asciiDir}) = {setDirOk}");

            // ── Step 4: CreateOcrInitOptions ──
            log.AppendLine("[OCR] Calling CreateOcrInitOptions...");
            if (OneOcrNative.CreateOcrInitOptions(out long context) != 0)
            {
                InitError = "CreateOcrInitOptions 失败";
                log.AppendLine($"[OCR] FAILED: {InitError}");
                InitDiagnostics = log.ToString();
                return Task.CompletedTask;
            }
            log.AppendLine($"[OCR] CreateOcrInitOptions OK, ctx=0x{context:X}");

            _context = new OcrNativeHandle(context, OcrNativeHandleKind.InitOptions);
            _ = OneOcrNative.OcrInitOptionsSetUseModelDelayLoad(_context.Value, 1);
            log.AppendLine("[OCR] OcrInitOptionsSetUseModelDelayLoad called.");

            // ── Step 5: CreateOcrPipeline ──
            string key = "kj)TGtrK>f]b[Piow.gU+nC@s\"\"\"\"\"\"4";
            log.AppendLine($"[OCR] Calling CreateOcrPipeline...");
            log.AppendLine($"[OCR]   modelPath = {modelPath}");
            log.AppendLine($"[OCR]   key (first 20 chars) = {key[..Math.Min(20, key.Length)]}");
            log.AppendLine($"[OCR]   ctx = 0x{_context.Value:X}");

            long pipelineResult = OneOcrNative.CreateOcrPipeline(modelPath, key, _context.Value, out long pipeline);

            if (pipelineResult != 0)
            {
                int win32err = Marshal.GetLastWin32Error();
                string win32msg = GetWin32ErrorMessage(win32err);

                // Also check whether the file is actually readable
                bool canRead = false;
                try
                {
                    using FileStream fs = File.OpenRead(modelPath);
                    canRead = fs.CanRead;
                }
                catch (Exception readEx)
                {
                    log.AppendLine($"[OCR] File readability check failed: {readEx.Message}");
                }

                InitError = $"CreateOcrPipeline 失败 code={pipelineResult} hr=0x{(uint)Marshal.GetHRForLastWin32Error():X8} " +
                            $"win32err={win32err} ({win32msg}) path={modelPath}";
                log.AppendLine($"[OCR] FAILED: {InitError}");
                log.AppendLine($"[OCR] Model file readable: {canRead}");
                log.AppendLine($"[OCR] Model file exists: {File.Exists(modelPath)}");
                log.AppendLine($"[OCR] Model file size: {SafeFileSize(modelPath)}");
                log.AppendLine($"[OCR] AppContext.BaseDirectory encoding check:");
                log.AppendLine($"[OCR]   Has non-ASCII in base dir: {HasNonAscii(AppContext.BaseDirectory)}");
                log.AppendLine($"[OCR]   Has non-ASCII in model path: {HasNonAscii(modelPath)}");
                InitDiagnostics = log.ToString();
                Dispose();
                return Task.CompletedTask;
            }

            log.AppendLine($"[OCR] CreateOcrPipeline OK, pipeline=0x{pipeline:X}");

            _pipeline = new OcrNativeHandle(pipeline, OcrNativeHandleKind.Pipeline);

            // ── Step 6: CreateOcrProcessOptions ──
            log.AppendLine("[OCR] Calling CreateOcrProcessOptions...");
            if (OneOcrNative.CreateOcrProcessOptions(out long processOptions) != 0)
            {
                InitError = "CreateOcrProcessOptions 失败";
                log.AppendLine($"[OCR] FAILED: {InitError}");
                InitDiagnostics = log.ToString();
                Dispose();
                return Task.CompletedTask;
            }

            _processOptions = new OcrNativeHandle(processOptions, OcrNativeHandleKind.ProcessOptions);
            _ = OneOcrNative.OcrProcessOptionsSetMaxRecognitionLineCount(_processOptions.Value, 80);
            _ready = true;
            InitError = null;
            log.AppendLine("[OCR] SUCCESS — engine initialized.");
            InitDiagnostics = log.ToString();
        }
        catch (Exception ex)
        {
            InitError = $"初始化异常: {ex.Message}";
            log.AppendLine($"[OCR] EXCEPTION: {ex}");
            InitDiagnostics = log.ToString();
            _ready = false;
            Dispose();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns a directory path guaranteed to contain only ASCII characters.
    ///
    /// Path.GetTempPath() may include the user's home directory which on Chinese
    /// Windows contains CJK characters (e.g. C:\Users\徐子涵\...).
    /// CreateOcrPipeline in oneocr.dll rejects such paths with error code 5.
    ///
    /// We use C:\ProgramData\GameTranslatorLens\OneOcr (always ASCII) as the
    /// safe directory because:
    ///   - ProgramData is a well-known folder with a pure-ASCII path
    ///   - It is not user-specific, so the model is shared across users
    ///   - It is not cleaned up by disk cleanup utilities (unlike Temp)
    /// </summary>
    private static string ResolveAsciiOcrDirectory()
    {
        // Environment.SpecialFolder.CommonApplicationData → C:\ProgramData (ASCII)
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, "GameTranslatorLens", "OneOcr");
    }

    private void ConfigureNativePath()
    {
        if (_nativePathConfigured)
        {
            return;
        }

        // SetDllDirectory is only needed so oneocr.dll can find onnxruntime.dll.
        // We set it to the ASCII-safe directory where both DLLs are copied.
        string asciiDir = ResolveAsciiOcrDirectory();
        NativeMethods.SetDllDirectory(asciiDir);

        _nativePathConfigured = true;
    }

    private static string SafeFileSize(string path)
    {
        try
        {
            if (!File.Exists(path)) return "N/A";
            long bytes = new FileInfo(path).Length;
            return bytes >= 1_048_576 ? $"{bytes / 1_048_576.0:F1} MB" : $"{bytes} bytes";
        }
        catch
        {
            return "ERROR";
        }
    }

    private static bool HasNonAscii(string s)
    {
        foreach (char c in s)
        {
            if (c > 127) return true;
        }
        return false;
    }

    private static string GetWin32ErrorMessage(int errorCode)
    {
        try
        {
            return new System.ComponentModel.Win32Exception(errorCode).Message;
        }
        catch
        {
            return "unknown";
        }
    }

    public void Dispose()
    {
        _processOptions?.Dispose();
        _processOptions = null;
        _pipeline?.Dispose();
        _pipeline = null;
        _context?.Dispose();
        _context = null;
        _ready = false;
    }

    private static string PtrToUtf8(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
        {
            return "";
        }

        int length = 0;
        while (Marshal.ReadByte(ptr, length) != 0)
        {
            length++;
        }

        byte[] buffer = new byte[length];
        Marshal.Copy(ptr, buffer, 0, length);
        return Encoding.UTF8.GetString(buffer);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Img
    {
        public int t;
        public int col;
        public int row;
        public int _unk;
        public long step;
        public IntPtr data_ptr;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BoundingBox
    {
        public float x1;
        public float y1;
        public float x2;
        public float y2;
        public float x3;
        public float y3;
        public float x4;
        public float y4;
    }

    private sealed class OcrNativeHandle : SafeHandle
    {
        private readonly OcrNativeHandleKind _kind;

        public OcrNativeHandle(long value, OcrNativeHandleKind kind)
            : base(IntPtr.Zero, ownsHandle: true)
        {
            _kind = kind;
            SetHandle(new IntPtr(value));
        }

        public long Value => handle.ToInt64();

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            long value = handle.ToInt64();
            long result = _kind switch
            {
                OcrNativeHandleKind.ProcessOptions => OneOcrNative.ReleaseOcrProcessOptions(value),
                OcrNativeHandleKind.Pipeline => OneOcrNative.ReleaseOcrPipeline(value),
                OcrNativeHandleKind.InitOptions => OneOcrNative.ReleaseOcrInitOptions(value),
                _ => 0
            };
            return result == 0;
        }
    }

    private enum OcrNativeHandleKind
    {
        InitOptions,
        Pipeline,
        ProcessOptions
    }

    private static partial class OneOcrNative
    {
        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long CreateOcrInitOptions(out long ctx);

        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long OcrInitOptionsSetUseModelDelayLoad(long ctx, byte flag);

        [LibraryImport("oneocr.dll", StringMarshalling = StringMarshalling.Utf8)]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long CreateOcrPipeline(string modelPath, string key, long ctx, out long pipeline);

        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long CreateOcrProcessOptions(out long opt);

        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long OcrProcessOptionsSetMaxRecognitionLineCount(long opt, long count);

        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long RunOcrPipeline(long pipeline, ref Img img, long opt, out long instance);

        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long GetOcrLineCount(long instance, out long count);

        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long GetOcrLine(long instance, long index, out long line);

        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long GetOcrLineContent(long line, out IntPtr content);

        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long GetOcrLineBoundingBox(long line, out IntPtr boundingBoxPtr);

        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long ReleaseOcrResult(long instance);

        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long ReleaseOcrProcessOptions(long opt);

        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long ReleaseOcrPipeline(long pipeline);

        [LibraryImport("oneocr.dll")]
        [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
        public static partial long ReleaseOcrInitOptions(long ctx);
    }

    private static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll", EntryPoint = "SetDllDirectoryW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetDllDirectory(string lpPathName);
    }
}
