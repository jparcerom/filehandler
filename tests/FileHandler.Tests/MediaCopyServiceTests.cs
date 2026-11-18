using System;
using System.IO;
using FileHandler.Models;
using FileHandler.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileHandler.Tests;

[TestClass]
public sealed class MediaCopyServiceTests
{
    [TestMethod]
    public void Scan_ClassifiesSupportedExtensionsWithoutCaseSensitivity()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var nested = Path.Combine(source, "nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(source, "photo.JPG"), "image");
            File.WriteAllText(Path.Combine(nested, "raw.RAW"), "raw image");
            File.WriteAllText(Path.Combine(nested, "clip.Mp4"), "video");
            File.WriteAllText(Path.Combine(source, "notes.txt"), "not media");

            var service = new MediaCopyService(Path.Combine(root, "logs", "filehandler.log"));
            var result = service.Scan(source, new Progress<OperationProgress>());

            Assert.AreEqual(2, result.ImageFiles.Count);
            Assert.AreEqual(1, result.VideoFiles.Count);
            Assert.AreEqual(1, result.SubdirectoryCount);
            Assert.AreEqual(0, result.Errors);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Copy_PreservesExistingFilesAndAddsSuffixForBothCollisionTypes()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(source);
            var image = Path.Combine(source, "photo.jpg");
            var video = Path.Combine(source, "clip.mp4");
            File.WriteAllText(image, "same content");
            File.WriteAllText(video, "incoming video");
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "photo.jpg"), "same content");
            File.WriteAllText(Path.Combine(destination, "photo-1.jpg"), "another existing copy");
            File.WriteAllText(Path.Combine(destination, "clip.mp4"), "different video");

            var service = new MediaCopyService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var summary = service.Copy(
                scan,
                copyImages: true,
                imageDestination: destination,
                copyVideos: true,
                videoDestination: destination,
                new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.ImagesCopied);
            Assert.AreEqual(1, summary.VideosCopied);
            Assert.AreEqual(0, summary.CopyErrors);
            Assert.AreEqual("same content", File.ReadAllText(Path.Combine(destination, "photo.jpg")));
            Assert.AreEqual("another existing copy", File.ReadAllText(Path.Combine(destination, "photo-1.jpg")));
            Assert.AreEqual("same content", File.ReadAllText(Path.Combine(destination, "photo-2.jpg")));
            Assert.AreEqual("different video", File.ReadAllText(Path.Combine(destination, "clip.mp4")));
            Assert.AreEqual("incoming video", File.ReadAllText(Path.Combine(destination, "clip-1.mp4")));
            Assert.IsTrue(File.Exists(image));
            Assert.IsTrue(File.Exists(video));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Copy_CopiesOnlyTheSelectedMediaType()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            var imageDestination = Path.Combine(root, "images");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "photo.jpg"), "image");
            File.WriteAllText(Path.Combine(source, "clip.mp4"), "video");

            var service = new MediaCopyService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var summary = service.Copy(
                scan,
                copyImages: true,
                imageDestination: imageDestination,
                copyVideos: false,
                videoDestination: Path.Combine(root, "videos"),
                progress: new Progress<OperationProgress>());

            Assert.AreEqual(1, summary.ImagesCopied);
            Assert.AreEqual(0, summary.VideosToCopy);
            Assert.IsTrue(File.Exists(Path.Combine(imageDestination, "photo.jpg")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "videos")));
            Assert.IsTrue(File.Exists(Path.Combine(source, "clip.mp4")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Copy_RejectsDestinationInsideSource()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "photo.jpg"), "image");

            var service = new MediaCopyService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());

            Assert.ThrowsException<ArgumentException>(() => service.Copy(
                scan,
                copyImages: true,
                imageDestination: Path.Combine(source, "output"),
                copyVideos: false,
                videoDestination: null,
                new Progress<OperationProgress>()));
            Assert.IsFalse(Directory.Exists(Path.Combine(source, "output")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Copy_ReportsLockedSourceFilesAndPreservesThem()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            var sourceFile = Path.Combine(source, "photo.jpg");
            File.WriteAllText(sourceFile, "locked image");

            var service = new MediaCopyService(Path.Combine(root, "logs", "filehandler.log"));
            var scan = service.Scan(source, new Progress<OperationProgress>());
            var destination = Path.Combine(root, "destination");
            using (new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var summary = service.Copy(
                    scan,
                    copyImages: true,
                    imageDestination: destination,
                    copyVideos: false,
                    videoDestination: null,
                    new Progress<OperationProgress>());

                Assert.AreEqual(0, summary.ImagesCopied);
                Assert.AreEqual(1, summary.CopyErrors);
                Assert.IsTrue(File.Exists(sourceFile));
                Assert.IsFalse(File.Exists(Path.Combine(destination, "photo.jpg")));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"FileHandler.MediaTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
