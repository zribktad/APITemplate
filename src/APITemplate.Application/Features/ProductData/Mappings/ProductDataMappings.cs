using ImageProductDataEntity = APITemplate.Domain.Entities.ImageProductData;
using ProductDataEntity = APITemplate.Domain.Entities.ProductData;
using VideoProductDataEntity = APITemplate.Domain.Entities.VideoProductData;

namespace APITemplate.Application.Features.ProductData.Mappings;
public static class ProductDataMappings
{
    public static ProductDataResponse ToResponse(this ProductDataEntity data) =>
        data switch
        {
            ImageProductDataEntity image => new ImageProductDataResponse
            {
                Id = image.Id,
                Type = "image",
                Title = image.Title,
                Description = image.Description,
                CreatedAt = image.CreatedAt,
                Width = image.Width,
                Height = image.Height,
                Format = image.Format,
                FileSizeBytes = image.FileSizeBytes
            },
            VideoProductDataEntity video => new VideoProductDataResponse
            {
                Id = video.Id,
                Type = "video",
                Title = video.Title,
                Description = video.Description,
                CreatedAt = video.CreatedAt,
                DurationSeconds = video.DurationSeconds,
                Resolution = video.Resolution,
                Format = video.Format,
                FileSizeBytes = video.FileSizeBytes
            },
            _ => throw new InvalidOperationException($"Unknown ProductData type: {data.GetType().Name}")
        };
}
