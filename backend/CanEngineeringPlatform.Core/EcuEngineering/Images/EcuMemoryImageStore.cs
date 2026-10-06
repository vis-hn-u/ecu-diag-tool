using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace CanEngineeringPlatform.Core.EcuEngineering.Images;

public interface IEcuMemoryImageStore
{
    void StoreImage(EcuMemoryImage image);
    IReadOnlyList<EcuMemoryImage> GetAllImages();
    EcuMemoryImage? GetImageById(string imageId);
    EcuMemoryImage? GetImageByHash(string sha256);
    ReadManifest? GetManifest(string imageId);
    byte[]? GetRawBytes(string imageId);
}

public class EcuMemoryImageStore : IEcuMemoryImageStore
{
    private readonly ConcurrentDictionary<string, EcuMemoryImage> _imagesById = new(StringComparer.OrdinalIgnoreCase);

    public void StoreImage(EcuMemoryImage image)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        if (string.IsNullOrWhiteSpace(image.Sha256Hash) && image.RawBytes.Length > 0)
        {
            image.Sha256Hash = EcuMemoryImage.ComputeSha256(image.RawBytes);
        }

        // Never overwrite an existing image ID: generate unique ID if needed
        string id = image.ImageId;
        if (string.IsNullOrWhiteSpace(id) || _imagesById.ContainsKey(id))
        {
            id = $"IMG-{Guid.NewGuid():N}";
            image.ImageId = id;
        }

        _imagesById[id] = image;
    }

    public IReadOnlyList<EcuMemoryImage> GetAllImages()
    {
        return _imagesById.Values.OrderByDescending(i => i.TimestampUtc).ToList();
    }

    public EcuMemoryImage? GetImageById(string imageId)
    {
        if (string.IsNullOrWhiteSpace(imageId)) return null;
        _imagesById.TryGetValue(imageId, out var img);
        return img;
    }

    public EcuMemoryImage? GetImageByHash(string sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256)) return null;
        return _imagesById.Values.FirstOrDefault(i => string.Equals(i.Sha256Hash, sha256, StringComparison.OrdinalIgnoreCase));
    }

    public ReadManifest? GetManifest(string imageId)
    {
        var img = GetImageById(imageId);
        return img != null ? ReadManifest.FromImage(img) : null;
    }

    public byte[]? GetRawBytes(string imageId)
    {
        var img = GetImageById(imageId);
        return img?.RawBytes;
    }
}
