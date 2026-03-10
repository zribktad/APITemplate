using MongoDB.Bson.Serialization.Attributes;

namespace APITemplate.Domain.Entities;

[BsonDiscriminator("video")]
public sealed class VideoProductData : ProductData
{
    public int DurationSeconds { get; set; }

    public string Resolution { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }
}
