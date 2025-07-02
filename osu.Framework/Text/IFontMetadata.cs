// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Framework.Text
{
    /// <summary>
    /// Interface representing font metadata that provides character glyphs and metrics.
    /// </summary>
    public interface IFontMetadata
    {
        /// <summary>
        /// Gets the baseline distance from the top of the line.
        /// </summary>
        int Base { get; }

        /// <summary>
        /// Gets the size of the font in pixels.
        /// </summary>
        int FontSize { get; }

        /// <summary>
        /// Gets the number of texture pages in the font.
        /// </summary>
        int PageCount { get; }

        /// <summary>
        /// Checks if the font has a glyph for the given character.
        /// </summary>
        bool HasCharacter(Grapheme character);

        /// <summary>
        /// Gets the metadata for the given character.
        /// </summary>
        ICharacterMetadata? GetCharacter(Grapheme character);

        /// <summary>
        /// Gets the available characters in the font.
        /// </summary>
        IEnumerable<Grapheme> GetAvailableCharacters();

        /// <summary>
        /// Gets the filename of the page at the given index.
        /// </summary>
        string? GetPageFilename(int page);

        /// <summary>
        /// Gets the kerning amount between two characters.
        /// </summary>
        /// <param name="left">The left character.</param>
        /// <param name="right">The right character.</param>
        /// <returns>The kerning amount in pixels.</returns>
        int GetKerningAmount(Grapheme left, Grapheme right);

        /// <summary>
        /// Interface representing metadata for a single character.
        /// </summary>
        public interface ICharacterMetadata
        {
            int Width { get; }
            int Height { get; }
            int X { get; }
            int Y { get; }

            /// <summary>
            /// How much the current x-position should be moved for drawing. This should not adjust the cursor position.
            /// </summary>
            float XOffset { get; }

            /// <summary>
            /// How much the current y-position should be moved for drawing. This should not adjust the cursor position.
            /// </summary>
            float YOffset { get; }

            /// <summary>
            /// How much the current x-position should be moved after drawing a character.
            /// </summary>
            float XAdvance { get; }

            /// <summary>
            /// Gets the texture page index of the character.
            /// </summary>
            int Page { get; }
        }
    }
}
