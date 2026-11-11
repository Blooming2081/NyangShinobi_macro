using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NyangShinobi.Domain;

namespace NyangShinobi.App.Diagnostics;

public static class DiagnosticImages
{
    public static Task<BitmapSource> CreatePreviewAsync(ImageFrame frame, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!MemoryMarshal.TryGetArray(frame.Pixels, out var pixels) || pixels.Offset != 0)
                throw new InvalidOperationException("미리보기에 관리 배열 프레임이 필요합니다.");
            // 관리 프레임 복제 없이 WPF에 한 번만 전달한다. 렌더링/ROI 변경 시 다시 만들지 않는다.
            var bitmap = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, pixels.Array!, frame.Stride);
            bitmap.Freeze();
            cancellationToken.ThrowIfCancellationRequested();
            return bitmap;
        }, cancellationToken);

    public static Task SavePngAsync(BitmapSource image, string path, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (!image.IsFrozen) throw new ArgumentException("저장할 이미지는 Freeze 상태여야 합니다.", nameof(image));
            cancellationToken.ThrowIfCancellationRequested();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            encoder.Save(file);
        }, cancellationToken);
}
