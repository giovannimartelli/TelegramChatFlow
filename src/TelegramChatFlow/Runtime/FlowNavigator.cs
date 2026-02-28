namespace TelegramChatFlow.Runtime;

// OK VISTO

/// <summary>
/// Navigation state machine: handles step advancement,
/// entering/exiting sub-flows, and returning to the menu.
/// </summary>
public sealed class FlowNavigator(
    FlowRegistry registry,
    StepRenderer renderer,
    MessageManager messages)
{
    /// <summary>Advances to the next step, or completes the flow.</summary>
    public async Task AdvanceAsync(FlowSession session, FlowDefinition flow, object dataSnapshot)
    {
        var currentStep = flow.Steps.ElementAtOrDefault(session.CurrentStepIndex);
        if (currentStep?.Persistent == true)
            await messages.DetachPersistentMessageAsync(session);

        session.StepHistory.Push(new StepHistoryEntry
        {
            StepIndex = session.CurrentStepIndex,
            Data = dataSnapshot
        });
        var next = session.CurrentStepIndex + 1;

        if (next < flow.Steps.Count)
        {
            session.CurrentStepIndex = next;
            await renderer.RenderStepAsync(session, flow.Steps[next]);
        }
        else
        {
            await CompleteCurrentFlowAsync(session);
        }
    }

    /// <summary>Goes back to the previous step, parent flow, or menu.</summary>
    public async Task GoBackAsync(FlowSession session)
    {
        if (session.CurrentFlowId is null)
            throw new InvalidOperationException("Cannot go back: no active flow.");

        await messages.CleanupPersistentMessagesAsync(session);

        if (session.StepHistory.TryPop(out var entry))
        {
            var flow = registry.GetFlow(session.CurrentFlowId);
            session.CurrentStepIndex = entry.StepIndex;
            session.Data = entry.Data;
            await renderer.RenderStepAsync(session, flow.Steps[entry.StepIndex]);
        }
        else if (session.FlowStack.Count > 0)
        {
            var frame = session.FlowStack.Pop();
            session.CurrentFlowId = frame.FlowId;
            session.CurrentStepIndex = frame.StepIndex;
            session.Data = frame.Data;
            session.StepHistory = frame.StepHistory;
            var parent = registry.GetFlow(frame.FlowId);
            await renderer.RenderStepAsync(session, parent.Steps[frame.StepIndex]);
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
            throw new ArgumentException($"Step ID '{stepId}' not found in flow '{flow.Id}'.");

        session.StepHistory.Push(new StepHistoryEntry
        {
            StepIndex = session.CurrentStepIndex,
            Data = dataSnapshot
        });
        session.CurrentStepIndex = targetIdx;
        await renderer.RenderStepAsync(session, flow.Steps[targetIdx]);
    }

    /// <summary>Skips the current step (if it is skippable).</summary>
    public async Task SkipStepAsync(FlowSession session)
    {
        if (session.CurrentFlowId is null) return;

        var flow = registry.GetFlow(session.CurrentFlowId);
        await AdvanceAsync(session, flow, flow.CloneData(session.Data!));
    }

    /// <summary>Starts a root flow.</summary>
    public async Task StartFlowAsync(FlowSession session, string flowId)
    {
        var flow = registry.GetFlow(flowId);
        session.ResetAfterFlow();
        session.CurrentFlowId = flowId;
        session.Data = flow.CreateData();

        if (flow.Steps.Count > 0)
            await renderer.RenderStepAsync(session, flow.Steps[0]);
    }

    /// <summary>Starts a sub-flow from a handler, saving the current frame on the stack.</summary>
    public async Task StartSubFlowAsync(FlowSession session, string subFlowId, object dataSnapshot)
    {
        var sub = registry.GetFlow(subFlowId);

        session.FlowStack.Push(new SubFlowFrame
        {
            FlowId = session.CurrentFlowId!,
            StepIndex = session.CurrentStepIndex,
            Data = dataSnapshot,
            StepHistory = session.StepHistory
        });

        session.CurrentFlowId = subFlowId;
        session.CurrentStepIndex = 0;
        session.StepHistory = new Stack<StepHistoryEntry>();

        if (sub.Steps.Count > 0)
            await renderer.RenderStepAsync(session, sub.Steps[0]);
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

            var parent = registry.GetFlow(frame.FlowId);
            await AdvanceAsync(session, parent, parent.CloneData(session.Data!));
        }
        else
        {
            await ResetToMenuAsync(session);
        }
    }

    /// <summary>Resets the session and shows the main menu.</summary>
    public async Task ResetToMenuAsync(FlowSession session)
    {
        await messages.CleanupAllFlowMessagesAsync(session);

        if (session.HasReplyKeyboard)
        {
            await messages.RemoveReplyKeyboardAsync(session.ChatId);
            session.HasReplyKeyboard = false;
        }

        session.ResetAfterFlow();

        await renderer.ShowMenuAsync(session);
    }
}