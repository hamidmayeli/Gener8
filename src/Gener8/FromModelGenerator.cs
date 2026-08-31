using Gener8.Contexts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Text;

namespace Gener8;

[Generator]
public sealed partial class FromModelGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            foreach (var source in DefaultSource.Essentials)
                ctx.AddSource(source.Filename, SourceText.From(source.Code, Encoding.UTF8));
        });

        var pipeline = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => SyntaxTransformer.IsPartialClassWithAttributes(node),
                transform: static (ctx, _) => SyntaxTransformer.ExtractClassTarget(ctx))
            .Where(static result => result is not null);

        var nullableEnabled = context.CompilationProvider
            .Select(static (c, _) => c.Options.NullableContextOptions != NullableContextOptions.Disable);

        // Report diagnostics for known errors; emit source for successful targets.
        context.RegisterSourceOutput(pipeline.Combine(nullableEnabled), static (ctx, pair) =>
        {
            var (result, nullable) = pair;
            if (result!.Errors is { Count: > 0 } errors)
            {
                foreach (var e in errors)
                    ctx.ReportDiagnostic(e);
                return;
            }

            try
            {
                SourceProducer.Emit(ctx, result.Target!, nullable);
            }
            catch (Exception ex)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnexpectedError, Location.None,
                    result.Target!.ClassName, ex.Message));
            }
        });

        // Downstream pipelines only consume successfully processed targets.
        var successPipeline = pipeline
            .Where(static r => r?.Target is not null)
            .Select(static (r, _) => r!.Target!);

        var repositoryKinds = successPipeline
            .Select(static (t, _) => t.Repository)
            .Where(static k => k != RepositoryKind.None)
            .Collect();

        context.RegisterSourceOutput(repositoryKinds, static (ctx, kinds) =>
        {
            try
            {
                SourceProducer.EmitRepositoryBaseClasses(ctx, kinds);
            }
            catch (Exception ex)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.UnexpectedError, Location.None,
                    "repository base classes", ex.Message));
            }
        });

        // Collect model full names of user-annotated DTOs so auto-generation skips duplicates.
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
                try
                {
                    SourceProducer.EmitAutoDtos(ctx, targets, modelNames, nullable);
                }
                catch (Exception ex)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.UnexpectedError, Location.None,
                        "auto DTOs", ex.Message));
                }
            });
    }
}
