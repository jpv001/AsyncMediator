using System.Transactions;

namespace AsyncMediator;

/// <summary>
/// Abstract base class for command handlers with validation and optional transaction support.
/// </summary>
/// <typeparam name="TCommand">The type of command this handler processes.</typeparam>
/// <remarks>
/// Override <see cref="UseTransactionScope"/> to control transaction behavior.
/// Default is false for maximum performance. Set to true when ACID compliance is required.
/// </remarks>
public abstract class CommandHandler<TCommand>(IMediator mediator) : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Gets the command being processed.
    /// </summary>
    protected TCommand Command { get; private set; } = default!;

    /// <summary>
    /// Gets the mediator instance for sending additional commands or deferring events.
    /// </summary>
    protected IMediator Mediator { get; } = mediator;

    /// <summary>
    /// Gets the cancellation token for the current command execution.
    /// Use this in derived classes to check for cancellation during long-running operations.
    /// </summary>
    protected CancellationToken CancellationToken { get; private set; }

    /// <summary>
    /// Controls whether command execution is wrapped in a TransactionScope.
    /// Override and return true when ACID compliance is required.
    /// Default is false for maximum performance.
    /// </summary>
    protected virtual bool UseTransactionScope => false;

    /// <inheritdoc />
    public async Task<ICommandWorkflowResult> Handle(TCommand command, CancellationToken cancellationToken = default)
    {
        Command = command;
        CancellationToken = cancellationToken;
        var context = new ValidationContext();

        cancellationToken.ThrowIfCancellationRequested();
        await Validate(context, cancellationToken).ConfigureAwait(false);
        if (context.ValidationResults.Count > 0)
            return new CommandWorkflowResult(context.ValidationResults);

        return UseTransactionScope
            ? await HandleWithTransaction(context, cancellationToken).ConfigureAwait(false)
            : await HandleWithoutTransaction(context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ICommandWorkflowResult> HandleWithTransaction(ValidationContext context, CancellationToken cancellationToken)
    {
        ICommandWorkflowResult finalResult;

        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var workflowResult = await DoHandle(context, cancellationToken).ConfigureAwait(false);
            if (context.ValidationResults.Count > 0)
                return new CommandWorkflowResult(context.ValidationResults);

            finalResult = workflowResult ?? CommandWorkflowResult.Ok();
            if (!finalResult.Success)
                return finalResult;

            transaction.Complete();
        }

        // Execute events AFTER transaction commits successfully
        await Mediator.ExecuteDeferredEvents(cancellationToken).ConfigureAwait(false);
        return finalResult;
    }

    private async Task<ICommandWorkflowResult> HandleWithoutTransaction(ValidationContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workflowResult = await DoHandle(context, cancellationToken).ConfigureAwait(false);
        if (context.ValidationResults.Count > 0)
            return new CommandWorkflowResult(context.ValidationResults);

        var finalResult = workflowResult ?? CommandWorkflowResult.Ok();
        if (!finalResult.Success)
            return finalResult;

        await Mediator.ExecuteDeferredEvents(cancellationToken).ConfigureAwait(false);
        return finalResult;
    }

    /// <summary>
    /// Validates the command. Add errors to context.ValidationResults.
    /// </summary>
    /// <param name="validationContext">The validation context to add errors to.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    protected abstract Task Validate(ValidationContext validationContext, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the command logic after validation passes.
    /// </summary>
    /// <param name="validationContext">The validation context.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    protected abstract Task<ICommandWorkflowResult> DoHandle(ValidationContext validationContext, CancellationToken cancellationToken);
}
