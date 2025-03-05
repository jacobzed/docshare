namespace DocShare.Helpers
{
    public record Notify (string Action);

    public record NotifyFileAdded(string FileName) : Notify("FileAdded");

    public record NotifyFileDeleted(string FileName) : Notify("FileDeleted");
    
    public record NotifyStatus(int Count) : Notify("Status");

}
