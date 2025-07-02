// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using osu.Framework.Extensions;
using SharpFNT;

namespace osu.Framework.Text
{
    /// <summary>
    /// A font metadata implementation for SharpFNT bitmap fonts.
    /// </summary>
    public class SharpFntFontMetadata : IFontMetadata
    {
        public readonly BitmapFont Font;

        /// <summary>
        /// Creates a new SharpFNT bitmap font metadata wrapper.
        /// </summary>
        public SharpFntFontMetadata(BitmapFont font)
        {
            Font = font;
        }

        /// <summary>
        /// Creates a new SharpFNT bitmap font from a stream.
        /// </summary>
        /// <param name="stream">The stream containing the font data.</param>
        /// <param name="formatHint">The format hint for parsing.</param>
        /// <param name="optimizeForPowerOfTwo">Whether to optimize for power of two textures.</param>
        /// <returns>A new SharpFNT bitmap font metadata wrapper.</returns>
        public static SharpFntFontMetadata FromStream(Stream stream, FormatHint formatHint = FormatHint.Binary, bool optimizeForPowerOfTwo = false)
        {
            var font = BitmapFont.FromStream(stream, formatHint, optimizeForPowerOfTwo);
            return new SharpFntFontMetadata(font);
        }

        // IFontMetadata implementation
        public int Base => Font.Common?.Base ?? 0;

        public int PageCount => Font.Pages?.Count ?? 0;

        public int FontSize => Font.Info?.Size ?? 0;

        public bool HasCharacter(Grapheme character)
        {
            if (!character.IsSingleScalarValue || Font.Characters == null)
                return false;

            return Font.Characters.ContainsKey(((Rune)character).Value);
        }

        public IEnumerable<Grapheme> GetAvailableCharacters() => Font.Characters.Keys.Select(k => new Grapheme(((Rune)k).ToString()));

        public IFontMetadata.ICharacterMetadata? GetCharacter(Grapheme character)
        {
            if (!character.IsSingleScalarValue || Font.Characters == null)
            {
                return null;
            }

            var sharpFntCharacter = Font.GetCharacter((Rune)character);
            return sharpFntCharacter != null ? new SharpFntCharacterMetadata(sharpFntCharacter) : null;
        }

        public string? GetPageFilename(int page) => Font.Pages[page];

        public int GetKerningAmount(Grapheme left, Grapheme right)
        {
            if (Font.KerningPairs == null)
                return 0;

            if (!left.IsSingleScalarValue || !right.IsSingleScalarValue)
                return 0;

            return Font.KerningPairs.TryGetValue(new KerningPair(((Rune)left).Value, ((Rune)right).Value), out int kerningValue) ? kerningValue : 0;
        }

        /// <summary>
        /// SharpFNT implementation of ICharacterMetadata.
        /// </summary>
        private class SharpFntCharacterMetadata : IFontMetadata.ICharacterMetadata
        {
            private readonly Character character;

            public SharpFntCharacterMetadata(Character character)
            {
                this.character = character;
            }

            public int Width => character.Width;
            public int Height => character.Height;
            public int X => character.X;
            public int Y => character.Y;
            public int Page => character.Page;
            public float XOffset => character.XOffset;
            public float YOffset => character.YOffset;
            public float XAdvance => character.XAdvance;
        }
    }
}
