namespace JYPPX.DeploySharp.Internal
{
    /// <summary>Exposes the number of independently-created sessions behind an internal session wrapper. / 公开内部 Session 包装器中的独立 Session 数量。</summary>
    internal interface IInferenceSessionConcurrency
    {
        internal int MaximumConcurrency { get; }
    }
}
