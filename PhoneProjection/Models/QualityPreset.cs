namespace PhoneProjection.Models
{
    public class QualityPreset
    {
        public string Name { get; set; } = string.Empty;
        public int MaxSize { get; set; }
        public int BitrateMbps { get; set; } = 8;
        public bool IsCustom { get; set; } = false;
        public string Description { get; set; } = string.Empty;

        public override string ToString() => IsCustom 
            ? $"{Name} (User Defined)" 
            : $"{Name} ({MaxSize}p, {BitrateMbps} Mbps)";
    }

    public static class Presets
    {
        public static readonly QualityPreset Balanced = new()
        {
            Name = "Balanced",
            MaxSize = 1600,
            BitrateMbps = 8,
            IsCustom = false,
            Description = "Max size 1600, 8 Mbps bitrate. User baseline configuration, optimal balance."
        };

        public static readonly QualityPreset Smooth = new()
        {
            Name = "Smooth",
            MaxSize = 1280,
            BitrateMbps = 6,
            IsCustom = false,
            Description = "Max size 1280, 6 Mbps bitrate. Reduced encoding load, fluid high-motion playback."
        };

        public static readonly QualityPreset HighQuality = new()
        {
            Name = "High Quality",
            MaxSize = 1920,
            BitrateMbps = 12,
            IsCustom = false,
            Description = "Max size 1920, 12 Mbps bitrate. Sharp detail rendering on 1080p+ screens."
        };

        public static readonly QualityPreset Custom = new()
        {
            Name = "Custom",
            MaxSize = 1600,
            BitrateMbps = 8,
            IsCustom = true,
            Description = "Custom defined max size and bitrate."
        };

        public static QualityPreset[] All => new[] { Balanced, Smooth, HighQuality, Custom };
    }
}
