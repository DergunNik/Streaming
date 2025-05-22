namespace LiveService.Models;

public class StreamInfo
{
    public required string CloudinaryStreamId { get; set; }
    public required string Name { get; set; }
    public required string ArchivePublicId { get; set; }
    public required int AuthorId { get; set; }
}