// ETERVALE_ANDROID_CONTENT_V3
using System;
using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content.Res;
using Microsoft.Xna.Framework;
using MonoGame.Framework.Content;

namespace EtervaleAndroidGame;

// The MonoGame Native fork resolves desktop files under AppContext.BaseDirectory.
// On Android we explicitly use a content provider, which supports app-private
// external files (/sdcard/Android/data/com.etervale.game/files) and APK assets.
internal sealed class EtervaleAndroidContentProvider : IContentProvider
{
    private readonly AssetManager _assets;
    private readonly string _root;

    private EtervaleAndroidContentProvider(AssetManager assets, string root)
    {
        _assets = assets;
        _root = Path.GetFullPath(root);
    }

    public static void Configure(Activity activity)
    {
        string root = activity.GetExternalFilesDir(null)?.AbsolutePath
            ?? activity.FilesDir?.AbsolutePath
            ?? throw new InvalidOperationException("Sem armazenamento da aplicacao Android.");
        Directory.CreateDirectory(root);
        // ETERVALE_ANDROID_ASSETS_V4: FindAssetsRoot() must find this directory
        // before falling back to AppContext.BaseDirectory (/data/user/0/...).
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        Directory.SetCurrentDirectory(root);
        TitleContainer.ContentProvider = new EtervaleAndroidContentProvider(activity.Assets!, root);
        Android.Util.Log.Info("ETERVALE_3D", "Content e assets root: " + root);
    }

    public Task<bool> FetchContent(string relativePath) => Task.FromResult(true);

    public Stream OpenReadStream(string relativePath)
    {
        var name = relativePath.Replace('\\', '/');
        if (name.StartsWith('/') || Array.IndexOf(name.Split('/'), "..") >= 0)
            return null!;

        string full = Path.GetFullPath(Path.Combine(_root, name.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return null!;

        if (File.Exists(full))
            return File.OpenRead(full);  // best for large .xnb and pushed resources

        try
        {
            using Stream data = _assets.Open(name);
            // Android assets may not be seekable; ContentReader requires a
            // seekable stream. Limit this conversion to a single XNB request.
            var memory = new MemoryStream();
            data.CopyTo(memory);
            memory.Position = 0;
            return memory;
        }
        catch (Java.IO.FileNotFoundException)
        {
            return null!;
        }
        catch (System.IO.FileNotFoundException)
        {
            return null!;
        }
    }
}
