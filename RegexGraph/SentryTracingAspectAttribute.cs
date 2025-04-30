using System.Diagnostics;
using PostSharp.Aspects;
using PostSharp.Serialization;
using Sentry;

namespace RegexNodeGraph;

[PSerializable]
public class SentryTracingAspect : OnMethodBoundaryAspect
{
    public override void OnEntry(MethodExecutionArgs args)
    {
        // Avvia una transazione di tracing con il nome del metodo
        var transaction = SentrySdk.StartTransaction(args.Method.DeclaringType.FullName + "." + args.Method.Name, "method-execution");
        args.MethodExecutionTag = transaction;

        Debug.WriteLine($"[Sentry] Start tracing: {args.Method.DeclaringType.FullName}.{args.Method.Name}");
    }

    public override void OnExit(MethodExecutionArgs args)
    {
        if (args.MethodExecutionTag is ISpan transaction)
        {
            transaction.Finish(SpanStatus.Ok);
        }

        Debug.WriteLine($"[Sentry] Finished tracing: {args.Method.DeclaringType.FullName}.{args.Method.Name}");
    }

    public override void OnException(MethodExecutionArgs args)
    {
        if (args.MethodExecutionTag is ISpan transaction)
        {
            transaction.Finish(SpanStatus.InternalError);
        }

        SentrySdk.CaptureException(args.Exception);

        Debug.WriteLine($"[Sentry] Error in: {args.Method.DeclaringType.FullName}.{args.Method.Name}: {args.Exception.Message}");
    }
}