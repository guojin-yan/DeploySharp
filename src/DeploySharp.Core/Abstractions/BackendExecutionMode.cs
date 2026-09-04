namespace JYPPX.DeploySharp
{
    /// <summary>Describes where a backend is expected to execute. / 描述后端预期的执行位置。</summary>
    public enum BackendExecutionMode
    {
        /// <summary>Execute in the consumer process. / 在使用方进程内执行。</summary>
        InProcess = 0,
        /// <summary>Execute in an application-owned worker process. / 在应用拥有的 Worker 进程中执行。</summary>
        Worker = 1,
        /// <summary>Allow either in-process or worker execution. / 允许进程内或 Worker 执行。</summary>
        InProcessOrWorker = 2
    }
}
