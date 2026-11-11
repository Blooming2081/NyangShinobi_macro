namespace NyangShinobi.Domain;

public enum FlowOutcome { Continue, Completed, PausedByEvent, NeedsUserAction, Stopped }
public enum InputOutcome { Pending, Verified, NeedsUserAction }
