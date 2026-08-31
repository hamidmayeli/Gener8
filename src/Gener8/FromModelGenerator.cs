using Gener8.Contexts;
using Microsoft.CodeAnalysis;
using System;

namespace Gener8;

[Generator]
public sealed partial class FromModelGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Attributes, enums, and repository contracts are now in Gener8.Abstractions
        // (bundled inside the Gener8 NuGet under lib/netstandard2.0/). There is no longer
        // a RegisterPostInitializationOutput step needed for those types.

        var pipeline = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => SyntaxTransformer.IsPartialClassWithAttributes(node),
                transform: static (ctx, _) => SyntaxTransformer.ExtractClassTarget(ctx))
            .Where(static result => result is not null);

        var nullableEnabled = context.CompilationProvider
            .Select(static (c, _) => c.Options.NullableContextOptions != NullableContextOptions.Disable);

        context.RegisterSourceOutput(pipeline.Combine(nullableEnabled), static (ctx, pair) =>
        {
            var (result, nullable) = pair;
            if (result!.Errors is { Count: > 0 } errors)
            {
                foreach (var e in errors)
                    ctx.ReportDiagnostic(e);
                return;
            }
            try { SourceProducer.Emit(ctx, result.Target!, nullable); }
            catch (Exception ex)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnexpectedError, Location.None,
                    result.Target!.ClassName, ex.Message));
            }
        });

        var successPipeline = pipeline
            .Where(static r => r?.Target is not null)
            .Select(static (r, _) => r!.Target!);

        var userAnnotatedModelNames = successPipeline
            .Select(static (t, _) => t.Model.FullName)
            .Collect();

        var autoDtoTargets = successPipeline
            .SelectMany(static (t, _) => t.AutoDtoTargets)
            .Collect();

        context.RegisterSourceOutput(
            autoDtoTargets.Combine(userAnnotatedModelNames).Combine(nullableEnabled),
            static (ctx, pair) =>
            {
                var ((targets, modelNames), nullable) = pair;
                try { SourceProducer.EmitAutoDtos(ctx, targets, modelNames, nullable); }
                catch (Exception ex)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnexpectedError, Location.None,
                        "auto DTOs", ex.Message));
                }
            });
    }
}
