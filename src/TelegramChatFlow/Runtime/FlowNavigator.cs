namespace TelegramChatFlow.Runtime;

/// <summary>
/// Navigation state machine: handles step advancement,
/// entering/exiting sub-flows, and returning to the menu.
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

    /// <summary>Advances to the next step, or completes the flow.</summary>
    public async Task AdvanceAsync(FlowSession session, FlowDefinition flow,
        object? dataSnapshot = null)
    {
        var currentStep = flow.Steps.ElementAtOrDefault(session.CurrentStepIndex);
        if (currentStep?.Persistent == true)
            await _messages.DetachBotMessageAsync(session);

        session.StepHistory.Push(new StepHistoryEntry
        {
            StepIndex = session.CurrentStepIndex,
            Data = dataSnapshot ?? flow.CloneData(session.Data!)
        });
        var next = session.CurrentStepIndex + 1;

        if (next < flow.Steps.Count)
        {
            session.CurrentStepIndex = next;
            await _renderer.RenderStepAsync(session, flow.Steps[next]);
        }
        else
        {
            await CompleteCurrentFlowAsync(session);
        }
    }

    /// <summary>Goes back to the previous step, parent flow, or menu.</summary>
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
            if (parent is not null)
                await _renderer.RenderStepAsync(session, parent.Steps[frame.StepIndex]);
            else
                await ResetToMenuAsync(session);
        }
        else
        {
            await ResetToMenuAsync(session);
        }
    }

    /// <summary>Jumps directly to a step by ID.</summary>
    public async Task GoToStepAsync(FlowSession session, FlowDefinition flow, string stepId,
        object dataSnapshot)
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

    /// <summary>Skips the current step (if it is skippable).</summary>
    public async Task SkipStepAsync(FlowSession session)
    {
        if (session.CurrentFlowId is null) return;

        var flow = _registry.GetFlow(session.CurrentFlowId);
        if (flow is null) return;

        var step = flow.Steps.ElementAtOrDefault(session.CurrentStepIndex);
        if (step?.Skippable == true)
            await AdvanceAsync(session, flow, flow.CloneData(session.Data!));
    }

    /// <summary>Starts a root flow.</summary>
    public async Task StartFlowAsync(FlowSession session, string flowId)
    {
        if (!_registry.TryGetRootFlow(flowId, out var flow)) return;

        session.Reset();
        session.CurrentFlowId = flowId;
        session.Data = flow.CreateData();

        if (flow.Steps.Count > 0)
            await _renderer.RenderStepAsync(session, flow.Steps[0]);
    }

    /// <summary>Starts a sub-flow from a handler, saving the current frame on the stack.</summary>
    public async Task StartSubFlowAsync(FlowSession session, string subFlowId,
        object dataSnapshot)
    {
        var sub = _registry.GetFlow(subFlowId);
        if (sub is null) return;

        session.FlowStack.Push(new SubFlowFrame
        {
            FlowId = session.CurrentFlowId!,
            StepIndex = session.CurrentStepIndex,
            Data = dataSnapshot,
            StepHistory = session.StepHistory
        });

        session.CurrentFlowId = subFlowId;
        session.CurrentStepIndex = 0;
        session.StepHistory = new();

        if (sub.Steps.Count > 0)
            await _renderer.RenderStepAsync(session, sub.Steps[0]);
    }

    /// <summary>Completes the current flow and returns to the parent or menu.</summary>
    public async Task CompleteCurrentFlowAsync(FlowSession session)
    {
        if (session.FlowStack.Count > 0)
        {
            var frame = session.FlowStack.Pop();
            session.CurrentFlowId = frame.FlowId;
            session.CurrentStepIndex = frame.StepIndex;
            session.StepHistory = frame.StepHistory;

            var parent = _registry.GetFlow(frame.FlowId);
            if (parent is not null)
                await AdvanceAsync(session, parent);
            else
                await ResetToMenuAsync(session);
        }
        else
        {
            await ResetToMenuAsync(session);
        }
    }

    /// <summary>Resets the session and shows the main menu.</summary>
    public async Task ResetToMenuAsync(FlowSession session)
    {
        await _messages.CleanupAllFlowMessagesAsync(session);

        if (session.HasReplyKeyboard)
        {
            await _messages.RemoveReplyKeyboardAsync(session.ChatId);
            session.HasReplyKeyboard = false;
        }

        session.Reset();

        await _renderer.ShowMenuAsync(session);
    }
}
