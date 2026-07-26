namespace EasyClipStash;

static class Program
{
    private const string MutexName = @"Local\EasyClipStash_SingleInstance";

    /// <summary>업데이트가 띄운 인스턴스임을 알리는 인자. 이 경우 이전 인스턴스의 종료를 기다린다.</summary>
    internal const string AfterUpdateArgument = "--after-update";

    private static Mutex? _instanceMutex;

    [STAThread]
    static void Main(string[] args)
    {
        bool afterUpdate = args.Any(a => string.Equals(a, AfterUpdateArgument, StringComparison.OrdinalIgnoreCase));

        if (!TryAcquireSingleInstance(afterUpdate))
        {
            MessageBox.Show(L.AlreadyRunning, "EasyClipStash", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            ApplicationConfiguration.Initialize();
            InstallCrashGuards();
            Application.Run(new TrayApplicationContext(startedByUpdate: afterUpdate));
        }
        finally
        {
            ReleaseInstanceLock();
        }
    }

    /// <summary>
    /// 예상하지 못한 예외를 잡아 안내로 바꾼다.
    /// 트레이에 상주하며 로그인 시 자동 실행되는 앱이라, 낯선 .NET 크래시 창이 뜨는 것보다
    /// 무엇이 잘못됐는지 알려주고 계속 동작하는 편이 낫다.
    /// </summary>
    private static void InstallCrashGuards()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.ThreadException += (_, e) => ReportCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) ReportCrash(ex);
        };
    }

    private static void ReportCrash(Exception ex)
    {
        try
        {
            MessageBox.Show(L.UnexpectedError(ex.Message), "EasyClipStash",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch
        {
            // 안내조차 띄울 수 없는 상황이면 더 할 수 있는 게 없다
        }
    }

    /// <summary>
    /// 단일 실행 잠금을 잡는다.
    /// 업데이트 직후라면 이전 인스턴스가 아직 종료 중일 수 있으므로 잠시 기다렸다 재시도한다.
    /// (그렇지 않으면 교체 직후 뜬 새 인스턴스가 "이미 실행 중"으로 튕겨 아무것도 남지 않는다)
    /// </summary>
    private static bool TryAcquireSingleInstance(bool afterUpdate)
    {
        DateTime deadline = DateTime.UtcNow + (afterUpdate ? TimeSpan.FromSeconds(15) : TimeSpan.Zero);

        while (true)
        {
            var mutex = new Mutex(true, MutexName, out bool createdNew);
            if (createdNew)
            {
                _instanceMutex = mutex;
                return true;
            }

            mutex.Dispose();
            if (DateTime.UtcNow >= deadline)
                return false;
            Thread.Sleep(250);
        }
    }

    /// <summary>
    /// 단일 실행 잠금을 즉시 푼다.
    /// 업데이트가 새 인스턴스를 띄우기 직전에 호출해, 새 인스턴스가 잠금을 바로 잡을 수 있게 한다.
    /// </summary>
    internal static void ReleaseInstanceLock()
    {
        if (_instanceMutex is null) return;

        try { _instanceMutex.ReleaseMutex(); }
        catch (ApplicationException) { /* 다른 스레드에서 호출된 경우 — 아래 Dispose가 핸들을 닫아 해제된다 */ }

        _instanceMutex.Dispose();
        _instanceMutex = null;
    }
}
