// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace osu.Framework.Text
{
    /// <summary>
    /// A font metadata implementation for JSON-based font metadata.
    /// </summary>
    public class JsonFontMetadata : IFontMetadata
    {
        private readonly FontInfo fontInfo;
        private readonly Dictionary<Grapheme, CharacterData> characterLookup = new Dictionary<Grapheme, CharacterData>();

        /// <summary>
        /// Creates a new JSON font metadata from parsed font information.
        /// </summary>
        public JsonFontMetadata(FontInfo fontInfo)
        {
            this.fontInfo = fontInfo;

            // Build character lookup dictionary for faster access
            if (fontInfo.Chars?.Characters != null)
            {
                foreach (var character in fontInfo.Chars.Characters)
                {
                    if (character.Code != null && character.Code.Length > 0)
                    {
                        StringBuilder sb = new StringBuilder("", 8);

                        foreach (int codePoint in character.Code)
                        {
                            sb.Append(new Rune(codePoint));
                        }

                        characterLookup[new Grapheme(sb.ToString())] = character;
                    }
                }
            }
        }

        private static readonly JsonSerializerOptions serializer_options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Creates a new JSON font metadata from a stream containing JSON data.
        /// </summary>
        /// <param name="stream">The stream containing the JSON font data.</param>
        /// <returns>A new JSON font metadata instance.</returns>
        public static JsonFontMetadata FromStream(Stream stream)
        {
            var fontInfo = JsonSerializer.Deserialize<FontInfo>(stream, serializer_options);

            if (fontInfo == null)
                throw new InvalidOperationException("Failed to deserialize font metadata from JSON");

            return new JsonFontMetadata(fontInfo);
        }

        // IFontMetadata implementation
        public int Base => fontInfo.Common?.Base ?? 0;

        public int PageCount => fontInfo.Pages?.Length ?? 0;

        public int FontSize => 50;

        public bool HasCharacter(Grapheme character)
        {
            return characterLookup.ContainsKey(character);
        }

        public IEnumerable<Grapheme> GetAvailableCharacters()
        {
            return characterLookup.Keys;
        }

        public IFontMetadata.ICharacterMetadata? GetCharacter(Grapheme character)
        {
            if (characterLookup.TryGetValue(character, out var characterData))
            {
                return new JsonCharacterMetadata(characterData);
            }

            return null;
        }

        public string? GetPageFilename(int page)
        {
            if (fontInfo.Pages == null || page < 0 || page >= fontInfo.Pages.Length)
                return null;

            return fontInfo.Pages[page].File;
        }

        public int GetKerningAmount(Grapheme left, Grapheme right)
        {
            // JSON format doesn't include kerning information
            return 0;
        }

        /// <summary>
        /// JSON implementation of ICharacterMetadata.
        /// </summary>
        private class JsonCharacterMetadata : IFontMetadata.ICharacterMetadata
        {
            private readonly CharacterData character;

            public JsonCharacterMetadata(CharacterData character)
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

        // JSON data structures for deserialization
        public class FontInfo
        {
            public InfoData? Info { get; set; }
            public CommonData? Common { get; set; }
            public PageData[]? Pages { get; set; }
            public CharsData? Chars { get; set; }
        }

        public class InfoData
        {
            public string? Face { get; set; }
        }

        public class CommonData
        {
            public int LineHeight { get; set; }
            public int Base { get; set; }
        }

        public class PageData
        {
            public int Id { get; set; }
            public string? File { get; set; }
        }

        public class CharsData
        {
            public int Count { get; set; }

            [JsonPropertyName("chars")]
            public CharacterData[]? Characters { get; set; }
        }

        public class CharacterData
        {
            public int[]? Code { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }

            public float XOffset { get; set; }

            public float YOffset { get; set; }

            public float XAdvance { get; set; }

            public int Page { get; set; }
        }
    }
}
