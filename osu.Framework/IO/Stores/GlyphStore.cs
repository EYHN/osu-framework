// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Text;
using SharpFNT;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Framework.IO.Stores
{
    /// <summary>
    /// A basic glyph store that will load font sprite sheets every character retrieval.
    /// </summary>
    public class GlyphStore : IResourceStore<TextureUpload>, IGlyphStore
    {
        protected readonly string AssetName;

        protected readonly IResourceStore<TextureUpload> TextureLoader;

        public string FontName { get; }

        public float? Baseline => FontMetadata?.Base;

        public int FontSize => FontMetadata?.FontSize ?? 100;

        /// <summary>
        /// Whether this font contains coloured textures. This is primarily used for emoji.
        /// </summary>
        public bool Coloured { get; }

        protected readonly ResourceStore<byte[]> Store;

        /// <summary>
        /// The folder name containing the font files.
        /// This is used as the base path for loading font page textures and resources.
        /// </summary>
        protected readonly string AssetFolderName;

        [CanBeNull]
        protected IFontMetadata FontMetadata => completionSource.Task.GetResultSafely();

        private readonly TaskCompletionSource<IFontMetadata> completionSource = new TaskCompletionSource<IFontMetadata>();

        /// <summary>
        /// This is a rare usage of a static framework-wide cache.
        /// In normal execution font instances are held locally by font stores and this will add no overhead or improvement.
        /// It exists specifically to avoid overheads of parsing fonts repeatedly in unit tests.
        /// </summary>
        private static readonly ConcurrentDictionary<string, IFontMetadata> font_cache = new ConcurrentDictionary<string, IFontMetadata>();

        /// <summary>
        /// Create a new glyph store.
        /// </summary>
        /// <param name="store">The store to provide font resources.</param>
        /// <param name="assetName">The base name of the font.</param>
        /// <param name="textureLoader">An optional platform-specific store for loading textures. Should load for the store provided in <param ref="param"/>.</param>
        /// <param name="coloured">Whether this font contains coloured textures. This is primarily used for emoji.</param>
        public GlyphStore(ResourceStore<byte[]> store, string assetName = null, IResourceStore<TextureUpload> textureLoader = null, bool coloured = false)
        {
            Store = new ResourceStore<byte[]>(store);

            // Add supported font file extensions
            // <see cref="SharpFntFontMetadata"/>
            Store.AddExtension("fnt");
            Store.AddExtension("bin");
            // <see cref="JsonFontMetadata"/>
            Store.AddExtension("json");

            AssetName = assetName;
            AssetFolderName = assetName?[..assetName.LastIndexOf('/')];
            TextureLoader = textureLoader;

            FontName = assetName?.Split('/').Last() ?? string.Empty;

            Coloured = coloured;
        }

        private Task fontLoadTask;

        public Task LoadFontAsync() => fontLoadTask ??= Task.Factory.StartNew(() =>
        {
            try
            {
                IFontMetadata fontMetadata;

                using (var s = Store.GetStream($@"{AssetName}", out string filename))
                {
                    string hash = s.ComputeMD5Hash();

                    if (font_cache.TryGetValue(hash, out fontMetadata))
                    {
                        Logger.Log($"Cached font load for {AssetName}");
                    }
                    else
                    {
                        font_cache.TryAdd(hash, fontMetadata = CreateFontMetadataFromStream(s, filename));
                    }
                }

                completionSource.SetResult(fontMetadata);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Couldn't load font asset from {AssetName}.");
                completionSource.SetResult(null);
                throw;
            }
        }, TaskCreationOptions.PreferFairness);

        /// <summary>
        /// Loads the font metadata from the given stream.
        /// </summary>
        /// <param name="stream">The stream containing the font metadata.</param>
        /// <param name="filename">The filename of the font metadata.</param>
        protected virtual IFontMetadata CreateFontMetadataFromStream(Stream stream, string filename)
        {
            if (filename.EndsWith(".fnt", StringComparison.Ordinal) || filename.EndsWith(".bin", StringComparison.Ordinal))
            {
                return SharpFntFontMetadata.FromStream(stream, FormatHint.Binary, false);
            }

            if (filename.EndsWith(".json", StringComparison.Ordinal))
            {
                return JsonFontMetadata.FromStream(stream);
            }

            throw new NotSupportedException($"Unsupported font format: {filename}");
        }

        public bool HasGlyph(Grapheme c) => FontMetadata?.HasCharacter(c) == true;

        protected virtual TextureUpload GetPageImage(int page)
        {
            if (TextureLoader != null)
                return TextureLoader.Get(GetFilenameForPage(page));

            using (var stream = Store.GetStream(GetFilenameForPage(page)))
                return new TextureUpload(stream);
        }

        protected string GetFilenameForPage(int page)
        {
            Debug.Assert(FontMetadata != null);
            return $@"{AssetFolderName}/{FontMetadata.GetPageFilename(page)}";
        }

        public CharacterGlyph Get(Grapheme character)
        {
            if (FontMetadata == null)
                return null;

            Debug.Assert(Baseline != null);

            var characterMetadata = FontMetadata.GetCharacter(character);

            Debug.Assert(characterMetadata != null);

            return new CharacterGlyph(character, characterMetadata.XOffset, characterMetadata.YOffset, characterMetadata.XAdvance, Baseline.Value, this);
        }

        /// <summary>
        /// This is a convenience method that converts the character to a <see cref="Grapheme"/> and calls <see cref="Get(Grapheme)"/>.
        /// </summary>
        /// <param name="character">The character to retrieve.</param>
        public CharacterGlyph Get(char character)
        {
            return Get(new Grapheme(character));
        }

        public int GetKerning(Grapheme left, Grapheme right) => FontMetadata?.GetKerningAmount(left, right) ?? 0;

        Task<CharacterGlyph> IResourceStore<CharacterGlyph>.GetAsync(string name, CancellationToken cancellationToken) =>
            Task.Run(() => ((IGlyphStore)this).Get(new Grapheme(name)), cancellationToken);

        CharacterGlyph IResourceStore<CharacterGlyph>.Get(string name) => Get(new Grapheme(name));

        public TextureUpload Get(string name)
        {
            if (FontMetadata == null) return null;

            Grapheme grapheme;

            // name is expected to be in the format "{Grapheme}" or "Font:{FontName}/{Grapheme}"
            // this is a shorthand to check if there is a font name in the lookup
            if (name.StartsWith("Font:", StringComparison.Ordinal))
            {
                // if FontName does not match, return null.
                if (!name.StartsWith($@"Font:{FontName}/", StringComparison.Ordinal))
                    return null;

                grapheme = new Grapheme(name.AsSpan(FontName.Length + 6));
            }
            else
            {
                grapheme = new Grapheme(name);
            }

            var characterMetadata = FontMetadata.GetCharacter(grapheme);
            return characterMetadata != null ? LoadCharacter(characterMetadata) : null;
        }

        public virtual async Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            await completionSource.Task.ConfigureAwait(false);

            return Get(name);
        }

        protected int LoadedGlyphCount;

        protected virtual TextureUpload LoadCharacter(IFontMetadata.ICharacterMetadata character)
        {
            var page = GetPageImage(character.Page);
            LoadedGlyphCount++;

            var image = new Image<Rgba32>(SixLabors.ImageSharp.Configuration.Default, character.Width, character.Height);
            var source = page.Data;

            // the spritesheet may have unused pixels trimmed
            int readableHeight = Math.Min(character.Height, page.Height - character.Y);
            int readableWidth = Math.Min(character.Width, page.Width - character.X);

            for (int y = 0; y < character.Height; y++)
            {
                var pixelRowMemory = image.DangerousGetPixelRowMemory(y);
                int readOffset = (character.Y + y) * page.Width + character.X;

                for (int x = 0; x < character.Width; x++)
                    pixelRowMemory.Span[x] = x < readableWidth && y < readableHeight ? source[readOffset + x] : new Rgba32(255, 255, 255, 0);
            }

            return new TextureUpload(image);
        }

        public Stream GetStream(string name) => throw new NotSupportedException();

        public IEnumerable<string> GetAvailableResources() => FontMetadata?.GetAvailableCharacters().Select(k => $"Font:{FontName}/{k}") ?? Enumerable.Empty<string>();

        #region IDisposable Support

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        #endregion
    }
}
