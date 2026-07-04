namespace CanXe.Application.Models;

public enum WeighTicketWorkflowState
{
    Draft,
    AwaitingSecondWeigh,
    Completed
}

public static class WeighTicketWorkflow
{
    public static WeighTicketWorkflowState FromEventCount(int eventCount) =>
        eventCount switch
        {
            >= 2 => WeighTicketWorkflowState.Completed,
            1 => WeighTicketWorkflowState.AwaitingSecondWeigh,
            _ => WeighTicketWorkflowState.Draft
        };

    public static string DisplayText(WeighTicketWorkflowState state) =>
        state switch
        {
            WeighTicketWorkflowState.AwaitingSecondWeigh => "CHỜ CÂN LẦN 2",
            WeighTicketWorkflowState.Draft => "CHƯA LƯU",
            _ => string.Empty
        };

    public static string ListColumnText(WeighTicketWorkflowState state) =>
        string.IsNullOrEmpty(DisplayText(state)) ? "—" : DisplayText(state);
}
