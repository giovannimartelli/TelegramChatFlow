namespace TelegramChatFlow.Runtime;

/// <summary>
/// State machine di navigazione: gestisce l'avanzamento tra step,
/// l'ingresso/uscita dai sub-flow e il ritorno al menu.
/// </summary>
public sealed class FlowNavigator
{
    private readonly FlowRegistry _registry;
    private readonly StepRenderer _renderer;
    private readonly MessageManager _messages;

    public FlowNavigator(
        FlowRegistry registry,
        StepRenderer renderer,
        MessageManager messages)
    {
        _registry = registry;
        _renderer = renderer;
        _messages = messages;
    }

    /// <summary>Avanza allo step successivo, al menu sub-flow, o completa il flusso.</summary>
    public async Task AdvanceAsync(FlowSession session, FlowDefinition flow,
        Dictionary<string, object?>? dataSnapshot = null)
    {
        var currentStep = flow.Steps.ElementAtOrDefault(session.CurrentStepIndex);
        if (currentStep?.Persistent == true)
            await _messages.DetachBotMessageAsync(session);

        session.StepHistory.Push(new StepHistoryEntry
        {
            StepIndex = session.CurrentStepIndex,
            Data = dataSnapshot ?? new Dictionary<string, object?>(session.Data)
        });
        var next = session.CurrentStepIndex + 1;

        if (next < flow.Steps.Count)
        {
            session.CurrentStepIndex = next;
            await _renderer.RenderStepAsync(session, flow.Steps[next]);
        }
        else if (flow.SubFlows is { Count: > 0 })
        {
            session.CurrentStepIndex = flow.Steps.Count;
            await _renderer.ShowSubFlowMenuAsync(session, flow);
        }
        else
        {
            await CompleteCurrentFlowAsync(session);
        }
    }

    /// <summary>Torna allo step precedente, al flusso genitore, o al menu.</summary>
    public async Task GoBackAsync(FlowSession session)
    {
        if (session.CurrentFlowId is null) return;

        await _messages.CleanupPersistentMessagesAsync(session);

        if (session.StepHistory.TryPop(out var entry))
        {
            var flow = _registry.GetFlow(session.CurrentFlowId);
            if (flow is null)
            {
                await ResetToMenuAsync(session);
                return;
            }

            session.CurrentStepIndex = entry.StepIndex;
            session.Data = entry.Data;
            await _renderer.RenderStepAsync(session, flow.Steps[entry.StepIndex]);
        }
        else if (session.FlowStack.Count > 0)
        {
            var frame = session.FlowStack.Pop();
            session.CurrentFlowId = frame.FlowId;
            session.CurrentStepIndex = frame.StepIndex;
            session.Data = frame.Data;
            session.StepHistory = frame.StepHistory;

            var parent = _registry.GetFlow(frame.FlowId);
            if (parent?.SubFlows is { Count: > 0 })
                await _renderer.ShowSubFlowMenuAsync(session, parent);
            else
                await ResetToMenuAsync(session);
        }
        else
        {
            await ResetToMenuAsync(session);
        }
    }

    /// <summary>Salta direttamente a uno step per ID.</summary>
    public async Task GoToStepAsync(FlowSession session, FlowDefinition flow, string stepId,
        Dictionary<string, object?> dataSnapshot)
    {
        var targetIdx = -1;
        for (var i = 0; i < flow.Steps.Count; i++)
            if (flow.Steps[i].Id == stepId)
            {
                targetIdx = i;
                break;
            }

        if (targetIdx < 0)
        {
            await ResetToMenuAsync(session);
            return;
        }

        session.StepHistory.Push(new StepHistoryEntry
        {
            StepIndex = session.CurrentStepIndex,
            Data = dataSnapshot
        });
        session.CurrentStepIndex = targetIdx;
        await _renderer.RenderStepAsync(session, flow.Steps[targetIdx]);
    }

    /// <summary>Salta lo step corrente (se è skippable).</summary>
    public async Task SkipStepAsync(FlowSession session)
    {
        if (session.CurrentFlowId is null) return;

        var flow = _registry.GetFlow(session.CurrentFlowId);
        if (flow is null) return;

        var step = flow.Steps.ElementAtOrDefault(session.CurrentStepIndex);
        if (step?.Skippable == true)
            await AdvanceAsync(session, flow, new Dictionary<string, object?>(session.Data));
    }

    /// <summary>Avvia un flusso root.</summary>
    public async Task StartFlowAsync(FlowSession session, string flowId)
    {
        if (!_registry.TryGetRootFlow(flowId, out var flow)) return;

        session.CurrentFlowId = flowId;
        session.CurrentStepIndex = 0;
        session.Data = new();
        session.FlowStack = new();
        session.StepHistory = new();

        if (flow.Steps.Count > 0)
            await _renderer.RenderStepAsync(session, flow.Steps[0]);
        else if (flow.SubFlows is { Count: > 0 })
            await _renderer.ShowSubFlowMenuAsync(session, flow);
    }

    /// <summary>Avvia un sub-flow, salvando il frame corrente nello stack.</summary>
    public async Task StartSubFlowAsync(FlowSession session, string subFlowId)
    {
        var current = _registry.GetFlow(session.CurrentFlowId!);
        var sub = current?.SubFlows?.FirstOrDefault(s => s.Id == subFlowId);
        if (sub is null) return;

        session.FlowStack.Push(new SubFlowFrame
        {
            FlowId = session.CurrentFlowId!,
            StepIndex = session.CurrentStepIndex,
            Data = new Dictionary<string, object?>(session.Data),
            StepHistory = session.StepHistory
        });

        session.CurrentFlowId = subFlowId;
        session.CurrentStepIndex = 0;
        session.Data = new();
        session.StepHistory = new();

        if (sub.Steps.Count > 0)
            await _renderer.RenderStepAsync(session, sub.Steps[0]);
        else if (sub.SubFlows is { Count: > 0 })
            await _renderer.ShowSubFlowMenuAsync(session, sub);
    }

    /// <summary>Completa il flusso corrente e torna al genitore o al menu.</summary>
    public async Task CompleteCurrentFlowAsync(FlowSession session)
    {
        if (session.FlowStack.Count > 0)
        {
            var frame = session.FlowStack.Pop();
            session.CurrentFlowId = frame.FlowId;
            session.CurrentStepIndex = frame.StepIndex;
            session.Data = frame.Data;
            session.StepHistory = frame.StepHistory;

            var parent = _registry.GetFlow(frame.FlowId);
            if (parent?.SubFlows is { Count: > 0 })
                await _renderer.ShowSubFlowMenuAsync(session, parent);
            else if (parent is not null)
                await AdvanceAsync(session, parent);
            else
                await ResetToMenuAsync(session);
        }
        else
        {
            await ResetToMenuAsync(session);
        }
    }

    /// <summary>Resetta la sessione e mostra il menu principale.</summary>
    public async Task ResetToMenuAsync(FlowSession session)
    {
        await _messages.CleanupAllFlowMessagesAsync(session);

        if (session.HasReplyKeyboard)
        {
            await _messages.RemoveReplyKeyboardAsync(session.ChatId);
            session.HasReplyKeyboard = false;
        }

        session.CurrentFlowId = null;
        session.CurrentStepIndex = 0;
        session.Data = new();
        session.FlowStack = new();
        session.StepHistory = new();

        await _renderer.ShowMenuAsync(session);
    }
}