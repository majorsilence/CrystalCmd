/** Mirrors Majorsilence.CrystalCmd.WorkQueues.WorkItemStatus (values persisted to the DB). */
public enum WorkItemStatus {
    Unknown(0),
    Pending(1),
    Processing(2),
    Completed(3),
    Failed(4);

    public final int code;

    WorkItemStatus(int code) {
        this.code = code;
    }

    public static WorkItemStatus fromCode(int code) {
        for (WorkItemStatus s : values()) {
            if (s.code == code) {
                return s;
            }
        }
        return Unknown;
    }
}
