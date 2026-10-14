namespace PhoneProjection.Models
{
    public class QualityPreset
    {
        public string Name { get; set; } = string.Empty;
        public int MaxSize { get; set; }
        public int BitrateMbps { get; set; } = 8;
        public int RecommendedFps { get; set; } = 60;
        public bool IsCustom { get; set; } = false;
        public string UseCaseCategory { get; set; } = "General";
        public string Description { get; set; } = string.Empty;

        public override string ToString() => IsCustom 
            ? $"{Name} (User Defined)" 
            : $"{Name} [{UseCaseCategory}] ({MaxSize}p, {BitrateMbps}M, {RecommendedFps}fps)";
    }

    public static class Presets
    {
        // Core Balanced
        public static readonly QualityPreset Balanced = new()
        {
            Name = "Balanced",
            MaxSize = 1600,
            BitrateMbps = 8,
            RecommendedFps = 60,
            UseCaseCategory = "Standard",
            Description = "1600p cap, 8 Mbps bitrate. Sony Xperia XZ3 default baseline, smooth & crisp."
        };

        // Smooth / Gaming
        public static readonly QualityPreset Smooth = new()
        {
            Name = "Smooth",
            MaxSize = 1280,
            BitrateMbps = 6,
            RecommendedFps = 60,
            UseCaseCategory = "Gaming",
            Description = "1280p cap, 6 Mbps bitrate. Lowest encoder overhead, ultra-responsive low buffer."
        };

        // High Quality / Reading
        public static readonly QualityPreset HighQuality = new()
        {
            Name = "High Quality",
            MaxSize = 1920,
            BitrateMbps = 12,
            RecommendedFps = 60,
            UseCaseCategory = "Reading",
            Description = "1920p cap, 12 Mbps bitrate. Sharp text and fine UI detail rendering."
        };

        // Video Playback
        public static readonly QualityPreset VideoPlayback = new()
        {
            Name = "Video Playback",
            MaxSize = 1600,
            BitrateMbps = 10,
            RecommendedFps = 60,
            UseCaseCategory = "Video",
            Description = "1600p cap, 10 Mbps bitrate. Enhanced color depth and motion bitrate for movies & streaming."
        };

        // Low Bandwidth (Wi-Fi 2.4GHz / Weak links)
        public static readonly QualityPreset LowBandwidth = new()
        {
            Name = "Low Bandwidth",
            MaxSize = 1024,
            BitrateMbps = 3,
            RecommendedFps = 30,
            UseCaseCategory = "Wireless",
            Description = "1024p cap, 3 Mbps bitrate, 30 FPS. Optimized for congested Wi-Fi networks."
        };

        // Custom
        public static readonly QualityPreset Custom = new()
        {
            Name = "Custom",
            MaxSize = 1600,
            BitrateMbps = 8,
            RecommendedFps = 60,
            IsCustom = true,
            UseCaseCategory = "Custom",
            Description = "User defined custom resolution, bitrate, and frame rate cap."
        };

        public static QualityPreset[] All => new[] { Balanced, Smooth, HighQuality, VideoPlayback, LowBandwidth, Custom };
    }
}
