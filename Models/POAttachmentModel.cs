namespace BTITPORequest.Models
{
    public class POAttachmentModel
    {
        public int    AttachId    { get; set; }
        public int    POId        { get; set; }
        public string FileName    { get; set; } = string.Empty;   // original
        public string StoredName  { get; set; } = string.Empty;   // GUID on disk
        public long?  FileSize    { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public string UploadedBy  { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }

        // Helpers
        public string FileSizeDisplay => FileSize.HasValue
            ? FileSize > 1_048_576 ? $"{FileSize / 1_048_576.0:N1} MB"
            : FileSize > 1024       ? $"{FileSize / 1024.0:N0} KB"
            :                         $"{FileSize} B"
            : "";

        public string FileIcon => ContentType switch
        {
            "application/pdf"  => "bi-file-earmark-pdf text-danger",
            "image/jpeg"
            or "image/jpg"
            or "image/png"     => "bi-file-earmark-image text-primary",
            _                  => "bi-file-earmark text-secondary"
        };
    }
}
