namespace TelegramChatFlow.Models.Steps;

/// <summary>Esito dell'elaborazione dell'input di uno step.</summary>
public abstract class StepResult
{
    private StepResult()
    {
    }

    /// <summary>Input valido, avanza allo step successivo.</summary>
    public static readonly StepResult Next = new NextResult();

    /// <summary>Input non valido, resta sullo step corrente.</summary>
    public static readonly StepResult Retry = new RetryResult();

    /// <summary>Esci dal flusso.</summary>
    public static readonly StepResult Exit = new ExitResult();

    /// <summary>Salta direttamente allo step con l'id specificato.</summary>
    public static StepResult GoTo(string stepId) => new GoToResult(stepId);

    /// <summary>Lancia un sub-flow dall'handler corrente.</summary>
    public static StepResult SubFlow(string subFlowId) => new SubFlowResult(subFlowId);

    /// <summary>Resta sullo step corrente mostrando un contenuto visivo diverso.</summary>
    public static StepResult RetryWith(ShowDefinition show) => new RetryResult { Show = show };

    public sealed class NextResult : StepResult
    {
        internal NextResult()
        {
        }
    }

    public sealed class RetryResult : StepResult
    {
        internal RetryResult()
        {
        }

        public ShowDefinition? Show { get; init; }
    }

    public sealed class ExitResult : StepResult
    {
        internal ExitResult()
        {
        }
    }

    public sealed class GoToResult : StepResult
    {
        internal GoToResult(string stepId)
        {
            StepId = stepId;
        }

        public string StepId { get; }
    }

    public sealed class SubFlowResult : StepResult
    {
        internal SubFlowResult(string subFlowId)
        {
            SubFlowId = subFlowId;
        }

        public string SubFlowId { get; }
    }
}