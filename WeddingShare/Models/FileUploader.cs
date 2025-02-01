namespace WeddingShare.Models
{
    public class FileUploader
    {
        public FileUploader(string id, string? key, string url, bool identityRequired = false)
        {
            this.GalleryId = id;
            this.SecretKey = key;
            this.UploadUrl = url;
            this.IdentityRequired = identityRequired;
        }

        public string? GalleryId { get; set; }
        public string? SecretKey { get; set; }
        public string? UploadUrl { get; set; }
        public bool IdentityRequired { get; set; }
    }
}