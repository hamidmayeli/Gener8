using Gener8.Contexts;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
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
            .Where(static target => target is not null);

        context.RegisterSourceOutput(pipeline, static (ctx, target) => SourceProducer.Emit(ctx, target!));

        var repositoryKinds = pipeline
            .Select(static (t, _) => t!.Repository)
            .Where(static k => k != RepositoryKind.None)
            .Collect();

        context.RegisterSourceOutput(repositoryKinds, SourceProducer.EmitRepositoryBaseClasses);

        // Collect model full names of user-annotated DTOs so auto-generation skips duplicates.
        var userAnnotatedModelNames = pipeline
            .Select(static (t, _) => t!.Model.FullName)
            .Collect();

        var autoDtoTargets = pipeline
            .SelectMany(static (t, _) => t!.AutoDtoTargets)
            .Collect();

        context.RegisterSourceOutput(
            autoDtoTargets.Combine(userAnnotatedModelNames),
            static (ctx, pair) => SourceProducer.EmitAutoDtos(ctx, pair.Left, pair.Right));
    }
}
