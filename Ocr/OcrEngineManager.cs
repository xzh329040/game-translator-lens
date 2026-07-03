namespace GameTranslatorLens.Ocr;

public sealed class OcrEngineManager : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IOcrEngine? _currentEngine;
    private string? _currentEngineName;
    private string? _currentLanguage;

    public async Task<T> UseAsync<T>(
        string engineName,
        string languageCode,
        Func<IOcrEngine, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            IOcrEngine engine = GetOrCreateEngine(engineName, languageCode);
            return await action(engine, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public (bool ready, string? error) GetEngineStatus()
    {
        _gate.Wait();
        try
        {
            if (_currentEngine is null)
            {
                return (false, "引擎未创建");
            }

            return (_currentEngine.IsReady, _currentEngine.InitError);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate()
    {
        _gate.Wait();
        try
        {
            DisposeCurrentEngineUnlocked();
            _currentEngineName = null;
            _currentLanguage = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        Invalidate();
        _gate.Dispose();
    }

    private IOcrEngine GetOrCreateEngine(string engineName, string languageCode)
    {
        if (_currentEngine is not null &&
            string.Equals(_currentEngineName, engineName, StringComparison.Ordinal) &&
            string.Equals(_currentLanguage, languageCode, StringComparison.Ordinal))
        {
            return _currentEngine;
        }

        DisposeCurrentEngineUnlocked();
        _currentEngineName = engineName;
        _currentLanguage = languageCode;
        _currentEngine = new OneOcrEngine();
        return _currentEngine;
    }

    private void DisposeCurrentEngineUnlocked()
    {
        if (_currentEngine is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _currentEngine = null;
    }
}
