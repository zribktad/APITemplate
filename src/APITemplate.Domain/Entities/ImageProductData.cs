using MongoDB.Bson.Serialization.Attributes;

namespace APITemplate.Domain.Entities;

[BsonDiscriminator("image")]
public sealed class ImageProductData : ProductData
{
    public int Width { get; set; }

    public int Height { get; set; }

    public string Format { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }
}
