using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace FileHandler.Services;

public static class MediaThumbnailProvider
{
    private const int ThumbnailWidth = 160;
    private const int ThumbnailHeight = 100;

    public static BitmapSource? Load(string path, bool isImage)
    {
        if (isImage && TryLoadImage(path, out var image))
        {
            return image;
        }

        return TryLoadShellThumbnail(path);
    }

    private static bool TryLoadImage(string path, out BitmapSource? image)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = ThumbnailWidth;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            image = bitmap;
            return true;
        }
        catch (IOException)
        {
            image = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            image = null;
            return false;
        }
        catch (NotSupportedException)
        {
            image = null;
            return false;
        }
        catch (FileFormatException)
        {
            image = null;
            return false;
        }
    }

    private static BitmapSource? TryLoadShellThumbnail(string path)
    {
        var imageFactoryInterfaceId = new Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B");
        var shellItemPointer = IntPtr.Zero;
        var bitmapHandle = IntPtr.Zero;
        IShellItemImageFactory? imageFactory = null;

        try
        {
            var result = SHCreateItemFromParsingName(
                path,
                IntPtr.Zero,
                ref imageFactoryInterfaceId,
                out shellItemPointer);
            if (result < 0 || shellItemPointer == IntPtr.Zero)
            {
                return null;
            }

            imageFactory = (IShellItemImageFactory)Marshal.GetObjectForIUnknown(shellItemPointer);
            result = imageFactory.GetImage(
                new NativeSize(ThumbnailWidth, ThumbnailHeight),
                ShellImageFlags.ThumbnailOnly | ShellImageFlags.BiggerSizeOk,
                out bitmapHandle);
            if (result < 0 || bitmapHandle == IntPtr.Zero)
            {
                return null;
            }

            var bitmap = Imaging.CreateBitmapSourceFromHBitmap(
                bitmapHandle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
            return bitmap;
        }
        catch (COMException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (ExternalException)
        {
            return null;
        }
        finally
        {
            if (bitmapHandle != IntPtr.Zero)
            {
                DeleteObject(bitmapHandle);
            }

            if (imageFactory is not null)
            {
                Marshal.ReleaseComObject(imageFactory);
            }

            if (shellItemPointer != IntPtr.Zero)
            {
                Marshal.Release(shellItemPointer);
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        ref Guid interfaceId,
        out IntPtr item);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern bool DeleteObject(IntPtr handle);

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, ShellImageFlags flags, out IntPtr bitmap);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize
    {
        public NativeSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public readonly int Width;
        public readonly int Height;
    }

    [Flags]
    private enum ShellImageFlags
    {
        BiggerSizeOk = 0x1,
        ThumbnailOnly = 0x8
    }
}
