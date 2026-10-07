namespace JobManager.API.Entities
{
    public class JobAplication
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public Job Job { get; set; }
        public string CandidateName { get; set; }
        public string CandidateEmail { get; set; }
        public string? CVUrl { get; set; } // Optional: URL stored in cloud storage (S3)
    }
}
